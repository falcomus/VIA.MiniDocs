namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

/// <summary>Database-only invariants that cannot be represented by an EF model or a row CHECK.</summary>
internal static class D2IntegritySql
{
  // The workspace guard deliberately writes the same version value: it creates a real
  // row-write conflict even at REPEATABLE READ, without changing the domain version.
  // Application transactions must still acquire the workspace lock before child rows.
  internal const string Up = """
      CREATE FUNCTION minidocs.serialize_workspace(target uuid) RETURNS void
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        UPDATE minidocs.workspaces SET version = version WHERE id = target;
      END;
      $$;

      CREATE FUNCTION minidocs.guard_identity_user() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_user_deletion_protected',
            MESSAGE = 'User deletion requires a separate administrative workflow.';
        END IF;
        IF NEW.id IS DISTINCT FROM OLD.id THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_user_identity_immutable',
            MESSAGE = 'User identity is immutable.';
        END IF;
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_identity_user BEFORE UPDATE OR DELETE ON minidocs.identity_users
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_identity_user();

      CREATE FUNCTION minidocs.guard_workspace_owner() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_deletion_protected',
            MESSAGE = 'Workspace deletion requires a separate administrative workflow.';
        ELSIF TG_OP = 'UPDATE' THEN
          IF NEW.id IS DISTINCT FROM OLD.id OR NEW.owner_user_id IS DISTINCT FROM OLD.owner_user_id THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_owner_immutable',
              MESSAGE = 'Workspace identity and owner are immutable.';
          END IF;
        ELSE
          -- Serialize workspace creation with a concurrent account-kind change.
          UPDATE minidocs.identity_users SET account_kind = account_kind WHERE id = NEW.owner_user_id;
        END IF;
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_workspace_owner BEFORE INSERT OR UPDATE OR DELETE ON minidocs.workspaces
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_workspace_owner();

      CREATE FUNCTION minidocs.assert_user_workspace(target uuid) RETURNS void
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      DECLARE kind text;
      BEGIN
        SELECT account_kind INTO kind FROM minidocs.identity_users WHERE id = target;
        IF (kind = 'regular' AND NOT EXISTS (SELECT 1 FROM minidocs.workspaces WHERE owner_user_id = target))
           OR (kind = 'admin_only' AND EXISTS (SELECT 1 FROM minidocs.workspaces WHERE owner_user_id = target)) THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_user_workspace_cardinality',
            MESSAGE = 'Regular users require exactly one workspace; admin-only users require none.';
        END IF;
      END;
      $$;
      CREATE FUNCTION minidocs.check_user_workspace() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        PERFORM minidocs.assert_user_workspace(NEW.id);
        RETURN NULL;
      END;
      $$;
      CREATE CONSTRAINT TRIGGER check_user_workspace AFTER INSERT OR UPDATE OF account_kind ON minidocs.identity_users
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_user_workspace();

      CREATE FUNCTION minidocs.assert_workspace_integrity(target uuid) RETURNS void
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      DECLARE current_workspace minidocs.workspaces%ROWTYPE;
      BEGIN
        SELECT * INTO current_workspace FROM minidocs.workspaces WHERE id = target;
        IF NOT FOUND THEN RETURN; END IF;
        PERFORM minidocs.assert_user_workspace(current_workspace.owner_user_id);
        IF NOT EXISTS (SELECT 1 FROM minidocs.topics WHERE workspace_id = target AND system_key = 'UNASSIGNED') THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_system_topic',
            MESSAGE = 'A workspace requires an UNASSIGNED system topic.';
        END IF;
        IF current_workspace.status = 'active' AND NOT EXISTS (
          SELECT 1 FROM minidocs.via_docs_accounts WHERE workspace_id = target
            AND application_id IS NOT NULL AND verified_at IS NOT NULL
        ) THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_verified_account',
            MESSAGE = 'An active workspace requires a verified technical account.';
        END IF;
      END;
      $$;
      CREATE FUNCTION minidocs.check_workspace_integrity() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        PERFORM minidocs.assert_workspace_integrity(NEW.id);
        RETURN NULL;
      END;
      $$;
      CREATE CONSTRAINT TRIGGER check_workspace_integrity AFTER INSERT OR UPDATE OF status ON minidocs.workspaces
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_workspace_integrity();

      CREATE FUNCTION minidocs.guard_workspace_record() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          PERFORM minidocs.serialize_workspace(OLD.workspace_id);
          RETURN OLD;
        END IF;
        IF TG_OP = 'UPDATE' THEN
          IF NEW.workspace_id IS DISTINCT FROM OLD.workspace_id OR NEW.id IS DISTINCT FROM OLD.id THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_record_identity',
              MESSAGE = 'Workspace-scoped record identity is immutable.';
          END IF;
        END IF;
        PERFORM minidocs.serialize_workspace(NEW.workspace_id);
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_via_docs_account BEFORE INSERT OR UPDATE OR DELETE ON minidocs.via_docs_accounts
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_workspace_record();
      CREATE TRIGGER guard_document BEFORE INSERT OR DELETE OR UPDATE OF workspace_id, id, topic_id, availability, deleted_at
        ON minidocs.documents FOR EACH ROW EXECUTE FUNCTION minidocs.guard_workspace_record();
      CREATE TRIGGER guard_provisioning_operation BEFORE INSERT OR UPDATE OR DELETE ON minidocs.provisioning_operations
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_workspace_record();
      CREATE FUNCTION minidocs.check_account_workspace() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          PERFORM minidocs.assert_workspace_integrity(OLD.workspace_id);
        ELSE
          PERFORM minidocs.assert_workspace_integrity(NEW.workspace_id);
        END IF;
        RETURN NULL;
      END;
      $$;
      CREATE CONSTRAINT TRIGGER check_account_workspace AFTER INSERT OR UPDATE OR DELETE ON minidocs.via_docs_accounts
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_account_workspace();

      CREATE FUNCTION minidocs.guard_topic() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          PERFORM minidocs.serialize_workspace(OLD.workspace_id);
          IF OLD.system_key IS NOT NULL THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_topic_system_protected',
              MESSAGE = 'The system topic cannot be deleted.';
          END IF;
          RETURN OLD;
        END IF;
        IF TG_OP = 'UPDATE' THEN
          IF NEW.workspace_id IS DISTINCT FROM OLD.workspace_id OR NEW.id IS DISTINCT FROM OLD.id THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_workspace_record_identity',
              MESSAGE = 'Topic identity is immutable.';
          END IF;
          IF OLD.system_key IS NOT NULL AND (NEW.name IS DISTINCT FROM OLD.name
             OR NEW.parent_id IS DISTINCT FROM OLD.parent_id OR NEW.system_key IS DISTINCT FROM OLD.system_key) THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_topic_system_protected',
              MESSAGE = 'The system topic cannot be renamed, moved or reclassified.';
          END IF;
          IF NEW.system_key IS DISTINCT FROM OLD.system_key THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_topic_system_protected',
              MESSAGE = 'Topic system classification is immutable.';
          END IF;
        END IF;
        PERFORM minidocs.serialize_workspace(NEW.workspace_id);
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_topic BEFORE INSERT OR UPDATE OR DELETE ON minidocs.topics
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_topic();
      CREATE FUNCTION minidocs.check_topic_tree() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      DECLARE current_topic minidocs.topics%ROWTYPE;
              cyclic boolean;
              below_system boolean;
      BEGIN
        -- Read the final transaction state; a multi-row UPDATE must not bypass the cycle check.
        SELECT * INTO current_topic FROM minidocs.topics WHERE id = NEW.id;
        IF NOT FOUND THEN RETURN NULL; END IF;
        WITH RECURSIVE ancestors(id, parent_id, system_key) AS (
          SELECT id, parent_id, system_key FROM minidocs.topics
            WHERE workspace_id = current_topic.workspace_id AND id = current_topic.parent_id
          UNION
          SELECT t.id, t.parent_id, t.system_key FROM minidocs.topics t
            JOIN ancestors a ON t.id = a.parent_id WHERE t.workspace_id = current_topic.workspace_id
        )
        SELECT coalesce(bool_or(id = current_topic.id), false), coalesce(bool_or(system_key IS NOT NULL), false)
          INTO cyclic, below_system FROM ancestors;
        IF cyclic THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_topic_tree_acyclic',
            MESSAGE = 'A topic hierarchy cannot contain a cycle.';
        END IF;
        IF below_system THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_topic_no_system_children',
            MESSAGE = 'The system topic cannot have child topics.';
        END IF;
        RETURN NULL;
      END;
      $$;
      CREATE CONSTRAINT TRIGGER check_topic_tree AFTER INSERT OR UPDATE OF parent_id ON minidocs.topics
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_topic_tree();

      CREATE FUNCTION minidocs.guard_file_operation() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          PERFORM minidocs.serialize_workspace(OLD.workspace_id);
          IF OLD.state NOT IN ('completed', 'failed') THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_file_operation_unresolved',
              MESSAGE = 'An unresolved file operation cannot be removed.';
          END IF;
          RETURN OLD;
        END IF;
        IF TG_OP = 'UPDATE' THEN
          IF NEW.workspace_id IS DISTINCT FROM OLD.workspace_id OR NEW.id IS DISTINCT FROM OLD.id
             OR NEW.document_id IS DISTINCT FROM OLD.document_id OR NEW.kind IS DISTINCT FROM OLD.kind
             OR NEW.request_id IS DISTINCT FROM OLD.request_id THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_file_operation_identity',
              MESSAGE = 'File operation identity and request reservation are immutable.';
          END IF;
        END IF;
        PERFORM minidocs.serialize_workspace(NEW.workspace_id);
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_file_operation BEFORE INSERT OR UPDATE OR DELETE ON minidocs.file_operations
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_file_operation();
      CREATE FUNCTION minidocs.guard_upload_tombstone() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP <> 'INSERT' THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_upload_tombstone_permanent',
            MESSAGE = 'An upload request tombstone is permanent for the workspace lifetime.';
        END IF;
        PERFORM minidocs.serialize_workspace(NEW.workspace_id);
        RETURN NEW;
      END;
      $$;
      CREATE TRIGGER guard_upload_tombstone BEFORE INSERT OR UPDATE OR DELETE ON minidocs.upload_request_tombstones
        FOR EACH ROW EXECUTE FUNCTION minidocs.guard_upload_tombstone();
      CREATE FUNCTION minidocs.assert_upload_reservation(target_workspace uuid, target_request uuid) RETURNS void
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF EXISTS (SELECT 1 FROM minidocs.upload_request_tombstones
                     WHERE workspace_id = target_workspace AND request_id = target_request)
           AND EXISTS (SELECT 1 FROM minidocs.file_operations
                     WHERE workspace_id = target_workspace AND request_id = target_request) THEN
          RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_upload_request_exclusive',
            MESSAGE = 'An upload request cannot be both reserved and retired.';
        END IF;
      END;
      $$;
      CREATE FUNCTION minidocs.check_upload_reservation() RETURNS trigger
      LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
      BEGIN
        IF TG_OP = 'DELETE' THEN
          IF OLD.kind = 'upload' AND NOT EXISTS (SELECT 1 FROM minidocs.upload_request_tombstones
              WHERE workspace_id = OLD.workspace_id AND request_id = OLD.request_id) THEN
            RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_upload_request_retained',
              MESSAGE = 'Removing an upload operation requires a permanent request tombstone.';
          END IF;
        ELSE
          PERFORM minidocs.assert_upload_reservation(NEW.workspace_id, NEW.request_id);
        END IF;
        RETURN NULL;
      END;
      $$;
      CREATE CONSTRAINT TRIGGER check_file_upload_reservation AFTER INSERT OR UPDATE OR DELETE ON minidocs.file_operations
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_upload_reservation();
      CREATE CONSTRAINT TRIGGER check_upload_tombstone AFTER INSERT ON minidocs.upload_request_tombstones
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION minidocs.check_upload_reservation();
      """;

