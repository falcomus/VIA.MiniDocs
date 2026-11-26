-- Preconditions belong to the SQL write boundary, including direct calls.
CREATE FUNCTION minidocs.require_command_argument(p_valid boolean, p_argument text) RETURNS void
LANGUAGE plpgsql IMMUTABLE SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF p_valid IS DISTINCT FROM true THEN
    RAISE EXCEPTION USING ERRCODE = '22023', CONSTRAINT = 'ck_command_argument',
      MESSAGE = 'Invalid command argument', DETAIL = p_argument;
  END IF;
END;
$$;

CREATE FUNCTION minidocs.valid_lease_duration(p_duration interval) RETURNS boolean
LANGUAGE sql IMMUTABLE SET search_path = pg_catalog, pg_temp AS $$
  SELECT isfinite(p_duration) AND extract(year FROM p_duration) = 0
    AND extract(month FROM p_duration) = 0 AND extract(day FROM p_duration) = 0
    AND p_duration > interval '0 seconds' AND p_duration <= minidocs.maximum_lease_duration();
$$;

-- Call only after acquiring the workspace write lock, in a separate SQL expression.
-- Lock the owner row as well so repeatable-read callers cannot use an old owner snapshot.
CREATE OR REPLACE FUNCTION minidocs.workspace_accepts_new_work(p_workspace uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SET search_path = pg_catalog, pg_temp AS $$
DECLARE allowed boolean;
BEGIN
  SELECT w.status = 'active' AND NOT u.is_disabled INTO allowed
  FROM minidocs.workspaces w JOIN minidocs.identity_users u ON u.id = w.owner_user_id
  WHERE w.id = p_workspace FOR SHARE OF u;
  RETURN coalesce(allowed, false);
END;
$$;

CREATE FUNCTION minidocs.workspace_accepts_provisioning(p_workspace uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SET search_path = pg_catalog, pg_temp AS $$
DECLARE allowed boolean;
BEGIN
  SELECT w.status = 'pending' AND NOT u.is_disabled INTO allowed
  FROM minidocs.workspaces w JOIN minidocs.identity_users u ON u.id = w.owner_user_id
  WHERE w.id = p_workspace FOR SHARE OF u;
  RETURN coalesce(allowed, false);
END;
$$;

REVOKE ALL ON FUNCTION minidocs.require_command_argument(boolean, text), minidocs.valid_lease_duration(interval),
  minidocs.workspace_accepts_provisioning(uuid), minidocs.maximum_lease_duration() FROM PUBLIC;
