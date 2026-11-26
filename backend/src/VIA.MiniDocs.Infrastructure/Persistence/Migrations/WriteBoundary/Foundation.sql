-- A real workspace-row update invalidates snapshots predating another writer.
-- The domain version is unchanged by lock acquisition alone.
CREATE FUNCTION minidocs.lock_workspace(p_workspace uuid) RETURNS boolean
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  UPDATE minidocs.workspaces SET version = version WHERE id = p_workspace;
  RETURN FOUND;
END;
$$;

CREATE FUNCTION minidocs.lock_document(p_workspace uuid, p_document uuid) RETURNS boolean
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document FOR UPDATE;
  RETURN FOUND;
END;
$$;

REVOKE ALL ON FUNCTION minidocs.lock_workspace(uuid), minidocs.lock_document(uuid, uuid) FROM PUBLIC;

CREATE FUNCTION minidocs.workspace_accepts_new_work(p_workspace uuid) RETURNS boolean
LANGUAGE sql STABLE SET search_path = pg_catalog, pg_temp AS $$
  SELECT EXISTS (SELECT 1 FROM minidocs.workspaces w
    JOIN minidocs.identity_users u ON u.id = w.owner_user_id
    WHERE w.id = p_workspace AND w.status = 'active' AND NOT u.is_disabled);
$$;

DO $$
DECLARE table_name text;
BEGIN
  FOREACH table_name IN ARRAY ARRAY['workspaces','user_invitations','via_docs_accounts',
    'provisioning_operations','topics','documents','tags','transcript_pages',
    'pending_transcripts','pending_transcript_pages','file_operations']
  LOOP
    EXECUTE format('ALTER TABLE minidocs.%I ADD CONSTRAINT %I CHECK (version > 0)',
        table_name, 'ck_' || table_name || '_positive_version');
  END LOOP;
END;
$$;

CREATE FUNCTION minidocs.check_runtime_identity_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF current_user = 'minidocs_runtime' AND (NEW.account_kind <> 'regular' OR NEW.is_disabled)
  THEN RAISE insufficient_privilege USING MESSAGE = 'Runtime can create only regular enabled Identity users'; END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER check_runtime_identity_insert BEFORE INSERT ON minidocs.identity_users
FOR EACH ROW EXECUTE FUNCTION minidocs.check_runtime_identity_insert();
