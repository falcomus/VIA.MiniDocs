-- Apply atomically with the function definitions. Superuser-only fixtures have no application roles.
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA minidocs FROM PUBLIC;
DO $$
DECLARE command text;
DECLARE signature text;
DECLARE column_list text;
DECLARE relation record;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'minidocs_runtime') THEN RETURN; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'minidocs_command_owner' AND NOT rolcanlogin)
  THEN RAISE EXCEPTION 'Prepare the dedicated MiniDocs roles before migrating'; END IF;

  REVOKE ALL ON ALL TABLES IN SCHEMA minidocs FROM PUBLIC, minidocs_runtime;
  REVOKE ALL ON ALL SEQUENCES IN SCHEMA minidocs FROM PUBLIC, minidocs_runtime;
  REVOKE ALL ON ALL FUNCTIONS IN SCHEMA minidocs FROM minidocs_runtime;
  REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM minidocs_runtime, PUBLIC;

  -- A table-level REVOKE does not remove independently granted column privileges.
  FOR relation IN SELECT c.oid, n.nspname, c.relname FROM pg_catalog.pg_class c
    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'minidocs' AND c.relkind IN ('r','p')
  LOOP
    SELECT string_agg(format('%I', attname), ', ' ORDER BY attnum) INTO column_list
    FROM pg_catalog.pg_attribute WHERE attrelid = relation.oid AND attnum > 0 AND NOT attisdropped;
    IF column_list IS NOT NULL THEN
      EXECUTE format('REVOKE ALL PRIVILEGES (%s) ON %I.%I FROM PUBLIC, minidocs_runtime',
          column_list, relation.nspname, relation.relname);
    END IF;
  END LOOP;

  GRANT SELECT ON minidocs.identity_users, minidocs.identity_roles, minidocs.identity_role_claims,
      minidocs.identity_user_roles, minidocs.identity_user_claims, minidocs.identity_user_logins,
      minidocs.identity_user_tokens, minidocs.workspaces,
      minidocs.via_docs_accounts, minidocs.provisioning_operations, minidocs.topics, minidocs.documents,
      minidocs.tags, minidocs.document_tags, minidocs.transcript_pages, minidocs.pending_transcripts,
      minidocs.pending_transcript_pages, minidocs.file_operations, minidocs.file_operation_receipts,
      minidocs.upload_request_tombstones TO minidocs_runtime;
  GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO minidocs_runtime;
  GRANT SELECT (id, created_by, created_at, updated_at, expires_at, revoked_at, redeemed_at, version)
    ON minidocs.user_invitations TO minidocs_runtime;

  GRANT INSERT (id, account_kind, is_disabled, user_name, normalized_user_name, email, normalized_email,
      email_confirmed, password_hash, security_stamp, concurrency_stamp, phone_number,
      phone_number_confirmed, two_factor_enabled, lockout_end, lockout_enabled, access_failed_count)
      ON minidocs.identity_users TO minidocs_runtime;
  GRANT UPDATE (user_name, normalized_user_name, email, normalized_email, email_confirmed,
      password_hash, security_stamp, concurrency_stamp, phone_number, phone_number_confirmed,
      two_factor_enabled, lockout_end, lockout_enabled, access_failed_count)
      ON minidocs.identity_users TO minidocs_runtime;
  GRANT INSERT, UPDATE, DELETE ON minidocs.identity_user_tokens TO minidocs_runtime;

  GRANT USAGE, CREATE ON SCHEMA minidocs TO minidocs_command_owner;
  GRANT SELECT ON minidocs.identity_users, minidocs.identity_roles, minidocs.identity_user_roles,
    minidocs.workspaces, minidocs.user_invitations,
    minidocs.via_docs_accounts, minidocs.provisioning_operations, minidocs.topics, minidocs.documents,
    minidocs.transcript_pages, minidocs.document_tags, minidocs.tags, minidocs.pending_transcripts,
    minidocs.pending_transcript_pages, minidocs.file_operations,
    minidocs.file_operation_receipts, minidocs.upload_request_tombstones TO minidocs_command_owner;
  -- The initial workspace owner guard performs a same-value account-kind update for serialization.
  GRANT UPDATE (account_kind) ON minidocs.identity_users TO minidocs_command_owner;
  GRANT INSERT, UPDATE ON minidocs.workspaces, minidocs.user_invitations,
    minidocs.via_docs_accounts, minidocs.provisioning_operations TO minidocs_command_owner;
  GRANT INSERT ON minidocs.upload_request_tombstones TO minidocs_command_owner;
  GRANT INSERT, UPDATE, DELETE ON minidocs.topics TO minidocs_command_owner;
  GRANT UPDATE (version) ON minidocs.tags TO minidocs_command_owner;
  GRANT DELETE ON minidocs.tags TO minidocs_command_owner;
  GRANT UPDATE (version, status, updated_at) ON minidocs.pending_transcripts TO minidocs_command_owner;
  GRANT INSERT, UPDATE, DELETE ON minidocs.documents, minidocs.transcript_pages, minidocs.file_operations
    TO minidocs_command_owner;
  GRANT INSERT, DELETE ON minidocs.file_operation_receipts TO minidocs_command_owner;
  GRANT INSERT, DELETE ON minidocs.document_tags TO minidocs_command_owner;
  GRANT EXECUTE ON FUNCTION minidocs.serialize_workspace(uuid), minidocs.lock_workspace(uuid),
    minidocs.lock_document(uuid, uuid), minidocs.assert_user_workspace(uuid),
    minidocs.workspace_accepts_new_work(uuid), minidocs.is_enabled_administrator(uuid),
    minidocs.assert_workspace_integrity(uuid), minidocs.assert_upload_reservation(uuid, uuid),
    minidocs.assert_provisioning_complete(uuid), minidocs.assert_transcript_integrity(uuid, uuid)
    TO minidocs_command_owner;

  FOREACH signature IN ARRAY ARRAY[
    'minidocs.command_move_topic(uuid, uuid, bigint, uuid)',
    'minidocs.command_delete_topic(uuid, uuid, bigint)',
    'minidocs.command_move_document(uuid, uuid, bigint, uuid)',
    'minidocs.command_set_document_tag(uuid, uuid, bigint, uuid, boolean)',
    'minidocs.command_delete_tag(uuid, uuid, bigint)',
    'minidocs.command_assign_pending_transcript(uuid, uuid, bigint, uuid, bigint, boolean)',
    'minidocs.command_begin_provisioning(uuid, uuid, uuid, interval, bigint)',
    'minidocs.command_record_provisioning_candidate(uuid, uuid, uuid, bigint, uuid)',
    'minidocs.command_recover_expired_provisioning(uuid, uuid, bigint)',
    'minidocs.command_register_workspace(bytea, uuid, uuid, uuid)',
    'minidocs.command_revoke_invitation(uuid, bigint, uuid)',
    'minidocs.command_lock_valid_invitation(bytea)',
    'minidocs.command_create_invitation(uuid, bytea, uuid, timestamptz)',
    'minidocs.command_prepare_provisioning(uuid, uuid, uuid, text, bytea, bytea)',
    'minidocs.command_claim_provisioning_verification(uuid, uuid, bigint, uuid, interval)',
    'minidocs.command_complete_provisioning(uuid, uuid, bigint, uuid, uuid)',
    'minidocs.command_replace_transcript(uuid, uuid, bigint, boolean, boolean, integer[], text[], boolean[])',
    'minidocs.command_edit_transcript_page(uuid, uuid, bigint, integer, bigint, text)',
    'minidocs.command_change_page_count(uuid, uuid, bigint, integer, text)',
    'minidocs.command_rename_document(uuid, uuid, bigint, text)',
    'minidocs.command_begin_upload(uuid, uuid, uuid, uuid, interval, bigint)',
    'minidocs.command_record_upload_remote(uuid, uuid, uuid, uuid, bigint, uuid)',
    'minidocs.command_record_upload_digest(uuid, uuid, uuid, bigint, bytea, bigint, uuid)',
    'minidocs.command_reclaim_known_upload(uuid, uuid, uuid, uuid, interval, bigint)',
    'minidocs.command_recover_expired_upload(uuid, uuid, uuid, bigint)',
    'minidocs.command_trash(uuid, uuid, bigint)',
    'minidocs.command_restore(uuid, uuid, bigint)',
    'minidocs.command_mark_delete_unknown(uuid, uuid, uuid, bigint, uuid)',
    'minidocs.command_recover_expired_delete(uuid, uuid, uuid, bigint)',
    'minidocs.command_retire_completed_uploads(uuid, integer)',
    'minidocs.command_remove_expired_receipts(uuid, integer)',
    'minidocs.command_reserve_upload(uuid, uuid, uuid, uuid, uuid, text, text)',
    'minidocs.command_complete_upload(uuid, uuid, uuid, bigint, bigint, uuid, uuid, bigint, bytea)',
    'minidocs.command_fail_unstarted_upload(uuid, uuid, uuid, bigint)',
    'minidocs.command_reconcile_unknown_upload(uuid, uuid, uuid, bigint, uuid)',
    'minidocs.command_begin_purge(uuid, uuid, bigint, uuid, uuid, interval)',
    'minidocs.command_begin_delete_verification(uuid, uuid, uuid, bigint, uuid, interval)',
    'minidocs.command_complete_purge(uuid, uuid, uuid, uuid, bigint, uuid, boolean)'
  ] LOOP
    -- These are fixed qualified signatures from the migration, never caller input.
    EXECUTE format('GRANT EXECUTE ON FUNCTION %s TO minidocs_runtime', signature);
    EXECUTE format('ALTER FUNCTION %s OWNER TO minidocs_command_owner', signature);
  END LOOP;
  ALTER FUNCTION minidocs.command_redeem_invitation(bytea, uuid) OWNER TO minidocs_command_owner;

  -- Deferred check triggers run after the command's security context has been restored.
  -- Their fixed bodies only check data. Runtime cannot execute them directly.
  FOREACH command IN ARRAY ARRAY['check_user_workspace', 'check_workspace_integrity',
      'check_account_workspace', 'check_upload_reservation', 'check_provisioning_complete',
      'check_transcript_integrity']
  LOOP
    EXECUTE format('ALTER FUNCTION minidocs.%I() SECURITY DEFINER', command);
    EXECUTE format('ALTER FUNCTION minidocs.%I() OWNER TO minidocs_command_owner', command);
  END LOOP;
  REVOKE CREATE ON SCHEMA minidocs FROM minidocs_command_owner;
END;
$$;
