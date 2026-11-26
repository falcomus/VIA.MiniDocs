-- Provisioning and invitation routines for the D2-06 migration.

CREATE OR REPLACE FUNCTION minidocs.command_begin_provisioning(p_workspace uuid, p_operation uuid, p_owner uuid, p_duration interval, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
  IF NOT minidocs.workspace_accepts_provisioning(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.provisioning_operations AS op
  SET status = 'registering', lease_owner = p_owner,
      lease_until = clock_timestamp() + p_duration,
      attempt_no = attempt_no + 1, version = version + 1, updated_at = clock_timestamp()
  WHERE op.workspace_id = p_workspace AND op.id = p_operation
    AND op.version = p_version AND op.status = 'ready'
    AND op.candidate_application_id IS NULL
    AND op.invitation_protected IS NOT NULL AND octet_length(op.invitation_protected) > 0
    AND (op.lease_until IS NULL OR op.lease_until <= clock_timestamp())
    AND EXISTS (SELECT 1 FROM minidocs.via_docs_accounts a
        WHERE a.workspace_id = op.workspace_id AND a.application_id IS NULL
          AND length(btrim(a.application_name)) > 0 AND octet_length(a.application_secret_protected) > 0);
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_record_provisioning_candidate(p_workspace uuid, p_operation uuid, p_candidate uuid, p_version bigint, p_owner uuid) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_candidate, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_candidate, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.provisioning_operations
  SET candidate_application_id = p_candidate,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_operation
    AND status = 'registering' AND version = p_version
    AND lease_owner = p_owner AND lease_until > clock_timestamp()
    AND (candidate_application_id IS NULL OR candidate_application_id = p_candidate);
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_recover_expired_provisioning(p_workspace uuid, p_operation uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.provisioning_operations
  SET status = 'recovery_required', lease_owner = NULL, lease_until = NULL,
      failure_category = 'transport_unknown', version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_operation
    AND status = 'registering' AND version = p_version
    AND lease_until <= clock_timestamp();
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_redeem_invitation(p_hash bytea, p_user uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE invitation_id uuid;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_user], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_user], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(octet_length(p_hash) = 32, 'tokenHash');
  SELECT id INTO invitation_id FROM minidocs.user_invitations WHERE token_hash = p_hash FOR UPDATE;
  IF NOT FOUND THEN RETURN false; END IF;
  UPDATE minidocs.user_invitations
  SET redeemed_at = clock_timestamp(), redeemed_user_id = p_user,
      updated_at = clock_timestamp(), version = version + 1
  WHERE id = invitation_id AND redeemed_at IS NULL AND revoked_at IS NULL AND expires_at > clock_timestamp()
    AND EXISTS (SELECT 1 FROM minidocs.identity_users WHERE id = p_user AND account_kind = 'regular');
  RETURN FOUND;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_register_workspace(p_hash bytea, p_user uuid, p_workspace uuid, p_topic uuid)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_user, p_workspace, p_topic], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_user, p_workspace, p_topic], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(octet_length(p_hash) = 32, 'tokenHash');
  IF EXISTS (SELECT 1 FROM minidocs.workspaces WHERE owner_user_id = p_user)
     OR NOT minidocs.command_redeem_invitation(p_hash, p_user) THEN RETURN false; END IF;
  INSERT INTO minidocs.workspaces(id, owner_user_id, status) VALUES (p_workspace, p_user, 'pending');
  INSERT INTO minidocs.topics(id, workspace_id, name, system_key)
  VALUES (p_topic, p_workspace, 'Nicht zugeordnet', 'UNASSIGNED');
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_lock_valid_invitation(p_hash bytea) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE invitation minidocs.user_invitations%ROWTYPE;
BEGIN
  PERFORM minidocs.require_command_argument(octet_length(p_hash) = 32, 'tokenHash');
  SELECT * INTO invitation FROM minidocs.user_invitations WHERE token_hash = p_hash FOR UPDATE;
  RETURN FOUND AND invitation.redeemed_at IS NULL AND invitation.revoked_at IS NULL AND invitation.expires_at > clock_timestamp();
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_revoke_invitation(p_invitation uuid, p_version bigint, p_actor uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_invitation, p_actor], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_invitation, p_actor], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.is_enabled_administrator(p_actor) THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.user_invitations WHERE id = p_invitation FOR UPDATE;
  UPDATE minidocs.user_invitations SET revoked_at = clock_timestamp(), updated_at = clock_timestamp(),
      version = version + 1
  WHERE id = p_invitation AND version = p_version AND revoked_at IS NULL AND redeemed_at IS NULL;
  RETURN FOUND;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_create_invitation(p_id uuid, p_hash bytea, p_creator uuid, p_expiry timestamptz)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE db_now timestamptz := clock_timestamp();
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_id, p_creator], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_id, p_creator], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(octet_length(p_hash) = 32, 'tokenHash');
  PERFORM minidocs.require_command_argument(p_expiry > db_now AND p_expiry <= db_now + interval '720 hours', 'expiresAt');
  IF NOT minidocs.is_enabled_administrator(p_creator)
  THEN RETURN false; END IF;
  INSERT INTO minidocs.user_invitations(id, token_hash, created_by, expires_at, created_at, updated_at)
  VALUES (p_id, p_hash, p_creator, p_expiry, db_now, db_now);
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_prepare_provisioning(p_workspace uuid, p_account uuid, p_operation uuid,
    p_name text, p_secret bytea, p_invitation bytea) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_account, p_operation], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_account, p_operation], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(octet_length(p_secret) > 0 AND octet_length(p_invitation) > 0, 'protectedCredentials');
  p_name := regexp_replace(p_name, '^[[:space:]]+|[[:space:]]+$', '', 'g');
  PERFORM minidocs.require_command_argument(char_length(p_name) BETWEEN 1 AND 255, 'name');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT minidocs.workspace_accepts_provisioning(p_workspace) THEN RETURN false; END IF;
  IF EXISTS (SELECT 1 FROM minidocs.via_docs_accounts WHERE workspace_id = p_workspace)
     OR EXISTS (SELECT 1 FROM minidocs.provisioning_operations WHERE workspace_id = p_workspace AND status <> 'cancelled')
  THEN RETURN false; END IF;
  INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name, application_secret_protected)
  VALUES (p_account, p_workspace, p_name, p_secret);
  INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, invitation_protected, attempt_no)
  VALUES (p_operation, p_workspace, 'ready', p_invitation, 0);
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_claim_provisioning_verification(p_workspace uuid, p_operation uuid,
    p_version bigint, p_owner uuid, p_duration interval) RETURNS bigint
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE result bigint;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN NULL; END IF;
  UPDATE minidocs.provisioning_operations
  SET lease_owner = p_owner, lease_until = clock_timestamp() + p_duration,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
    AND status = 'recovery_required' AND (lease_until IS NULL OR lease_until <= clock_timestamp())
  RETURNING version INTO result;
  RETURN result;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_complete_provisioning(p_workspace uuid, p_operation uuid, p_version bigint,
    p_owner uuid, p_application uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE db_now timestamptz;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_operation, p_owner, p_application], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_operation, p_owner, p_application], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.workspaces WHERE id = p_workspace AND status IN ('pending','suspended'))
     OR NOT EXISTS (SELECT 1 FROM minidocs.via_docs_accounts
         WHERE workspace_id = p_workspace AND application_id IS NULL) THEN RETURN false; END IF;
  db_now := clock_timestamp();
  UPDATE minidocs.provisioning_operations
  SET status = 'active', candidate_application_id = p_application, invitation_protected = NULL,
      failure_category = NULL, lease_owner = NULL, lease_until = NULL, updated_at = db_now, version = version + 1
  WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
    AND status IN ('registering', 'recovery_required') AND lease_owner = p_owner AND lease_until > db_now
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
