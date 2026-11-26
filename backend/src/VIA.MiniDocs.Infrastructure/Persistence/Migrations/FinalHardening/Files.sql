-- File, purge and retention routines for the D2-06 migration.

CREATE OR REPLACE FUNCTION minidocs.command_begin_upload(p_workspace uuid, p_document uuid, p_operation uuid, p_owner uuid, p_duration interval, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  IF NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations AS op
  SET state = 'in_flight', lease_owner = p_owner,
      lease_until = clock_timestamp() + p_duration, attempt_no = attempt_no + 1,
      version = version + 1, updated_at = clock_timestamp()
  WHERE op.workspace_id = p_workspace AND op.document_id = p_document
    AND op.id = p_operation AND op.version = p_version
    AND op.kind = 'upload' AND op.state = 'prepared' AND op.attempt_no = 0
    AND op.remote_file_id IS NULL AND op.lease_owner IS NULL
    AND EXISTS (SELECT 1 FROM minidocs.documents d
        WHERE d.workspace_id = op.workspace_id AND d.id = op.document_id
          AND d.availability = 'pending_upload' AND d.deleted_at IS NULL);
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_record_upload_remote(p_workspace uuid, p_document uuid, p_operation uuid, p_remote uuid, p_version bigint, p_owner uuid) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_remote, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_remote, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET state = 'remote_known', remote_file_id = p_remote,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'in_flight' AND version = p_version
    AND lease_owner = p_owner AND lease_until > clock_timestamp()
    AND remote_file_id IS NULL;
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_record_upload_digest(p_workspace uuid, p_document uuid, p_operation uuid, p_size bigint, p_digest bytea, p_version bigint, p_owner uuid) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(octet_length(p_digest) = 32 AND p_size > 0, 'digest');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET verified_size = p_size, verified_sha256 = p_digest,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'remote_known' AND version = p_version
    AND lease_owner = p_owner AND lease_until > clock_timestamp()
    AND remote_file_id IS NOT NULL AND verified_sha256 IS NULL AND verified_size IS NULL;
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_reclaim_known_upload(p_workspace uuid, p_document uuid, p_operation uuid, p_owner uuid, p_duration interval, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET lease_owner = p_owner, lease_until = clock_timestamp() + p_duration,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'remote_known' AND version = p_version
    AND remote_file_id IS NOT NULL AND (lease_until IS NULL OR lease_until <= clock_timestamp());
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_recover_expired_upload(p_workspace uuid, p_document uuid, p_operation uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET state = 'unknown', lease_owner = NULL, lease_until = NULL,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'in_flight' AND version = p_version
    AND lease_until <= clock_timestamp();
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_trash(p_workspace uuid, p_document uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  IF NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.documents AS d
  SET deleted_at = clock_timestamp(), updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_document
    AND version = p_version AND deleted_at IS NULL
    AND availability IN ('available', 'missing')
    AND NOT EXISTS (SELECT 1 FROM minidocs.file_operations op
        WHERE op.workspace_id = d.workspace_id AND op.document_id = d.id
          AND op.state NOT IN ('completed','failed'));
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_restore(p_workspace uuid, p_document uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  IF NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN 0; END IF;
  UPDATE minidocs.documents AS d
  SET deleted_at = NULL, updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_document
    AND version = p_version AND deleted_at IS NOT NULL
    AND availability IN ('available', 'missing')
    AND NOT EXISTS (SELECT 1 FROM minidocs.file_operations op
        WHERE op.workspace_id = d.workspace_id AND op.document_id = d.id
          AND op.state NOT IN ('completed','failed'));
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_mark_delete_unknown(p_workspace uuid, p_document uuid, p_operation uuid, p_version bigint, p_owner uuid) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET state = 'delete_unknown', lease_owner = NULL, lease_until = NULL,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'delete' AND state = 'delete_in_flight'
    AND version = p_version AND lease_owner = p_owner
    AND lease_until > clock_timestamp();
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_recover_expired_delete(p_workspace uuid, p_document uuid, p_operation uuid, p_version bigint) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN 0; END IF;
  UPDATE minidocs.file_operations
  SET state = 'delete_unknown', lease_owner = NULL, lease_until = NULL,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'delete' AND state = 'delete_in_flight' AND version = p_version
    AND lease_until <= clock_timestamp();
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_retire_completed_uploads(p_workspace uuid, p_limit integer) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_limit BETWEEN 1 AND 1000, 'limit');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
  WITH candidates AS (
    SELECT id, workspace_id, request_id FROM minidocs.file_operations
    WHERE workspace_id = p_workspace AND kind = 'upload' AND state = 'completed'
      AND completed_at <= clock_timestamp() - interval '720 hours'
    ORDER BY completed_at, id LIMIT p_limit
  ), reserved AS (
    INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id)
    SELECT workspace_id, request_id FROM candidates
    RETURNING workspace_id, request_id
  )
  DELETE FROM minidocs.file_operations op USING candidates c, reserved r
  WHERE op.id = c.id AND op.workspace_id = c.workspace_id
    AND r.workspace_id = c.workspace_id AND r.request_id = c.request_id;
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_remove_expired_receipts(p_workspace uuid, p_limit integer) RETURNS integer
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE affected integer;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_limit BETWEEN 1 AND 1000, 'limit');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN 0; END IF;
  DELETE FROM minidocs.file_operation_receipts r WHERE r.workspace_id = p_workspace
    AND r.id IN (
      SELECT id FROM minidocs.file_operation_receipts WHERE workspace_id = p_workspace
        AND completed_at <= clock_timestamp() - interval '720 hours'
      ORDER BY completed_at, id LIMIT p_limit
    );
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_reserve_upload(p_workspace uuid, p_document uuid, p_topic uuid,
    p_operation uuid, p_request uuid, p_title text, p_kind text) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_topic, p_operation, p_request], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_topic, p_operation, p_request], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_kind IN ('meeting','note'), 'kind');
  p_title := regexp_replace(p_title, '^[[:space:]]+|[[:space:]]+$', '', 'g');
  PERFORM minidocs.require_command_argument(char_length(p_title) BETWEEN 1 AND 255, 'title');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  IF NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN false; END IF;
  IF EXISTS (SELECT 1 FROM minidocs.upload_request_tombstones WHERE workspace_id = p_workspace AND request_id = p_request)
     OR EXISTS (SELECT 1 FROM minidocs.file_operations WHERE workspace_id = p_workspace AND request_id = p_request)
     OR NOT EXISTS (SELECT 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_topic)
  THEN RETURN false; END IF;
  INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
  VALUES (p_document, p_workspace, p_topic, p_title, p_kind, 'pending_upload');
  INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id, attempt_no)
  VALUES (p_operation, p_workspace, p_document, 'upload', 'prepared', p_request, 0);
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_complete_upload(p_workspace uuid, p_document uuid, p_operation uuid,
    p_document_version bigint, p_version bigint, p_owner uuid, p_remote uuid, p_size bigint, p_digest bytea) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner, p_remote], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner, p_remote], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_document_version  > 0 AND p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(octet_length(p_digest) = 32 AND p_size > 0, 'digest');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN false; END IF;
  UPDATE minidocs.file_operations AS op
  SET state = 'completed', completed_at = clock_timestamp(), lease_owner = NULL, lease_until = NULL,
      version = version + 1, updated_at = clock_timestamp()
  WHERE op.workspace_id = p_workspace AND op.document_id = p_document AND op.id = p_operation
    AND op.kind = 'upload' AND op.state = 'remote_known' AND op.version = p_version
    AND op.lease_owner = p_owner AND op.lease_until > clock_timestamp()
    AND op.remote_file_id = p_remote AND op.verified_size = p_size
    AND op.verified_sha256 = p_digest
    AND EXISTS (SELECT 1 FROM minidocs.documents d
        WHERE d.workspace_id = op.workspace_id AND d.id = op.document_id
        AND d.version = p_document_version AND d.availability = 'pending_upload'
        AND d.deleted_at IS NULL AND d.via_file_id IS NULL);
  IF NOT FOUND THEN RETURN false; END IF;
  UPDATE minidocs.documents
  SET via_file_id = p_remote, availability = 'available',
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document
    AND availability = 'pending_upload' AND deleted_at IS NULL AND via_file_id IS NULL;
  IF NOT FOUND THEN RAISE EXCEPTION 'Document changed during upload completion'; END IF;
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_begin_purge(p_workspace uuid, p_document uuid, p_document_version bigint,
    p_operation uuid, p_owner uuid, p_duration interval) RETURNS uuid
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE p_remote uuid;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_document_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN NULL; END IF;
  IF NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN NULL; END IF;
  SELECT via_file_id INTO p_remote FROM minidocs.documents
  WHERE workspace_id = p_workspace AND id = p_document AND version = p_document_version
    AND deleted_at IS NOT NULL AND availability IN ('available','missing');
  IF p_remote IS NULL OR EXISTS (SELECT 1 FROM minidocs.file_operations
       WHERE workspace_id = p_workspace AND document_id = p_document AND state NOT IN ('completed','failed'))
     OR EXISTS (SELECT 1 FROM minidocs.file_operations WHERE id = p_operation)
     OR EXISTS (SELECT 1 FROM minidocs.file_operation_receipts WHERE operation_id = p_operation)
  THEN RETURN NULL; END IF;
  INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state,
      remote_file_id, attempt_no)
  VALUES (p_operation, p_workspace, p_document, 'delete', 'prepared', p_remote, 0);
  UPDATE minidocs.file_operations
  SET state = 'delete_in_flight', attempt_no = 1, lease_owner = p_owner,
      lease_until = clock_timestamp() + p_duration, version = version + 1,
      updated_at = clock_timestamp()
  WHERE id = p_operation AND workspace_id = p_workspace AND document_id = p_document
    AND state = 'prepared' AND version = 1;
  IF NOT FOUND THEN RAISE EXCEPTION 'Purge reservation disappeared'; END IF;
  UPDATE minidocs.documents SET availability = 'delete_pending',
      updated_at = clock_timestamp(), version = version + 1
  WHERE id = p_document AND workspace_id = p_workspace
    AND version = p_document_version;
  IF NOT FOUND THEN RAISE EXCEPTION 'Document changed during purge reservation'; END IF;
  RETURN p_remote;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_begin_delete_verification(p_workspace uuid, p_document uuid, p_operation uuid,
    p_version bigint, p_owner uuid, p_duration interval) RETURNS uuid
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE remote uuid;
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(minidocs.valid_lease_duration(p_duration), 'duration');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN NULL; END IF;
  UPDATE minidocs.file_operations AS op
  SET lease_owner = p_owner, lease_until = clock_timestamp() + p_duration,
      updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'delete' AND state = 'delete_unknown' AND version = p_version
    AND (lease_until IS NULL OR lease_until <= clock_timestamp())
    AND EXISTS (SELECT 1 FROM minidocs.documents d
        WHERE d.workspace_id = op.workspace_id AND d.id = op.document_id
          AND d.availability = 'delete_pending' AND d.deleted_at IS NOT NULL
          AND d.via_file_id = op.remote_file_id)
  RETURNING op.remote_file_id INTO remote;
  RETURN remote;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_complete_purge(p_workspace uuid, p_document uuid, p_operation uuid,
    p_remote uuid, p_version bigint, p_owner uuid, p_verification boolean) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE p_receipt uuid := gen_random_uuid();
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_remote, p_owner], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_remote, p_owner], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  PERFORM minidocs.require_command_argument(p_verification IS NOT NULL, 'verification');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN false; END IF;
  UPDATE minidocs.file_operations AS op
  SET state = 'completed', completed_at = clock_timestamp(),
      lease_owner = NULL, lease_until = NULL, version = version + 1,
      updated_at = clock_timestamp()
  WHERE op.workspace_id = p_workspace AND op.document_id = p_document
    AND op.id = p_operation AND op.kind = 'delete'
    AND ((p_verification AND op.state = 'delete_unknown')
      OR (NOT p_verification AND op.state = 'delete_in_flight'))
    AND op.version = p_version AND op.lease_owner = p_owner
    AND op.lease_until > clock_timestamp() AND op.remote_file_id = p_remote
    AND EXISTS (SELECT 1 FROM minidocs.documents d
        WHERE d.workspace_id = op.workspace_id AND d.id = op.document_id
          AND d.deleted_at IS NOT NULL AND d.availability = 'delete_pending'
          AND d.via_file_id = op.remote_file_id)
    AND NOT EXISTS (SELECT 1 FROM minidocs.file_operations other
        WHERE other.workspace_id = op.workspace_id AND other.document_id = op.document_id
          AND other.id <> op.id AND other.state NOT IN ('completed','failed'));
  IF NOT FOUND THEN RETURN false; END IF;
  INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id)
  SELECT workspace_id, request_id FROM minidocs.file_operations
  WHERE workspace_id = p_workspace AND document_id = p_document
    AND kind = 'upload' AND state IN ('completed','failed');
  INSERT INTO minidocs.file_operation_receipts(id, workspace_id, operation_id, kind, outcome, completed_at)
  SELECT p_receipt, workspace_id, id, 'delete', 'remote_deleted', completed_at
  FROM minidocs.file_operations WHERE workspace_id = p_workspace
    AND document_id = p_document AND id = p_operation;
  IF NOT FOUND THEN RAISE EXCEPTION 'Purge receipt could not be recorded'; END IF;
  DELETE FROM minidocs.transcript_pages
  WHERE workspace_id = p_workspace AND document_id = p_document;
  DELETE FROM minidocs.document_tags
  WHERE workspace_id = p_workspace AND document_id = p_document;
  DELETE FROM minidocs.file_operations
  WHERE workspace_id = p_workspace AND document_id = p_document
    AND state IN ('completed','failed');
  DELETE FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document;
  IF NOT FOUND THEN RAISE EXCEPTION 'Document changed during purge completion'; END IF;
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_fail_unstarted_upload(p_workspace uuid, p_document uuid, p_operation uuid,
    p_version bigint) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN false; END IF;
  UPDATE minidocs.file_operations SET state = 'failed', failure_category = 'not_sent',
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'prepared' AND version = p_version;
  IF NOT FOUND THEN RETURN false; END IF;
  UPDATE minidocs.documents SET availability = 'upload_failed', version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document AND availability = 'pending_upload' AND deleted_at IS NULL;
  IF NOT FOUND THEN RAISE EXCEPTION 'Document changed during upload failure'; END IF;
  RETURN true;
END;
$$;

CREATE OR REPLACE FUNCTION minidocs.command_reconcile_unknown_upload(p_workspace uuid, p_document uuid, p_operation uuid,
    p_version bigint, p_remote uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(array_position(ARRAY[p_workspace, p_document, p_operation, p_remote], NULL::uuid) IS NULL
    AND array_position(ARRAY[p_workspace, p_document, p_operation, p_remote], '00000000-0000-0000-0000-000000000000'::uuid) IS NULL, 'id');
  PERFORM minidocs.require_command_argument(p_version  > 0, 'version');
  IF NOT minidocs.lock_document(p_workspace, p_document) THEN RETURN false; END IF;
  UPDATE minidocs.file_operations SET state = 'remote_known', remote_file_id = p_remote,
      failure_category = NULL, version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND id = p_operation
    AND kind = 'upload' AND state = 'unknown' AND version = p_version;
  RETURN FOUND;
END;
$$;
