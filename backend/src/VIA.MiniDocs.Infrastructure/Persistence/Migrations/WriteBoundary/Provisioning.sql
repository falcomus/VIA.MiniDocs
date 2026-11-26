CREATE FUNCTION minidocs.command_begin_provisioning(p_workspace uuid, p_operation uuid, p_owner uuid, p_duration interval, p_version bigint) RETURNS integer
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  IF p_owner IS NULL OR p_owner = '00000000-0000-0000-0000-000000000000'
     OR p_duration IS NULL OR p_duration <= interval '0' THEN RETURN 0; END IF;
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
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
          AND length(btrim(a.application_name)) > 0 AND octet_length(a.application_secret_protected) > 0)
    AND EXISTS (SELECT 1 FROM minidocs.workspaces w
        WHERE w.id = op.workspace_id AND w.status = 'pending');
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE FUNCTION minidocs.command_record_provisioning_candidate(p_workspace uuid, p_operation uuid, p_candidate uuid, p_version bigint, p_owner uuid) RETURNS integer
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  IF p_candidate IS NULL OR p_candidate = '00000000-0000-0000-0000-000000000000' THEN RETURN 0; END IF;
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

CREATE FUNCTION minidocs.command_recover_expired_provisioning(p_workspace uuid, p_operation uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
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

CREATE FUNCTION minidocs.command_redeem_invitation(p_hash bytea, p_user uuid) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE invitation_id uuid;
BEGIN
  IF octet_length(p_hash) IS DISTINCT FROM 32 THEN RETURN false; END IF;
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

CREATE FUNCTION minidocs.command_register_workspace(p_hash bytea, p_user uuid, p_workspace uuid, p_topic uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF p_workspace = '00000000-0000-0000-0000-000000000000' OR p_topic = '00000000-0000-0000-0000-000000000000'
     OR EXISTS (SELECT 1 FROM minidocs.workspaces WHERE owner_user_id = p_user)
     OR NOT minidocs.command_redeem_invitation(p_hash, p_user) THEN RETURN false; END IF;
  INSERT INTO minidocs.workspaces(id, owner_user_id, status) VALUES (p_workspace, p_user, 'pending');
  INSERT INTO minidocs.topics(id, workspace_id, name, system_key)
  VALUES (p_topic, p_workspace, 'Nicht zugeordnet', 'UNASSIGNED');
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.is_enabled_administrator(p_user uuid) RETURNS boolean
LANGUAGE sql STABLE SET search_path = pg_catalog, pg_temp AS $$
  SELECT EXISTS (SELECT 1 FROM minidocs.identity_users u
    JOIN minidocs.identity_user_roles ur ON ur.user_id = u.id
    JOIN minidocs.identity_roles r ON r.id = ur.role_id
    WHERE u.id = p_user AND NOT u.is_disabled AND r.normalized_name = 'ADMINISTRATOR');
$$;

CREATE FUNCTION minidocs.command_lock_valid_invitation(p_hash bytea) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE invitation minidocs.user_invitations%ROWTYPE;
BEGIN
  IF octet_length(p_hash) IS DISTINCT FROM 32 THEN RETURN false; END IF;
  SELECT * INTO invitation FROM minidocs.user_invitations WHERE token_hash = p_hash FOR UPDATE;
  RETURN FOUND AND invitation.redeemed_at IS NULL AND invitation.revoked_at IS NULL AND invitation.expires_at > clock_timestamp();
END;
$$;

CREATE FUNCTION minidocs.command_revoke_invitation(p_invitation uuid, p_version bigint, p_actor uuid) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.is_enabled_administrator(p_actor) THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.user_invitations WHERE id = p_invitation FOR UPDATE;
  UPDATE minidocs.user_invitations SET revoked_at = clock_timestamp(), updated_at = clock_timestamp(),
      version = version + 1
  WHERE id = p_invitation AND version = p_version AND revoked_at IS NULL AND redeemed_at IS NULL;
  RETURN FOUND;
END;
$$;

CREATE FUNCTION minidocs.command_create_invitation(p_id uuid, p_hash bytea, p_creator uuid, p_expiry timestamptz)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE db_now timestamptz := clock_timestamp();
BEGIN
  IF p_expiry IS NULL OR p_expiry <= db_now OR p_expiry > db_now + interval '720 hours'
     OR octet_length(p_hash) IS DISTINCT FROM 32
     OR NOT minidocs.is_enabled_administrator(p_creator)
  THEN RETURN false; END IF;
  INSERT INTO minidocs.user_invitations(id, token_hash, created_by, expires_at, created_at, updated_at)
  VALUES (p_id, p_hash, p_creator, p_expiry, db_now, db_now);
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_prepare_provisioning(p_workspace uuid, p_account uuid, p_operation uuid,
    p_name text, p_secret bytea, p_invitation bytea) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.workspaces WHERE id = p_workspace AND status = 'pending')
     OR EXISTS (SELECT 1 FROM minidocs.via_docs_accounts WHERE workspace_id = p_workspace)
     OR EXISTS (SELECT 1 FROM minidocs.provisioning_operations WHERE workspace_id = p_workspace AND status <> 'cancelled')
     OR coalesce(length(btrim(p_name)), 0) = 0 OR coalesce(octet_length(p_secret), 0) = 0
     OR coalesce(octet_length(p_invitation), 0) = 0 THEN RETURN false; END IF;
  INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name, application_secret_protected)
  VALUES (p_account, p_workspace, p_name, p_secret);
  INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, invitation_protected, attempt_no)
  VALUES (p_operation, p_workspace, 'ready', p_invitation, 0);
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_claim_provisioning_verification(p_workspace uuid, p_operation uuid,
    p_version bigint, p_owner uuid, p_duration interval) RETURNS bigint
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE result bigint;
BEGIN
  IF p_owner IS NULL OR p_owner = '00000000-0000-0000-0000-000000000000'
     OR p_duration IS NULL OR p_duration <= interval '0' OR NOT minidocs.lock_workspace(p_workspace)
  THEN RETURN NULL; END IF;
  UPDATE minidocs.provisioning_operations
  SET lease_owner = p_owner, lease_until = clock_timestamp() + p_duration,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_operation AND version = p_version
    AND status = 'recovery_required' AND (lease_until IS NULL OR lease_until <= clock_timestamp())
  RETURNING version INTO result;
  RETURN result;
