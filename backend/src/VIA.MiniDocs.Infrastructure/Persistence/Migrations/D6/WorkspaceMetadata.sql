-- D6 metadata normalization v1: ECMAScript trim character set (FEFF included, NEL excluded).
-- Keep the HTTP boundary and frontend parity tests aligned; direct commands require canonical names.
DO $$
BEGIN
  IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
    GRANT CREATE ON SCHEMA minidocs TO minidocs_command_owner;
    GRANT INSERT (id, workspace_id, name, name_key, description, color),
      UPDATE (name, name_key, description, color, updated_at) ON minidocs.tags TO minidocs_command_owner;
    SET ROLE minidocs_command_owner;
  END IF;
END;
$$;

CREATE FUNCTION minidocs.command_d6_lock(p_workspace uuid)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(p_workspace IS NOT NULL AND p_workspace <> '00000000-0000-0000-0000-000000000000'::uuid, 'workspace');
  IF NOT minidocs.lock_workspace(p_workspace) THEN RETURN false; END IF;
  RETURN minidocs.workspace_accepts_new_work(p_workspace);
END;
$$;

CREATE FUNCTION minidocs.command_d6_topic(p_workspace uuid, p_id uuid, p_version bigint,
    p_name text, p_parent uuid, p_move boolean)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(p_id IS NOT NULL AND p_id <> '00000000-0000-0000-0000-000000000000'::uuid, 'id');
  PERFORM minidocs.require_command_argument(p_version IS NULL OR p_version > 0, 'version');
  PERFORM minidocs.require_command_argument(p_version IS NOT NULL OR p_name IS NOT NULL, 'name');
  PERFORM minidocs.require_command_argument(p_move IS NOT NULL, 'move');
  PERFORM minidocs.require_command_argument(p_parent IS NULL OR p_parent <> '00000000-0000-0000-0000-000000000000'::uuid, 'parent');
  IF NOT minidocs.command_d6_lock(p_workspace) THEN RETURN false; END IF;
  IF p_name IS NOT NULL AND (p_name <> btrim(p_name,
        U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') OR char_length(p_name) NOT BETWEEN 1 AND 255)
  THEN RETURN false; END IF;
  IF p_version IS NULL THEN
    IF p_name IS NULL OR (p_parent IS NOT NULL AND NOT EXISTS (SELECT 1 FROM minidocs.topics
        WHERE workspace_id = p_workspace AND id = p_parent AND system_key IS NULL))
    THEN RETURN false; END IF;
    INSERT INTO minidocs.topics(id, workspace_id, name, parent_id) VALUES (p_id, p_workspace, p_name, p_parent);
    RETURN true;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_id
      AND version = p_version AND system_key IS NULL) THEN RETURN false; END IF;
  IF p_move AND NOT minidocs.command_move_topic(p_workspace, p_id, p_version, p_parent)
  THEN RETURN false; END IF;
  IF p_name IS NOT NULL THEN
    UPDATE minidocs.topics SET name = p_name, version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_id AND name IS DISTINCT FROM p_name;
  END IF;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_d6_tag(p_workspace uuid, p_id uuid, p_version bigint,
    p_name text, p_description text, p_color text)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE normalized text;
BEGIN
  PERFORM minidocs.require_command_argument(p_id IS NOT NULL AND p_id <> '00000000-0000-0000-0000-000000000000'::uuid, 'id');
  PERFORM minidocs.require_command_argument(p_version IS NULL OR p_version > 0, 'version');
  PERFORM minidocs.require_command_argument(p_name IS NOT NULL AND p_description IS NOT NULL AND p_color IS NOT NULL, 'tag fields');
  IF NOT minidocs.command_d6_lock(p_workspace) THEN RETURN false; END IF;
  IF p_name IS NULL OR p_name <> btrim(p_name,
        U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') OR char_length(p_name) NOT BETWEEN 1 AND 100 OR
      p_description IS NULL OR char_length(p_description) > 1000 OR p_color IS NULL OR
      p_color !~ '^#[0-9A-Fa-f]{6}$' THEN RETURN false; END IF;
  normalized := lower(p_name COLLATE pg_catalog."de-x-icu");
  IF EXISTS (SELECT 1 FROM minidocs.tags WHERE workspace_id = p_workspace AND name_key = normalized AND id <> p_id)
  THEN RETURN false; END IF;
  IF p_version IS NULL THEN
    INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color)
    VALUES (p_id, p_workspace, p_name, normalized, p_description, p_color);
    RETURN true;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.tags WHERE workspace_id = p_workspace AND id = p_id AND version = p_version)
  THEN RETURN false; END IF;
  UPDATE minidocs.tags SET name = p_name, name_key = normalized, description = p_description, color = p_color,
    version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_id
      AND (name, name_key, description, color) IS DISTINCT FROM (p_name, normalized, p_description, p_color);
  IF FOUND THEN
    UPDATE minidocs.documents d SET version = d.version + 1, updated_at = clock_timestamp()
    WHERE d.workspace_id = p_workspace AND EXISTS (SELECT 1 FROM minidocs.document_tags link
      WHERE link.workspace_id = p_workspace AND link.document_id = d.id AND link.tag_id = p_id);
  END IF;
  RETURN true;
