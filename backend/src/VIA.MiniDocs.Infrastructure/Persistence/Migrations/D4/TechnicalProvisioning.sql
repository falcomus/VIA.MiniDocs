ALTER TABLE minidocs.via_docs_accounts ADD COLUMN credential_generation bigint NOT NULL DEFAULT 1
  CONSTRAINT ck_via_docs_credential_generation CHECK (credential_generation > 0);

DO $$
BEGIN
  IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
    GRANT CREATE ON SCHEMA minidocs TO minidocs_command_owner;
    SET LOCAL ROLE minidocs_command_owner;
  END IF;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_claim_provisioning_verification(p_workspace uuid, p_operation uuid,
    p_version bigint, p_owner uuid, p_duration interval) RETURNS bigint
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE result bigint; workspace_status text;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN NULL; END IF;
  SELECT w.status INTO workspace_status FROM minidocs.workspaces w
    JOIN minidocs.identity_users u ON u.id = w.owner_user_id
    WHERE w.id = p_workspace AND w.status IN ('pending', 'suspended') AND NOT u.is_disabled
      AND u.account_kind = 'regular'
    FOR SHARE OF u;
  IF NOT FOUND THEN RETURN NULL; END IF;
  UPDATE minidocs.provisioning_operations
  SET lease_owner = p_owner, lease_until = clock_timestamp() + p_duration,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
    AND status = 'recovery_required'
    AND EXISTS (SELECT 1 FROM minidocs.via_docs_accounts WHERE workspace_id = p_workspace AND application_id IS NULL)
    AND (lease_until IS NULL OR lease_until <= clock_timestamp())
  RETURNING version INTO result;
  RETURN result;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_complete_provisioning(p_workspace uuid, p_operation uuid, p_version bigint,
    p_owner uuid, p_application uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE db_now timestamptz; workspace_status text;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_owner, p_application], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_owner, p_application], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  SELECT w.status INTO workspace_status FROM minidocs.workspaces w
    JOIN minidocs.identity_users u ON u.id = w.owner_user_id
    WHERE w.id = p_workspace AND w.status IN ('pending', 'suspended') AND NOT u.is_disabled
      AND u.account_kind = 'regular'
    FOR SHARE OF u;
  IF NOT FOUND OR NOT EXISTS (SELECT 1 FROM minidocs.via_docs_accounts
      WHERE workspace_id = p_workspace AND application_id IS NULL AND verified_at IS NULL)
      OR EXISTS (SELECT 1 FROM minidocs.via_docs_accounts WHERE application_id = p_application)
  THEN RETURN false; END IF;
  db_now := clock_timestamp();
  UPDATE minidocs.provisioning_operations
  SET status = 'active', candidate_application_id = p_application, invitation_protected = NULL,
      failure_category = NULL, lease_owner = NULL, lease_until = NULL, updated_at = db_now, version = version + 1
  WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
    AND (status = 'recovery_required' OR (status = 'registering' AND workspace_status = 'pending'))
    AND lease_owner = p_owner AND lease_until > db_now
    AND (candidate_application_id IS NULL OR candidate_application_id = p_application);
  IF NOT FOUND THEN RETURN false; END IF;
  UPDATE minidocs.via_docs_accounts SET application_id = p_application, verified_at = db_now,
      version = version + 1, updated_at = db_now WHERE workspace_id = p_workspace AND application_id IS NULL;
  IF NOT FOUND THEN RAISE EXCEPTION 'Technical account changed during provisioning'; END IF;
  UPDATE minidocs.workspaces SET status = 'active', version = version + 1, updated_at = db_now
  WHERE id = p_workspace AND status = 'pending';
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_set_account_disabled(
  p_actor uuid, p_stamp text, p_handle bytea, p_target uuid,
  p_expected_stamp text, p_expected_roles text[], p_disabled boolean)
RETURNS smallint LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_workspace uuid;
  v_target record;
BEGIN
  PERFORM minidocs.require_command_argument(p_target IS NOT NULL AND p_disabled IS NOT NULL
    AND p_expected_roles IS NOT NULL, 'account');
  PERFORM pg_advisory_xact_lock(3841, 401);
  SELECT id INTO v_workspace FROM minidocs.workspaces WHERE owner_user_id = p_target;
  IF NOT minidocs.lock_admin_authority(p_actor, p_stamp, p_handle, p_target, v_workspace)
  THEN RETURN 1; END IF;
  IF v_workspace IS DISTINCT FROM (SELECT id FROM minidocs.workspaces WHERE owner_user_id = p_target)
  THEN RETURN 3; END IF;
  SELECT is_disabled, concurrency_stamp INTO v_target FROM minidocs.identity_users WHERE id = p_target;
  IF NOT FOUND THEN RETURN 2; END IF;
  IF p_expected_stamp IS NULL OR v_target.concurrency_stamp IS DISTINCT FROM p_expected_stamp
    OR p_expected_roles IS DISTINCT FROM ARRAY(
      SELECT r.normalized_name::text FROM minidocs.identity_user_roles ur
      JOIN minidocs.identity_roles r ON r.id = ur.role_id
      WHERE ur.user_id = p_target AND r.normalized_name IS NOT NULL ORDER BY r.normalized_name)
  THEN RETURN 3; END IF;
  IF v_target.is_disabled = p_disabled THEN RETURN 0; END IF;
  IF p_disabled AND minidocs.is_enabled_administrator(p_target)
    AND NOT EXISTS (SELECT 1 FROM minidocs.identity_users u
      WHERE u.id <> p_target AND minidocs.is_enabled_administrator(u.id))
  THEN RETURN 4; END IF;
  UPDATE minidocs.identity_users SET is_disabled = p_disabled,
    security_stamp = gen_random_uuid()::text, concurrency_stamp = gen_random_uuid()::text
    WHERE id = p_target;
  IF p_disabled AND v_workspace IS NOT NULL THEN
    UPDATE minidocs.provisioning_operations
      SET version = version + 1, updated_at = clock_timestamp()
      WHERE workspace_id = v_workspace AND status IN ('registering', 'recovery_required');
  END IF;
  RETURN 0;
END;
$$;

CREATE FUNCTION minidocs.command_replace_technical_invitation(
  p_workspace uuid, p_operation uuid, p_version bigint, p_invitation bytea) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(p_workspace IS NOT NULL AND p_operation IS NOT NULL
    AND p_version > 0 AND octet_length(p_invitation) BETWEEN 1 AND 4096, 'invitationReplacement');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT minidocs.workspace_accepts_provisioning(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.via_docs_accounts
      WHERE workspace_id = p_workspace AND application_id IS NULL AND verified_at IS NULL)
  THEN RETURN false; END IF;
  UPDATE minidocs.provisioning_operations
    SET invitation_protected = p_invitation, version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
      AND status = 'ready' AND attempt_no = 0 AND candidate_application_id IS NULL
      AND lease_owner IS NULL AND lease_until IS NULL;
  RETURN FOUND;
END;
$$;

REVOKE ALL ON FUNCTION minidocs.command_replace_technical_invitation(uuid, uuid, bigint, bytea) FROM PUBLIC;
DO $$
BEGIN
  IF to_regrole('minidocs_runtime') IS NOT NULL THEN
    GRANT EXECUTE ON FUNCTION minidocs.command_replace_technical_invitation(uuid, uuid, bigint, bytea) TO minidocs_runtime;
  END IF;
END;
$$;
RESET ROLE;
DO $$
BEGIN
  IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
    REVOKE CREATE ON SCHEMA minidocs FROM minidocs_command_owner;
  END IF;
END;
$$;
