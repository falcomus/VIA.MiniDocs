-- The prepared identity must survive an unknown remote result unchanged.
CREATE FUNCTION minidocs.guard_prepared_technical_identity() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF NEW.application_name IS DISTINCT FROM OLD.application_name
     OR NEW.application_secret_protected IS DISTINCT FROM OLD.application_secret_protected
     OR (OLD.application_id IS NOT NULL AND
        (NEW.application_id, NEW.verified_at) IS DISTINCT FROM (OLD.application_id, OLD.verified_at))
  THEN
    RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_prepared_technical_identity',
      MESSAGE = 'Prepared technical identity cannot be reassigned.';
  END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER guard_prepared_technical_identity
BEFORE UPDATE ON minidocs.via_docs_accounts FOR EACH ROW
EXECUTE FUNCTION minidocs.guard_prepared_technical_identity();

CREATE FUNCTION minidocs.guard_provisioning_transition() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
BEGIN
  IF (NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
        ('pending_invitation','ready'), ('pending_invitation','cancelled'),
        ('ready','registering'), ('ready','cancelled'), ('registering','recovery_required'),
        ('registering','active'), ('recovery_required','active')))
     OR (OLD.candidate_application_id IS NOT NULL AND
         NEW.candidate_application_id IS DISTINCT FROM OLD.candidate_application_id)
     OR NEW.attempt_no <> OLD.attempt_no +
          (CASE WHEN (OLD.status, NEW.status) = ('ready','registering') THEN 1 ELSE 0 END)
     OR (OLD.status IN ('active','cancelled') AND NEW IS DISTINCT FROM OLD)
  THEN
    RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_provisioning_status_transition',
      MESSAGE = 'Invalid provisioning transition.';
  END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER guard_provisioning_transition
BEFORE UPDATE ON minidocs.provisioning_operations FOR EACH ROW
EXECUTE FUNCTION minidocs.guard_provisioning_transition();

-- A legacy excessive lease aborts the upgrade. Expiring it on our own would
-- change the authority of a worker that might have sent an external request.
ALTER TABLE minidocs.file_operations ADD CONSTRAINT ck_file_lease_maximum CHECK (
  lease_until IS NULL OR (isfinite(lease_until)
    AND lease_until <= updated_at + minidocs.maximum_lease_duration()));
ALTER TABLE minidocs.provisioning_operations ADD CONSTRAINT ck_provisioning_lease_maximum CHECK (
  lease_until IS NULL OR (isfinite(lease_until)
    AND lease_until <= updated_at + minidocs.maximum_lease_duration()));

-- Remove the duplicate payload left by an already matched import on D2-05.
DELETE FROM minidocs.pending_transcript_pages AS page USING minidocs.pending_transcripts AS head
WHERE page.workspace_id = head.workspace_id AND page.pending_id = head.id AND head.status <> 'pending';
UPDATE minidocs.pending_transcripts
SET source_name = NULL, meta_date = NULL, meta_topic = NULL, meta_title = NULL, meta_tags = NULL,
    version = version + 1, updated_at = clock_timestamp()
WHERE status <> 'pending' AND (source_name IS NOT NULL OR meta_date IS NOT NULL OR
  meta_topic IS NOT NULL OR meta_title IS NOT NULL OR meta_tags IS NOT NULL);

ALTER TABLE minidocs.pending_transcripts ADD CONSTRAINT ck_pending_terminal_metadata CHECK (
  status = 'pending' OR (source_name IS NULL AND meta_date IS NULL AND meta_topic IS NULL
    AND meta_title IS NULL AND meta_tags IS NULL));

CREATE FUNCTION minidocs.check_pending_payload() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
DECLARE candidate uuid;
BEGIN
  IF TG_TABLE_NAME = 'pending_transcripts' THEN candidate := NEW.id;
  ELSE candidate := NEW.pending_id; END IF;
  IF EXISTS (SELECT 1 FROM minidocs.pending_transcripts h JOIN minidocs.pending_transcript_pages p
    ON p.workspace_id = h.workspace_id AND p.pending_id = h.id
    WHERE h.workspace_id = NEW.workspace_id AND h.id = candidate AND h.status <> 'pending')
  THEN
    RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'ck_pending_terminal_pages',
      MESSAGE = 'A terminal pending transcript cannot retain pages.';
  END IF;
  RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER check_pending_head_payload
AFTER INSERT OR UPDATE OF status ON minidocs.pending_transcripts DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION minidocs.check_pending_payload();
CREATE CONSTRAINT TRIGGER check_pending_page_payload
AFTER INSERT OR UPDATE ON minidocs.pending_transcript_pages DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION minidocs.check_pending_payload();

REVOKE ALL ON FUNCTION minidocs.check_pending_payload(), minidocs.guard_provisioning_transition(),
  minidocs.guard_prepared_technical_identity() FROM PUBLIC;
