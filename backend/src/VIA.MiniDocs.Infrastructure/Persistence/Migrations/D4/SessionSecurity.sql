CREATE TABLE minidocs.identity_sessions (
  handle_hash bytea PRIMARY KEY CHECK (octet_length(handle_hash) = 32),
  user_id uuid NOT NULL REFERENCES minidocs.identity_users(id),
  security_stamp text NOT NULL,
  issued_at timestamptz NOT NULL,
  idle_until timestamptz NOT NULL,
  absolute_until timestamptz NOT NULL,
  revoked_at timestamptz,
  CONSTRAINT ck_identity_session_times CHECK (
    issued_at < idle_until AND idle_until <= absolute_until
    AND (revoked_at IS NULL OR revoked_at >= issued_at))
);
CREATE INDEX ix_identity_sessions_expiry ON minidocs.identity_sessions (idle_until, handle_hash);

CREATE TABLE minidocs.administrator_bootstrap (
  singleton boolean PRIMARY KEY CHECK (singleton),
  user_id uuid NOT NULL REFERENCES minidocs.identity_users(id),
  completed_at timestamptz NOT NULL
);

CREATE FUNCTION minidocs.command_create_session(
  p_handle bytea, p_user uuid, p_stamp text, p_idle interval, p_absolute interval)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_now timestamptz;
BEGIN
  PERFORM minidocs.require_command_argument(octet_length(p_handle) = 32 AND p_user IS NOT NULL
    AND length(p_stamp) > 0, 'session');
  PERFORM minidocs.require_command_argument(p_idle > interval '0' AND p_idle <= interval '30 minutes' AND
    extract(year FROM p_idle) = 0 AND extract(month FROM p_idle) = 0 AND extract(day FROM p_idle) = 0
    AND extract(year FROM p_absolute) = 0 AND extract(month FROM p_absolute) = 0 AND extract(day FROM p_absolute) = 0
    AND p_idle <= p_absolute AND p_absolute <= interval '8 hours', 'lifetime');
  PERFORM 1 FROM minidocs.identity_users WHERE id = p_user FOR SHARE;
  v_now := clock_timestamp();
  IF NOT EXISTS (SELECT 1 FROM minidocs.identity_users WHERE id = p_user
      AND NOT is_disabled AND security_stamp = p_stamp
      AND (NOT lockout_enabled OR lockout_end IS NULL OR lockout_end <= v_now))
  THEN RETURN false; END IF;
  INSERT INTO minidocs.identity_sessions (handle_hash, user_id, security_stamp, issued_at, idle_until, absolute_until)
    VALUES (p_handle, p_user, p_stamp, v_now, v_now + p_idle, v_now + p_absolute);
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_renew_session(p_handle bytea, p_user uuid, p_idle interval)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_now timestamptz;
BEGIN
  PERFORM minidocs.require_command_argument(octet_length(p_handle) = 32 AND p_user IS NOT NULL, 'session');
  PERFORM minidocs.require_command_argument(p_idle > interval '0' AND p_idle <= interval '30 minutes' AND
    extract(year FROM p_idle) = 0 AND extract(month FROM p_idle) = 0 AND extract(day FROM p_idle) = 0, 'lifetime');
  PERFORM 1 FROM minidocs.identity_sessions WHERE handle_hash = p_handle AND user_id = p_user FOR UPDATE;
  v_now := clock_timestamp();
  UPDATE minidocs.identity_sessions SET idle_until = least(absolute_until, v_now + p_idle)
    WHERE handle_hash = p_handle AND user_id = p_user AND revoked_at IS NULL
      AND idle_until > v_now AND absolute_until > idle_until
      AND idle_until <= v_now + p_idle / 2;
  RETURN FOUND;
END;
$$;