  internal const string Down = """
      DROP TRIGGER check_upload_tombstone ON minidocs.upload_request_tombstones;
      DROP TRIGGER check_file_upload_reservation ON minidocs.file_operations;
      DROP TRIGGER guard_upload_tombstone ON minidocs.upload_request_tombstones;
      DROP TRIGGER guard_file_operation ON minidocs.file_operations;
      DROP TRIGGER check_topic_tree ON minidocs.topics;
      DROP TRIGGER guard_topic ON minidocs.topics;
      DROP TRIGGER check_account_workspace ON minidocs.via_docs_accounts;
      DROP TRIGGER guard_provisioning_operation ON minidocs.provisioning_operations;
      DROP TRIGGER guard_document ON minidocs.documents;
      DROP TRIGGER guard_via_docs_account ON minidocs.via_docs_accounts;
      DROP TRIGGER check_workspace_integrity ON minidocs.workspaces;
      DROP TRIGGER check_user_workspace ON minidocs.identity_users;
      DROP TRIGGER guard_workspace_owner ON minidocs.workspaces;
      DROP TRIGGER guard_identity_user ON minidocs.identity_users;
      DROP FUNCTION minidocs.check_upload_reservation();
      DROP FUNCTION minidocs.assert_upload_reservation(uuid, uuid);
      DROP FUNCTION minidocs.guard_upload_tombstone();
      DROP FUNCTION minidocs.guard_file_operation();
      DROP FUNCTION minidocs.check_topic_tree();
      DROP FUNCTION minidocs.guard_topic();
      DROP FUNCTION minidocs.check_account_workspace();
      DROP FUNCTION minidocs.guard_workspace_record();
      DROP FUNCTION minidocs.check_workspace_integrity();
      DROP FUNCTION minidocs.assert_workspace_integrity(uuid);
      DROP FUNCTION minidocs.check_user_workspace();
      DROP FUNCTION minidocs.assert_user_workspace(uuid);
      DROP FUNCTION minidocs.guard_workspace_owner();
      DROP FUNCTION minidocs.guard_identity_user();
      DROP FUNCTION minidocs.serialize_workspace(uuid);
      """;
}
