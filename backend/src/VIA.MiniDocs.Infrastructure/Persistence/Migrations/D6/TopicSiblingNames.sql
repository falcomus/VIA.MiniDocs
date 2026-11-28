-- Preflight and install the invariant in the same migration transaction.
LOCK TABLE minidocs.topics IN ACCESS EXCLUSIVE MODE;
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM minidocs.topics WHERE parent_id IS NULL AND system_key IS NULL AND lower(btrim(name, U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') COLLATE pg_catalog."de-x-icu") IN ('nicht zugeordnet', 'alle zugeordnet'))
  THEN RAISE EXCEPTION 'Topic sibling names: existing root uses a reserved name'; END IF;
  IF EXISTS (SELECT 1 FROM minidocs.topics GROUP BY workspace_id, parent_id, lower(btrim(name, U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') COLLATE pg_catalog."de-x-icu") HAVING count(*) > 1)
  THEN RAISE EXCEPTION 'Topic sibling names: existing sibling names conflict'; END IF;
END;
$$;

CREATE UNIQUE INDEX ux_topics_sibling_name ON minidocs.topics
  (workspace_id, parent_id, (lower(btrim(name, U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') COLLATE pg_catalog."de-x-icu"))) NULLS NOT DISTINCT;
ALTER TABLE minidocs.topics ADD CONSTRAINT ck_topics_reserved_root_name
  CHECK (parent_id IS NOT NULL OR system_key IS NOT NULL OR NOT (lower(btrim(name, U&'\0009\000A\000B\000C\000D\0020\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000\FEFF') COLLATE pg_catalog."de-x-icu") IN ('nicht zugeordnet', 'alle zugeordnet')));

DO $$
BEGIN
  IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
    GRANT CREATE ON SCHEMA minidocs TO minidocs_command_owner;
    SET ROLE minidocs_command_owner;
  END IF;
END;
$$;
CREATE OR REPLACE FUNCTION minidocs.command_d6_topic(p_workspace uuid, p_id uuid, p_version bigint,
    p_name text, p_parent uuid, p_move boolean)
RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE current_topic minidocs.topics%ROWTYPE;
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
  PERFORM 1 FROM minidocs.topics WHERE workspace_id = p_workspace
    AND (id = p_id OR (p_move AND id = p_parent)) ORDER BY id FOR UPDATE;
  SELECT * INTO current_topic FROM minidocs.topics WHERE workspace_id = p_workspace
    AND id = p_id AND version = p_version AND system_key IS NULL;
  IF NOT FOUND THEN RETURN false; END IF;
  IF p_move THEN
    IF p_parent IS NOT NULL AND NOT EXISTS (SELECT 1 FROM minidocs.topics
        WHERE workspace_id = p_workspace AND id = p_parent AND system_key IS NULL) THEN RETURN false; END IF;
    IF EXISTS (
      WITH RECURSIVE parents(id, parent_id) AS (
        SELECT id, parent_id FROM minidocs.topics WHERE workspace_id = p_workspace AND id = p_parent
        UNION SELECT t.id, t.parent_id FROM minidocs.topics t JOIN parents p ON t.id = p.parent_id
          WHERE t.workspace_id = p_workspace
      ) SELECT 1 FROM parents WHERE id = p_id
    ) THEN RETURN false; END IF;
  END IF;
  -- Validate the final sibling name and parent in one write.
  UPDATE minidocs.topics SET name = coalesce(p_name, current_topic.name),
    parent_id = CASE WHEN p_move THEN p_parent ELSE current_topic.parent_id END,
    version = version + 1, updated_at = clock_timestamp()
  WHERE workspace_id = p_workspace AND id = p_id
    AND ((p_name IS NOT NULL AND p_name IS DISTINCT FROM current_topic.name)
      OR (p_move AND p_parent IS DISTINCT FROM current_topic.parent_id));
  RETURN true;
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
