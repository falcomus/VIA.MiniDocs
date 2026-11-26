CREATE FUNCTION minidocs.command_replace_transcript(p_workspace uuid, p_document uuid, p_revision bigint,
    p_present boolean, p_edited boolean, p_numbers integer[], p_texts text[], p_edits boolean[]) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE doc minidocs.documents%ROWTYPE;
DECLARE removed integer; changed integer; added integer;
BEGIN
  IF p_numbers IS NULL OR p_texts IS NULL OR p_edits IS NULL OR p_present IS NULL OR p_edited IS NULL
     OR cardinality(p_numbers) <> cardinality(p_texts) OR cardinality(p_numbers) <> cardinality(p_edits)
     OR cardinality(p_numbers) > 10000 OR (NOT p_present AND (p_edited OR cardinality(p_numbers) > 0))
     OR EXISTS (SELECT 1 FROM unnest(p_numbers, p_texts, p_edits) AS v(n, t, e)
          WHERE n IS NULL OR n < 1 OR t IS NULL OR e IS NULL OR octet_length(t) > 1048576)
     OR (SELECT count(DISTINCT n) FROM unnest(p_numbers) AS v(n)) <> cardinality(p_numbers)
  THEN RAISE EXCEPTION USING ERRCODE = '22023', MESSAGE = 'Invalid transcript replacement'; END IF;
  IF NOT minidocs.lock_document(p_workspace, p_document) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  SELECT * INTO STRICT doc FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document;
  IF doc.transcript_revision IS DISTINCT FROM p_revision OR doc.deleted_at IS NOT NULL
     OR doc.availability = 'delete_pending' THEN RETURN false; END IF;
  IF EXISTS (SELECT 1 FROM unnest(p_numbers) AS v(n) WHERE n > coalesce(doc.page_count, 1))
  THEN RAISE EXCEPTION USING ERRCODE = '22023', MESSAGE = 'Transcript page exceeds document page range'; END IF;

  DELETE FROM minidocs.transcript_pages
  WHERE workspace_id = p_workspace AND document_id = p_document AND NOT (page_no = ANY(p_numbers));
  GET DIAGNOSTICS removed = ROW_COUNT;

  UPDATE minidocs.transcript_pages AS page
  SET text = v.t, edited = v.e, version = version + 1, updated_at = clock_timestamp()
  FROM unnest(p_numbers, p_texts, p_edits) AS v(n, t, e)
  WHERE page.workspace_id = p_workspace AND page.document_id = p_document AND page.page_no = v.n
    AND (page.text, page.edited) IS DISTINCT FROM (v.t, v.e);
  GET DIAGNOSTICS changed = ROW_COUNT;

  INSERT INTO minidocs.transcript_pages(workspace_id, document_id, page_no, text, edited)
  SELECT p_workspace, p_document, v.n, v.t, v.e FROM unnest(p_numbers, p_texts, p_edits) AS v(n, t, e)
  WHERE NOT EXISTS (SELECT 1 FROM minidocs.transcript_pages page
     WHERE page.workspace_id = p_workspace AND page.document_id = p_document AND page.page_no = v.n)
  ORDER BY v.n;
  GET DIAGNOSTICS added = ROW_COUNT;

  IF removed + changed + added > 0 OR (doc.transcript_present, doc.transcript_edited) IS DISTINCT FROM (p_present, p_edited) THEN
    UPDATE minidocs.documents SET transcript_present = p_present, transcript_edited = p_edited,
        transcript_revision = transcript_revision + 1, version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_document;
    IF NOT FOUND THEN RAISE EXCEPTION 'Document disappeared during transcript replacement'; END IF;
  END IF;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_edit_transcript_page(p_workspace uuid, p_document uuid, p_revision bigint,
    p_page integer, p_version bigint, p_text text) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE old_page minidocs.transcript_pages%ROWTYPE;