END;
$$;

CREATE FUNCTION minidocs.command_complete_provisioning(p_workspace uuid, p_operation uuid, p_version bigint,
    p_owner uuid, p_application uuid) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE db_now timestamptz;
BEGIN
  IF p_application IS NULL OR p_application = '00000000-0000-0000-0000-000000000000'
     OR NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.workspaces WHERE id = p_workspace AND status = 'pending')
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
  IF NOT FOUND THEN RAISE EXCEPTION 'Workspace changed during provisioning'; END IF;
  RETURN true;
END;
$$;

ALTER TABLE minidocs.via_docs_accounts ADD CONSTRAINT ck_via_credentials CHECK (
  length(btrim(application_name)) > 0 AND octet_length(application_secret_protected) > 0
  AND id <> '00000000-0000-0000-0000-000000000000'
  AND (application_id IS NULL OR application_id <> '00000000-0000-0000-0000-000000000000'));

ALTER TABLE minidocs.provisioning_operations ADD CONSTRAINT ck_provisioning_lifecycle CHECK (
  (lease_owner IS NULL OR lease_owner <> '00000000-0000-0000-0000-000000000000')
  AND (candidate_application_id IS NULL OR candidate_application_id <> '00000000-0000-0000-0000-000000000000')
  AND CASE status
    WHEN 'pending_invitation' THEN attempt_no = 0 AND lease_owner IS NULL AND candidate_application_id IS NULL AND invitation_protected IS NULL
    WHEN 'ready' THEN lease_owner IS NULL AND candidate_application_id IS NULL AND coalesce(octet_length(invitation_protected), 0) > 0
    WHEN 'registering' THEN attempt_no > 0 AND lease_owner IS NOT NULL AND coalesce(octet_length(invitation_protected), 0) > 0
    WHEN 'recovery_required' THEN attempt_no > 0
    WHEN 'active' THEN attempt_no > 0 AND lease_owner IS NULL AND candidate_application_id IS NOT NULL
        AND invitation_protected IS NULL AND failure_category IS NULL
    WHEN 'cancelled' THEN lease_owner IS NULL AND candidate_application_id IS NULL AND invitation_protected IS NULL
    ELSE true
  END);

CREATE UNIQUE INDEX ux_provisioning_active_workspace ON minidocs.provisioning_operations(workspace_id)
WHERE status = 'active';

CREATE FUNCTION minidocs.assert_provisioning_complete(p_workspace uuid) RETURNS void
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM minidocs.workspaces w JOIN minidocs.via_docs_accounts acc ON acc.workspace_id = w.id
      WHERE w.id = p_workspace AND w.status = 'active' AND acc.application_id IS NOT NULL AND acc.verified_at IS NOT NULL)
     AND NOT EXISTS (
       SELECT 1 FROM minidocs.provisioning_operations op JOIN minidocs.via_docs_accounts acc
         ON acc.workspace_id = op.workspace_id AND acc.application_id = op.candidate_application_id
       WHERE op.workspace_id = p_workspace AND op.status = 'active' AND acc.verified_at IS NOT NULL
     )
  THEN
    RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_provisioning_complete',
      MESSAGE = 'An active workspace requires completed local provisioning for its verified account.';
  END IF;
END;
$$;

CREATE FUNCTION minidocs.check_provisioning_complete() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF TG_TABLE_NAME = 'workspaces' THEN
    PERFORM minidocs.assert_provisioning_complete(NEW.id);
  ELSIF TG_OP = 'DELETE' THEN
    PERFORM minidocs.assert_provisioning_complete(OLD.workspace_id);
  ELSE
    PERFORM minidocs.assert_provisioning_complete(NEW.workspace_id);
  END IF;
  RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER check_workspace_provisioning AFTER INSERT OR UPDATE OF status ON minidocs.workspaces
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_provisioning_complete();
CREATE CONSTRAINT TRIGGER check_operation_provisioning AFTER INSERT OR UPDATE OR DELETE ON minidocs.provisioning_operations
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_provisioning_complete();
CREATE CONSTRAINT TRIGGER check_account_provisioning AFTER INSERT OR UPDATE OR DELETE ON minidocs.via_docs_accounts
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_provisioning_complete();

DO $$
DECLARE workspace_id uuid;
BEGIN
  FOR workspace_id IN SELECT id FROM minidocs.workspaces WHERE status = 'active' LOOP
    PERFORM minidocs.assert_provisioning_complete(workspace_id);
  END LOOP;
END;
$$;