CREATE FUNCTION minidocs.command_revoke_session(p_handle bytea, p_user uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(octet_length(p_handle) = 32 AND p_user IS NOT NULL, 'session');
  UPDATE minidocs.identity_sessions SET revoked_at = greatest(issued_at, clock_timestamp())
    WHERE handle_hash = p_handle AND user_id = p_user AND revoked_at IS NULL;
  RETURN FOUND;
END;
$$;

CREATE FUNCTION minidocs.command_cleanup_sessions(p_batch integer)
RETURNS integer LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_cutoff timestamptz := clock_timestamp();
  v_count integer;
BEGIN
  PERFORM minidocs.require_command_argument(p_batch BETWEEN 1 AND 500, 'batch');
  WITH expired AS (
    SELECT handle_hash FROM minidocs.identity_sessions WHERE idle_until <= v_cutoff
    ORDER BY idle_until, handle_hash LIMIT p_batch FOR UPDATE SKIP LOCKED
  )
  DELETE FROM minidocs.identity_sessions s USING expired e WHERE s.handle_hash = e.handle_hash;
  GET DIAGNOSTICS v_count = ROW_COUNT;
  RETURN v_count;
END;
$$;

-- The caller keeps these locks through its local mutation. The actor's
-- authority is linearized after the session lock, even when actor = target.
CREATE FUNCTION minidocs.lock_admin_authority(
  p_actor uuid, p_stamp text, p_handle bytea, p_target uuid, p_workspace uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_user uuid;
BEGIN
  PERFORM minidocs.require_command_argument(p_actor IS NOT NULL AND length(p_stamp) > 0
    AND octet_length(p_handle) = 32, 'actor');
  PERFORM pg_advisory_xact_lock(3841, 401);
  IF p_workspace IS NOT NULL THEN
    IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  END IF;
  FOR v_user IN SELECT id FROM minidocs.identity_users
    WHERE id IN (p_actor, p_target) ORDER BY id
  LOOP
    IF v_user = p_target THEN
      PERFORM 1 FROM minidocs.identity_users WHERE id = v_user FOR NO KEY UPDATE;
    ELSE
      PERFORM 1 FROM minidocs.identity_users WHERE id = v_user FOR SHARE;
    END IF;
  END LOOP;
  PERFORM 1 FROM minidocs.identity_sessions WHERE handle_hash = p_handle AND user_id = p_actor FOR SHARE;
  RETURN EXISTS (SELECT 1 FROM minidocs.identity_sessions
    WHERE handle_hash = p_handle AND user_id = p_actor AND security_stamp = p_stamp
      AND revoked_at IS NULL AND idle_until > clock_timestamp())
    AND EXISTS (SELECT 1 FROM minidocs.identity_users WHERE id = p_actor AND security_stamp = p_stamp)
    AND minidocs.is_enabled_administrator(p_actor);
END;
$$;

CREATE FUNCTION minidocs.command_set_account_disabled(
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
  RETURN 0;
END;
$$;

CREATE FUNCTION minidocs.command_bootstrap_administrator(
  p_user uuid, p_name text, p_normalized text, p_hash text)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  v_role uuid := gen_random_uuid();
BEGIN
  IF session_user <> 'minidocs_bootstrap' THEN RAISE insufficient_privilege; END IF;
  PERFORM pg_advisory_xact_lock(3841, 401);
  -- A pre-existing administrator role or AdminOnly account needs operator
  -- reconciliation. Ordinary D2 accounts are left untouched.
  IF EXISTS (SELECT 1 FROM minidocs.administrator_bootstrap)
     OR EXISTS (SELECT 1 FROM minidocs.identity_roles WHERE normalized_name = 'ADMINISTRATOR')
     OR EXISTS (SELECT 1 FROM minidocs.identity_users
         WHERE account_kind = 'admin_only' OR normalized_user_name = p_normalized)
  THEN RETURN false; END IF;
  INSERT INTO minidocs.identity_users (id, account_kind, is_disabled, user_name, normalized_user_name,
    email_confirmed, password_hash, security_stamp, concurrency_stamp, phone_number_confirmed,
    two_factor_enabled, lockout_enabled, access_failed_count)
    VALUES (p_user, 'admin_only', false, p_name, p_normalized, false, p_hash,
      gen_random_uuid()::text, gen_random_uuid()::text, false, false, true, 0);
  INSERT INTO minidocs.identity_roles (id, name, normalized_name, concurrency_stamp)
    VALUES (v_role, 'Administrator', 'ADMINISTRATOR', gen_random_uuid()::text);
  INSERT INTO minidocs.identity_user_roles (user_id, role_id) VALUES (p_user, v_role);
  INSERT INTO minidocs.administrator_bootstrap (singleton, user_id, completed_at)
    VALUES (true, p_user, clock_timestamp());
  RETURN true;
END;
$$;

REVOKE ALL ON FUNCTION
  minidocs.command_create_session(bytea, uuid, text, interval, interval),
  minidocs.command_renew_session(bytea, uuid, interval),
  minidocs.command_revoke_session(bytea, uuid),
  minidocs.command_cleanup_sessions(integer),
  minidocs.lock_admin_authority(uuid, text, bytea, uuid, uuid),
  minidocs.command_set_account_disabled(uuid, text, bytea, uuid, text, text[], boolean),
  minidocs.command_bootstrap_administrator(uuid, text, text, text)
FROM PUBLIC;

DO $$
BEGIN
  IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
    IF to_regrole('minidocs_bootstrap') IS NULL THEN
      RAISE EXCEPTION 'Prepare the isolated D4 bootstrap principal before migration.';
    END IF;
    GRANT SELECT ON minidocs.identity_sessions TO minidocs_runtime;
    GRANT SELECT, INSERT, DELETE ON minidocs.identity_sessions TO minidocs_command_owner;
    GRANT UPDATE (idle_until, revoked_at) ON minidocs.identity_sessions TO minidocs_command_owner;
    GRANT UPDATE (is_disabled, security_stamp, concurrency_stamp) ON minidocs.identity_users TO minidocs_command_owner;
    GRANT SELECT, INSERT ON minidocs.administrator_bootstrap TO minidocs_command_owner;
    GRANT INSERT ON minidocs.identity_roles, minidocs.identity_user_roles TO minidocs_command_owner;
    GRANT INSERT (id, account_kind, is_disabled, user_name, normalized_user_name, email_confirmed,
      password_hash, security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled,
      lockout_enabled, access_failed_count) ON minidocs.identity_users TO minidocs_command_owner;
    GRANT CREATE ON SCHEMA minidocs TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_create_session(bytea, uuid, text, interval, interval) OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_renew_session(bytea, uuid, interval) OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_revoke_session(bytea, uuid) OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_cleanup_sessions(integer) OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.lock_admin_authority(uuid, text, bytea, uuid, uuid) OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_set_account_disabled(uuid, text, bytea, uuid, text, text[], boolean)
      OWNER TO minidocs_command_owner;
    ALTER FUNCTION minidocs.command_bootstrap_administrator(uuid, text, text, text) OWNER TO minidocs_command_owner;
    REVOKE CREATE ON SCHEMA minidocs FROM minidocs_command_owner;
    SET LOCAL ROLE minidocs_command_owner;
    GRANT EXECUTE ON FUNCTION minidocs.command_create_session(bytea, uuid, text, interval, interval),
      minidocs.command_renew_session(bytea, uuid, interval), minidocs.command_revoke_session(bytea, uuid),
      minidocs.command_cleanup_sessions(integer), minidocs.lock_admin_authority(uuid, text, bytea, uuid, uuid),
      minidocs.command_set_account_disabled(uuid, text, bytea, uuid, text, text[], boolean) TO minidocs_runtime;
  END IF;
  IF to_regrole('minidocs_bootstrap') IS NOT NULL THEN
    GRANT EXECUTE ON FUNCTION minidocs.command_bootstrap_administrator(uuid, text, text, text) TO minidocs_bootstrap;
  END IF;
  RESET ROLE;
END;
$$;