BEGIN
  IF p_page IS NULL OR p_page < 1 OR p_text IS NULL OR octet_length(p_text) > 1048576
     OR NOT minidocs.lock_document(p_workspace, p_document)
     OR NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
    AND transcript_revision = p_revision AND deleted_at IS NULL AND availability <> 'delete_pending'
    AND p_page <= coalesce(page_count, 1)) THEN RETURN false; END IF;

  SELECT * INTO old_page FROM minidocs.transcript_pages WHERE workspace_id = p_workspace
    AND document_id = p_document AND page_no = p_page AND version = p_version;
  IF NOT FOUND THEN RETURN false; END IF;
  IF old_page.edited AND old_page.text = p_text THEN RETURN true; END IF;
  UPDATE minidocs.transcript_pages SET text = p_text, edited = true, version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND document_id = p_document AND page_no = p_page;
  UPDATE minidocs.documents SET transcript_present = true, transcript_edited = true,
      transcript_revision = transcript_revision + 1, version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_change_page_count(p_workspace uuid, p_document uuid, p_version bigint,
    p_count integer, p_source text) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_document(p_workspace, p_document) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  IF (p_count IS NULL) <> (p_source IS NULL) OR p_count NOT BETWEEN 1 AND 100000
     OR p_source NOT IN ('viewer','pdf_import','via_docs_verified')
     OR EXISTS (SELECT 1 FROM minidocs.transcript_pages WHERE workspace_id = p_workspace
          AND document_id = p_document AND page_no > coalesce(p_count, 1)) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
         AND version = p_version AND deleted_at IS NULL AND availability <> 'delete_pending') THEN RETURN false; END IF;
  UPDATE minidocs.documents SET page_count = p_count, page_count_source = p_source,
      version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document
    AND (page_count, page_count_source) IS DISTINCT FROM (p_count, p_source);
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_rename_document(p_workspace uuid, p_document uuid, p_version bigint, p_title text)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF p_title IS NULL OR length(btrim(p_title)) NOT BETWEEN 1 AND 255
     OR NOT minidocs.lock_document(p_workspace, p_document)
     OR NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
         AND version = p_version AND deleted_at IS NULL AND availability <> 'delete_pending') THEN RETURN false; END IF;
  UPDATE minidocs.documents SET title = p_title, version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document AND title IS DISTINCT FROM p_title;
  RETURN true;
END;
$$;


CREATE FUNCTION minidocs.command_move_topic(p_workspace uuid, p_topic uuid, p_version bigint, p_parent uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id IN (p_topic, p_parent)
  ORDER BY id FOR UPDATE;
  IF p_parent IS NOT NULL AND NOT EXISTS (SELECT 1 FROM minidocs.topics
      WHERE workspace_id = p_workspace AND id = p_parent AND system_key IS NULL) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_topic
      AND system_key IS NULL AND version = p_version) THEN RETURN false; END IF;
  IF EXISTS (
    WITH RECURSIVE parents(id, parent_id) AS (
      SELECT id, parent_id FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_parent
      UNION SELECT t.id, t.parent_id FROM minidocs.topics t JOIN parents p ON t.id = p.parent_id
        WHERE t.workspace_id = p_workspace
    ) SELECT 1 FROM parents WHERE id = p_topic
  ) THEN RETURN false; END IF;
  UPDATE minidocs.topics SET parent_id = p_parent, updated_at = clock_timestamp(), version = version + 1
  WHERE workspace_id = p_workspace AND id = p_topic AND parent_id IS DISTINCT FROM p_parent;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_delete_topic(p_workspace uuid, p_topic uuid, p_version bigint)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  DELETE FROM minidocs.topics t
  WHERE t.workspace_id = p_workspace AND t.id = p_topic AND t.version = p_version AND t.system_key IS NULL
    AND NOT EXISTS (SELECT 1 FROM minidocs.topics child WHERE child.workspace_id = p_workspace AND child.parent_id = p_topic)
    AND NOT EXISTS (SELECT 1 FROM minidocs.documents doc WHERE doc.workspace_id = p_workspace AND doc.topic_id = p_topic);
  RETURN FOUND;
END;
$$;