END;
$$;

CREATE FUNCTION minidocs.command_d6_document(p_workspace uuid, p_id uuid, p_version bigint,
    p_title text, p_kind text, p_date date, p_set_date boolean)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  PERFORM minidocs.require_command_argument(p_id IS NOT NULL AND p_id <> '00000000-0000-0000-0000-000000000000'::uuid, 'id');
  PERFORM minidocs.require_command_argument(p_version > 0, 'version');
  PERFORM minidocs.require_command_argument(p_set_date IS NOT NULL, 'set_date');
  IF NOT minidocs.command_d6_lock(p_workspace) THEN RETURN false; END IF;
  IF (p_title IS NOT NULL AND (p_title <> btrim(p_title,
        U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') OR char_length(p_title) NOT BETWEEN 1 AND 255))
      OR (p_kind IS NOT NULL AND p_kind NOT IN ('meeting','note')) THEN RETURN false; END IF;
  IF NOT EXISTS (SELECT 1 FROM minidocs.documents WHERE workspace_id = p_workspace AND id = p_id
      AND version = p_version AND deleted_at IS NULL AND availability <> 'delete_pending')
  THEN RETURN false; END IF;
  UPDATE minidocs.documents SET title = coalesce(p_title, title), kind = coalesce(p_kind, kind),
    document_date = CASE WHEN p_set_date THEN p_date ELSE document_date END,
    version = version + 1, updated_at = clock_timestamp()
    WHERE workspace_id = p_workspace AND id = p_id AND (title, kind, document_date) IS DISTINCT FROM
      (coalesce(p_title, title), coalesce(p_kind, kind), CASE WHEN p_set_date THEN p_date ELSE document_date END);
  RETURN true;
END;
$$;

REVOKE ALL ON FUNCTION minidocs.command_d6_lock(uuid),
  minidocs.command_d6_topic(uuid, uuid, bigint, text, uuid, boolean),
  minidocs.command_d6_tag(uuid, uuid, bigint, text, text, text),
  minidocs.command_d6_document(uuid, uuid, bigint, text, text, date, boolean) FROM PUBLIC;
DO $$
BEGIN
  IF to_regrole('minidocs_runtime') IS NOT NULL THEN
    GRANT EXECUTE ON FUNCTION minidocs.command_d6_lock(uuid),
      minidocs.command_d6_topic(uuid, uuid, bigint, text, uuid, boolean),
      minidocs.command_d6_tag(uuid, uuid, bigint, text, text, text),
      minidocs.command_d6_document(uuid, uuid, bigint, text, text, date, boolean) TO minidocs_runtime;
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