CREATE FUNCTION minidocs.command_move_document(p_workspace uuid, p_document uuid, p_version bigint, p_topic uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE prior_topic uuid;
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  SELECT topic_id INTO prior_topic FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
    AND version = p_version AND deleted_at IS NULL AND availability IN ('available','missing');
  IF NOT FOUND OR NOT EXISTS (SELECT 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_topic)
  THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id IN (prior_topic,p_topic) ORDER BY id FOR UPDATE;
  IF EXISTS (SELECT 1 FROM minidocs.file_operations WHERE workspace_id = p_workspace
       AND document_id = p_document AND state NOT IN ('completed','failed')) THEN RETURN false; END IF;
  UPDATE minidocs.documents SET topic_id = p_topic, version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_document AND topic_id IS DISTINCT FROM p_topic;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_set_document_tag(p_workspace uuid, p_document uuid, p_version bigint,
    p_tag uuid, p_present boolean) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE changed boolean;
BEGIN
  IF p_present IS NULL OR NOT minidocs.lock_workspace(p_workspace)
     OR NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.tags WHERE workspace_id = p_workspace AND id = p_tag FOR UPDATE;
  IF NOT FOUND THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
    AND version = p_version AND deleted_at IS NULL AND availability <> 'delete_pending' FOR UPDATE;
  IF NOT FOUND THEN RETURN false; END IF;
  IF p_present THEN
    INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
    VALUES (p_workspace, p_document, p_tag) ON CONFLICT DO NOTHING;
    changed := FOUND;
  ELSE
    DELETE FROM minidocs.document_tags WHERE workspace_id = p_workspace AND document_id = p_document AND tag_id = p_tag;
    changed := FOUND;
  END IF;
  IF changed THEN
    UPDATE minidocs.documents SET version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_document;
  END IF;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_delete_tag(p_workspace uuid, p_tag uuid, p_version bigint)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NOT minidocs.lock_workspace(p_workspace) OR NOT minidocs.workspace_accepts_new_work(p_workspace)
  THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.tags WHERE workspace_id = p_workspace AND id = p_tag AND version = p_version FOR UPDATE;
  IF NOT FOUND THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.documents d JOIN minidocs.document_tags link
     ON link.workspace_id = d.workspace_id AND link.document_id = d.id
  WHERE link.workspace_id = p_workspace AND link.tag_id = p_tag ORDER BY d.id FOR UPDATE OF d;
  WITH removed AS (
    DELETE FROM minidocs.document_tags WHERE workspace_id = p_workspace AND tag_id = p_tag RETURNING document_id
  )
  UPDATE minidocs.documents d SET version = version + 1, updated_at = clock_timestamp()
  FROM removed WHERE d.workspace_id = p_workspace AND d.id = removed.document_id;
  DELETE FROM minidocs.tags WHERE workspace_id = p_workspace AND id = p_tag;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_assign_pending_transcript(p_workspace uuid, p_document uuid, p_revision bigint,
    p_pending uuid, p_pending_version bigint, p_replace boolean) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE numbers integer[]; DECLARE texts text[]; DECLARE edits boolean[];
BEGIN
  IF p_replace IS NULL OR NOT minidocs.lock_document(p_workspace, p_document)
     OR NOT minidocs.workspace_accepts_new_work(p_workspace) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_document
      AND (p_replace OR NOT transcript_present)) THEN RETURN false; END IF;
  PERFORM 1 FROM minidocs.pending_transcripts WHERE workspace_id = p_workspace AND id = p_pending
    AND version = p_pending_version AND status = 'pending' FOR UPDATE;
  IF NOT FOUND THEN RETURN false; END IF;
  SELECT coalesce(array_agg(page_no ORDER BY page_no), ARRAY[]::integer[]),
    coalesce(array_agg(text ORDER BY page_no), ARRAY[]::text[]),
    coalesce(array_agg(false ORDER BY page_no), ARRAY[]::boolean[])
  INTO numbers, texts, edits FROM minidocs.pending_transcript_pages
  WHERE workspace_id = p_workspace AND pending_id = p_pending;
  IF NOT minidocs.command_replace_transcript(p_workspace, p_document, p_revision, true, false, numbers, texts, edits)
  THEN RETURN false; END IF;
  UPDATE minidocs.pending_transcripts SET status = 'matched', version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_pending;
  IF NOT FOUND THEN RAISE EXCEPTION 'Pending transcript changed during assignment'; END IF;
  RETURN true;
END;
$$;

ALTER TABLE minidocs.documents ADD CONSTRAINT ck_document_transcript_flags
CHECK (transcript_present OR NOT transcript_edited);
ALTER TABLE minidocs.tags ADD CONSTRAINT ck_tag_name_key CHECK (length(btrim(name_key)) > 0);

CREATE FUNCTION minidocs.assert_transcript_integrity(p_workspace uuid, p_document uuid) RETURNS void
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM minidocs.documents doc JOIN minidocs.transcript_pages page
    ON page.workspace_id = doc.workspace_id AND page.document_id = doc.id
    WHERE doc.workspace_id = p_workspace AND doc.id = p_document
      AND (NOT doc.transcript_present OR page.page_no > coalesce(doc.page_count, 1))) THEN
    RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_transcript_document_consistency',
      MESSAGE = 'Transcript pages must belong to a present transcript within the document page range.';
  END IF;
END;
$$;

CREATE FUNCTION minidocs.check_transcript_integrity() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF TG_TABLE_NAME = 'documents' THEN
    PERFORM minidocs.assert_transcript_integrity(NEW.workspace_id, NEW.id);
  ELSE
    PERFORM minidocs.assert_transcript_integrity(NEW.workspace_id, NEW.document_id);
  END IF;
  RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER check_document_transcript AFTER INSERT OR UPDATE OF page_count, transcript_present
  ON minidocs.documents DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION minidocs.check_transcript_integrity();
CREATE CONSTRAINT TRIGGER check_transcript_document AFTER INSERT OR UPDATE ON minidocs.transcript_pages
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_transcript_integrity();

DO $$
DECLARE doc record;
BEGIN
  FOR doc IN SELECT workspace_id, id FROM minidocs.documents LOOP
    PERFORM minidocs.assert_transcript_integrity(doc.workspace_id, doc.id);
  END LOOP;
END;
$$;
