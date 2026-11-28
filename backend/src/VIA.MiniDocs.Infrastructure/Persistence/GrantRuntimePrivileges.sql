-- MiniDocs deployment and restore gate. Catalog reads and migration-history read only.
-- This authored inventory comes from the checked-in migrations. Never use the
-- target database to establish expected rights or object identities.
DO $gate$
DECLARE
  runtime oid := pg_catalog.to_regrole('minidocs_runtime');
  writer oid := pg_catalog.to_regrole('minidocs_command_owner');
  migrator oid := pg_catalog.to_regrole('minidocs_migrator');
  bootstrap oid := pg_catalog.to_regrole('minidocs_bootstrap');
  protected_roles oid[] := ARRAY[runtime, writer, migrator];
  readers oid[] := ARRAY[runtime, writer];
  d4 boolean;
  d4c boolean;
  d6 boolean;
  sibling_names boolean;
  expected record;
  item record;
  role_id oid;
  object_id oid;
  right_name text;
  wanted boolean;
  column_rights text;
  table_count integer := 0;
  function_count integer := 0;
  trigger_count integer := 0;
BEGIN
  PERFORM pg_catalog.set_config('search_path', 'pg_catalog, pg_temp', true);
  SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261127090000_SessionSecurity') INTO d4;
  SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261127100000_TechnicalProvisioning') INTO d4c;
  SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261128100000_WorkspaceMetadata') INTO d6;
  SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261128110000_TopicSiblingNames') INTO sibling_names;
  IF d4 AND bootstrap IS NULL THEN RAISE EXCEPTION 'MiniDocs gate: missing bootstrap principal'; END IF;
  IF bootstrap IS NOT NULL THEN
    protected_roles := array_append(protected_roles, bootstrap);
    readers := array_append(readers, bootstrap);
  END IF;
  IF current_setting('server_version_num')::integer NOT BETWEEN 180000 AND 189999 THEN
    RAISE EXCEPTION 'MiniDocs gate: PostgreSQL 18 required';
  END IF;
  IF array_position(protected_roles, NULL::oid) IS NOT NULL OR EXISTS (
    SELECT 1 FROM pg_roles r WHERE r.oid = ANY(protected_roles)
    AND (r.rolcanlogin <> (r.oid <> writer) OR r.rolinherit OR r.rolsuper OR r.rolcreatedb
      OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls)
  ) THEN RAISE EXCEPTION 'MiniDocs gate: unexpected role attributes'; END IF;

  -- One SET-only, non-admin edge. Checking the complete boundary of these
  -- roles also excludes inherited or SET-able transitive paths.
  IF NOT EXISTS (SELECT 1 FROM pg_auth_members WHERE member = migrator AND roleid = writer
      AND set_option AND NOT inherit_option AND NOT admin_option)
     OR EXISTS (SELECT 1 FROM pg_auth_members WHERE (member = ANY(protected_roles) OR roleid = ANY(protected_roles))
       AND NOT (member = migrator AND roleid = writer AND set_option AND NOT inherit_option AND NOT admin_option))
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected role membership'; END IF;
  IF EXISTS (SELECT 1 FROM pg_parameter_acl p CROSS JOIN LATERAL aclexplode(p.paracl) a
      WHERE a.grantee = 0 OR a.grantee = ANY(protected_roles))
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected administrative parameter privilege'; END IF;

  SELECT oid, datdba, datacl INTO STRICT item FROM pg_database WHERE datname = current_database();
  IF item.datdba = ANY(protected_roles) OR EXISTS (
    SELECT 1 FROM aclexplode(coalesce(item.datacl, acldefault('d', item.datdba))) a
    WHERE a.grantee NOT IN (item.datdba, runtime, migrator) AND a.grantee IS DISTINCT FROM bootstrap
  ) THEN RAISE EXCEPTION 'MiniDocs gate: unsafe database ownership or PUBLIC rights'; END IF;
  FOREACH role_id IN ARRAY protected_roles LOOP
    IF has_database_privilege(role_id, item.oid, 'CONNECT') <> (role_id <> writer)
       OR has_database_privilege(role_id, item.oid, 'CREATE, TEMP, CONNECT WITH GRANT OPTION')
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected database rights for %', role_id::regrole; END IF;
  END LOOP;

  FOR item IN SELECT oid, nspname, nspowner, nspacl FROM pg_namespace
    WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema'
  LOOP
    IF (item.nspname = 'minidocs' AND item.nspowner <> migrator)
       OR (item.nspname <> 'minidocs' AND item.nspowner = ANY(protected_roles))
       OR EXISTS (SELECT 1 FROM aclexplode(coalesce(item.nspacl, acldefault('n', item.nspowner))) a
                  WHERE a.grantee <> item.nspowner AND NOT a.grantee = ANY(protected_roles))
    THEN RAISE EXCEPTION 'MiniDocs gate: unsafe schema ownership or PUBLIC rights: %', item.nspname; END IF;
    FOREACH role_id IN ARRAY readers LOOP
      wanted := item.nspname = 'minidocs' OR (item.nspname = 'public' AND role_id = runtime);
      IF has_schema_privilege(role_id, item.oid, 'USAGE') <> wanted
         OR has_schema_privilege(role_id, item.oid, 'CREATE, USAGE WITH GRANT OPTION')
      THEN RAISE EXCEPTION 'MiniDocs gate: unexpected schema rights on % for %', item.nspname, role_id::regrole; END IF;
    END LOOP;
  END LOOP;
  IF NOT has_schema_privilege(migrator, 'public', 'CREATE') OR NOT has_schema_privilege(migrator, 'public', 'USAGE')
  THEN RAISE EXCEPTION 'MiniDocs gate: migrator lacks migration history schema rights'; END IF;

  FOR expected IN SELECT * FROM (VALUES
    ('minidocs', 'identity_users', 'SELECT', 'SELECT'),
    ('minidocs', 'identity_roles', 'SELECT', CASE WHEN d4 THEN 'SELECT INSERT' ELSE 'SELECT' END),
    ('minidocs', 'identity_role_claims', 'SELECT', ''),
    ('minidocs', 'identity_user_roles', 'SELECT', CASE WHEN d4 THEN 'SELECT INSERT' ELSE 'SELECT' END),
    ('minidocs', 'identity_user_claims', 'SELECT', ''),
    ('minidocs', 'identity_user_logins', 'SELECT', ''),
    ('minidocs', 'identity_user_tokens', 'SELECT INSERT UPDATE DELETE', ''),
    ('minidocs', 'workspaces', 'SELECT', 'SELECT INSERT UPDATE'),
    ('minidocs', 'user_invitations', '', 'SELECT INSERT UPDATE'),
    ('minidocs', 'via_docs_accounts', 'SELECT', 'SELECT INSERT UPDATE'),
    ('minidocs', 'provisioning_operations', 'SELECT', 'SELECT INSERT UPDATE'),
    ('minidocs', 'topics', 'SELECT', 'SELECT INSERT UPDATE DELETE'),
    ('minidocs', 'documents', 'SELECT', 'SELECT INSERT UPDATE DELETE'),
    ('minidocs', 'tags', 'SELECT', 'SELECT DELETE'),
    ('minidocs', 'document_tags', 'SELECT', 'SELECT INSERT DELETE'),
    ('minidocs', 'transcript_pages', 'SELECT', 'SELECT INSERT UPDATE DELETE'),
    ('minidocs', 'pending_transcripts', 'SELECT', 'SELECT'),
    ('minidocs', 'pending_transcript_pages', 'SELECT', 'SELECT DELETE'),
    ('minidocs', 'file_operations', 'SELECT', 'SELECT INSERT UPDATE DELETE'),
    ('minidocs', 'file_operation_receipts', 'SELECT', 'SELECT INSERT DELETE'),
    ('minidocs', 'upload_request_tombstones', 'SELECT', 'SELECT INSERT'),
    ('public', '__EFMigrationsHistory', 'SELECT', ''),
    ('minidocs', 'identity_sessions', 'SELECT', 'SELECT INSERT DELETE'),
    ('minidocs', 'administrator_bootstrap', '', 'SELECT INSERT')
  ) v(schema_name, table_name, runtime_rights, writer_rights)
  WHERE d4 OR table_name NOT IN ('identity_sessions', 'administrator_bootstrap')
  LOOP
    object_id := to_regclass(format('%I.%I', expected.schema_name, expected.table_name));
    SELECT relkind, relowner, relacl INTO item FROM pg_class WHERE oid = object_id;
    IF NOT FOUND OR item.relkind <> 'r' OR item.relowner <> migrator
       OR EXISTS (SELECT 1 FROM aclexplode(coalesce(item.relacl, acldefault('r', item.relowner))) a
                  WHERE a.grantee NOT IN (migrator, runtime, writer))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected table ownership, existence or PUBLIC rights on %', expected.table_name; END IF;
    table_count := table_count + 1;
    FOREACH role_id IN ARRAY readers LOOP
      FOREACH right_name IN ARRAY ARRAY['SELECT','INSERT','UPDATE','DELETE','TRUNCATE','REFERENCES','TRIGGER','MAINTAIN'] LOOP
        wanted := right_name = ANY(string_to_array(
            CASE WHEN role_id = runtime THEN expected.runtime_rights
                 WHEN role_id = writer THEN expected.writer_rights ELSE '' END, ' '));
        IF has_table_privilege(role_id, object_id, right_name) <> wanted
            OR has_table_privilege(role_id, object_id, right_name || ' WITH GRANT OPTION')
        THEN RAISE EXCEPTION 'MiniDocs gate: unexpected table privilege % on % for %', right_name, expected.table_name, role_id::regrole; END IF;
        IF right_name NOT IN ('SELECT','INSERT','UPDATE','REFERENCES') THEN CONTINUE; END IF;
        SELECT rights INTO column_rights FROM (VALUES
          ('user_invitations', 'runtime', 'SELECT', 'id created_by created_at updated_at expires_at revoked_at redeemed_at version'),
          ('identity_users', 'runtime', 'INSERT', 'id account_kind is_disabled user_name normalized_user_name email normalized_email email_confirmed password_hash security_stamp concurrency_stamp phone_number phone_number_confirmed two_factor_enabled lockout_end lockout_enabled access_failed_count'),
          ('identity_users', 'runtime', 'UPDATE', 'user_name normalized_user_name email normalized_email email_confirmed password_hash security_stamp concurrency_stamp phone_number phone_number_confirmed two_factor_enabled lockout_end lockout_enabled access_failed_count'),
          ('identity_users', 'writer', 'UPDATE', CASE WHEN d4
            THEN 'account_kind is_disabled security_stamp concurrency_stamp' ELSE 'account_kind' END),
          ('identity_users', 'writer', 'INSERT', CASE WHEN d4 THEN
            'id account_kind is_disabled user_name normalized_user_name email_confirmed password_hash security_stamp concurrency_stamp phone_number_confirmed two_factor_enabled lockout_enabled access_failed_count' END),
          ('identity_sessions', 'writer', 'UPDATE', 'idle_until revoked_at'),
          ('tags', 'writer', 'INSERT', CASE WHEN d6 THEN 'id workspace_id name name_key description color' END),
          ('tags', 'writer', 'UPDATE', CASE WHEN d6 THEN 'version name name_key description color updated_at' ELSE 'version' END),
          ('pending_transcripts', 'writer', 'UPDATE', 'version status updated_at source_name meta_date meta_topic meta_title meta_tags')
        ) c(table_name, principal, privilege, rights)
        WHERE c.table_name = expected.table_name AND c.privilege = right_name
          AND c.principal = CASE WHEN role_id = runtime THEN 'runtime' WHEN role_id = writer THEN 'writer' END;
        FOR item IN SELECT attnum, attname, attacl FROM pg_attribute
                    WHERE attrelid = object_id AND attnum > 0 AND NOT attisdropped
        LOOP
          IF has_column_privilege(role_id, object_id, item.attnum, right_name)
                  <> (wanted OR item.attname = ANY(string_to_array(coalesce(column_rights, ''), ' ')))
             OR has_column_privilege(role_id, object_id, item.attnum, right_name || ' WITH GRANT OPTION')
             OR EXISTS (SELECT 1 FROM aclexplode(item.attacl) a WHERE a.grantee NOT IN (migrator, runtime, writer))
          THEN RAISE EXCEPTION 'MiniDocs gate: unexpected column privilege % on %.% for %',
              right_name, expected.table_name, item.attname, role_id::regrole; END IF;
        END LOOP;
        IF column_rights IS NOT NULL AND EXISTS (
          SELECT 1 FROM unnest(string_to_array(column_rights, ' ')) AS allowed(name)
          WHERE NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = object_id
              AND a.attname = allowed.name AND a.attnum > 0 AND NOT a.attisdropped)
        ) THEN RAISE EXCEPTION 'MiniDocs gate: missing expected column on %', expected.table_name; END IF;
      END LOOP;
    END LOOP;
  END LOOP;
  IF table_count <> (SELECT count(*) FROM pg_class WHERE relnamespace IN ('minidocs'::regnamespace, 'public'::regnamespace)
                     AND relkind IN ('r','p','v','m','f'))
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected relation in protected schemas'; END IF;

  FOR item IN SELECT c.oid, c.relname, c.relowner, c.relacl FROM pg_class c
    WHERE c.relkind = 'S' AND c.relnamespace IN ('minidocs'::regnamespace, 'public'::regnamespace)
  LOOP
    IF item.oid NOT IN ('minidocs.identity_role_claims_id_seq'::regclass, 'minidocs.identity_user_claims_id_seq'::regclass)
       OR item.relowner <> migrator OR has_sequence_privilege(runtime, item.oid, 'USAGE, SELECT, UPDATE')
       OR has_sequence_privilege(writer, item.oid, 'USAGE, SELECT, UPDATE')
       OR EXISTS (SELECT 1 FROM aclexplode(coalesce(item.relacl, acldefault('s', item.relowner))) a
                  WHERE a.grantee <> migrator)
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected sequence ownership or rights on %', item.relname; END IF;
  END LOOP;
  PERFORM 'minidocs.identity_role_claims_id_seq'::regclass, 'minidocs.identity_user_claims_id_seq'::regclass;

  FOR expected IN SELECT * FROM (VALUES
    ('minidocs.assert_provisioning_complete(uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'1b0df968d455d1effb6d86833a9a127f074f6f66f5dba697c062982b404b1969'),
    ('minidocs.assert_transcript_integrity(uuid, uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'7f72189a86d9e3474eae9689219e813b7abbf829659cdaf663bd4dd9d188deb7'),
    ('minidocs.assert_upload_reservation(uuid, uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'6cba167e60b54901834e0c2e3d1d56fca14302d245f921247149e9eaa10042d7'),
    ('minidocs.assert_user_workspace(uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'9825816d08bcd49a8fbcb0fca45e485b4c6f58e454cbd597d7974a7a5ee57490'),
    ('minidocs.assert_workspace_integrity(uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'3455a5c767d76e90a82c6fba4b896806c4d0bab58702526b1634f8f6478eb067'),
    ('minidocs.check_account_workspace()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'b94ef6036ef5f22affb860e8f5aeaffb6503933c13da96f8a46949ffcf6733cd'),
    ('minidocs.check_completed_delete_receipt()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'ac307b2c45e5115249e590f0a9d155eea3f8e22c3ef7d7ee07cb7afad995c164'),
    ('minidocs.check_pending_payload()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'d6a8a93152cbaa6f5b32c4eda9ff71df04cbad0ef0987c3a1b69e7864f5f55ce'),
    ('minidocs.check_provisioning_complete()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'9e2e90c4664c61e7d20994f4c2407a9849c0ccdaf4b429dedfc74a7da5212a23'),
    ('minidocs.check_runtime_identity_insert()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'a55f43c088965c195d2304002cf14042b4aa2b5b43280457c7557976ed32906c'),
    ('minidocs.check_topic_tree()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'341a4bc96d2113dacf6b01af9adf0e83084f0899bfdf90ef3b2b9b73d450d29e'),
    ('minidocs.check_transcript_integrity()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'d8c865d49c0b7acb7f2c6c0bb06699b4d714a345205a4c469601c36e30ce487b'),
    ('minidocs.check_upload_reservation()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'9d7f93916e88e870d026e4598aab0f4d10a4ddbb590451ec21339d017b44a6a9'),
    ('minidocs.check_user_workspace()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'1c408e940bfe65f701d0d908360e714418bec2128fee9e53da82cda8bf15eda9'),
    ('minidocs.check_workspace_integrity()', 'writer', 'trigger', 'plpgsql', 'v', true, false, false,'7cb4011fcb4e1faad01ca030b309d9bf7071c898e3025f14d5b61ca602d0a2b7'),
    ('minidocs.command_assign_pending_transcript(uuid, uuid, bigint, uuid, bigint, boolean)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'d809ec76bac7908c8233f0abfc0e6e67e886ee4872ea2a1fbdfaee87bcc9682b'),
    ('minidocs.command_begin_delete_verification(uuid, uuid, uuid, bigint, uuid, interval)', 'writer', 'uuid', 'plpgsql', 'v', true, true, false,'506973f12cfee016545508347d8af3c30c62e8631fce821625491650aa2025b1'),
    ('minidocs.command_begin_provisioning(uuid, uuid, uuid, interval, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'9c979b50cb33a480ac267703ee0d3b98f2406b48b4fada6f25d9d24e399a7eb8'),
    ('minidocs.command_begin_purge(uuid, uuid, bigint, uuid, uuid, interval)', 'writer', 'uuid', 'plpgsql', 'v', true, true, false,'13364da47af01e57f730c485b947e40299bc5d99ce66ab49359b70751f859171'),
    ('minidocs.command_begin_upload(uuid, uuid, uuid, uuid, interval, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'94dc764cfe33e1b301aa31464aef507d3df0ee56285e1bc4c21b2c4c9536e650'),
    ('minidocs.command_change_page_count(uuid, uuid, bigint, integer, text)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'1854fa373964845f578b668248d6aa7aa5e5cc9a88007568159c5dabaeb1f576'),
    ('minidocs.command_claim_provisioning_verification(uuid, uuid, bigint, uuid, interval)', 'writer', 'bigint', 'plpgsql', 'v', true, true, false,CASE WHEN d4c THEN '0ed51e5a6ca4e53fc2ec69c8f7cc7ce3a9e3900da1305768eab68e2b8d9fd411' ELSE 'b8d47023b20501baf48692bccfe1b47509114d59018bcc822557148125cdcb18' END),
    ('minidocs.command_complete_provisioning(uuid, uuid, bigint, uuid, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,CASE WHEN d4c THEN '0530ef43f4df2f618fe6e142bd3609052b396548b5cda5d0f21b24b87d74941c' ELSE '26e160d54d394c755755aa783fa3d0f0d38054fad9827808cc1807285e9d68bc' END),
    ('minidocs.command_complete_purge(uuid, uuid, uuid, uuid, bigint, uuid, boolean)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'5cd76b0f36275d28a8ab1896a91e1ac16603afc9e52f5a572b541c6aa8a8364a'),
    ('minidocs.command_complete_upload(uuid, uuid, uuid, bigint, bigint, uuid, uuid, bigint, bytea)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'a4e193e240950e16f7329ce4bd3d47f3f9900dd469ea3dcc8fe0fa1d79bab345'),
    ('minidocs.command_create_invitation(uuid, bytea, uuid, timestamptz)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'85da2b2f7b2ea1f0f5e8be69c118da2303df8748cdf23101dcd3d34d1becfe09'),
    ('minidocs.command_delete_tag(uuid, uuid, bigint)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'90adc70d8bc0272870c5bfb972c518ba9d430b8995bd541bad94107b8c3667e5'),
    ('minidocs.command_delete_topic(uuid, uuid, bigint)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'54b91da7baaf30b9fa26816abfdc1aa5f59449c2117a6911da40e0643fb65fd1'),
    ('minidocs.command_edit_transcript_page(uuid, uuid, bigint, integer, bigint, text)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'74f8616c48f2c78717dcc38dc23b8c429354e34204f7614e8f3f7c74913a3183'),
    ('minidocs.command_fail_unstarted_upload(uuid, uuid, uuid, bigint)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'aa388263a28eebe0115b979d713cfc0b5cf0df7c9c53ad784cdea9ede3ee9ab1'),
    ('minidocs.command_lock_valid_invitation(bytea)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'8ca487d62fec2f2d244413f3a8d53f5d1049634fe57e4e9cdfca9492ef7899b4'),
    ('minidocs.command_mark_delete_unknown(uuid, uuid, uuid, bigint, uuid)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'e721f4d00d5b09bc91861807733f9bd2711972e9aa15cf94fa66d951435583d8'),
    ('minidocs.command_move_document(uuid, uuid, bigint, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'76b8def3b68f96a3ae50b3d8331e6e05585d1db2018ed2b7d7eadcd16344160f'),
    ('minidocs.command_move_topic(uuid, uuid, bigint, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'7d3d8e196ec87e05a3874c582ad1726ed7f305a6b0e6c202536338aff0b50801'),
    ('minidocs.command_prepare_provisioning(uuid, uuid, uuid, text, bytea, bytea)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'8c5b0c8081ee0f46f5155ccc571b8894107414222275b9a25b3af6dcc2a11ea4'),
    ('minidocs.command_reclaim_known_upload(uuid, uuid, uuid, uuid, interval, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'3c4d7d08356a599423ead1669a5b8ef165f9709626d49f277477cf47a87b552c'),
    ('minidocs.command_reconcile_unknown_upload(uuid, uuid, uuid, bigint, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'fcf3839590399189b07dcdbed4e25a3ae05625f0d02874b6bb4d8daf36f6268f'),
    ('minidocs.command_record_provisioning_candidate(uuid, uuid, uuid, bigint, uuid)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'0187bed51034d307fdf996abfadfd6c5f40dd5438becf57690a6130297b69bd8'),
    ('minidocs.command_record_upload_digest(uuid, uuid, uuid, bigint, bytea, bigint, uuid)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'53adc6ddb6438321615e3ec0e0f537d309d80833cb3e4bb1037d4f82ab330c17'),
    ('minidocs.command_record_upload_remote(uuid, uuid, uuid, uuid, bigint, uuid)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'544a589993616a58b63b0824cb5288e44a32662e0c48b207d46cc3bebf04dc5f'),
    ('minidocs.command_recover_expired_delete(uuid, uuid, uuid, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'b4d1f79631f194a617263a9d1253988b0defd9a51b5d0981f851f78c4c69c794'),
    ('minidocs.command_recover_expired_provisioning(uuid, uuid, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'bb5583eca609712b05f73f23c91775e33d24f7c650e9c2a5bbc8a93bf46feb83'),
    ('minidocs.command_recover_expired_upload(uuid, uuid, uuid, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'9bfb34630089b37192f30863abef8837a64ec2575fc69280b3067171abe69392'),
    ('minidocs.command_redeem_invitation(bytea, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, false, false,'f906c1e5ec0c09935bedafbcc010ca39f9a3c349693efee000fcd1e9affb8b2d'),
    ('minidocs.command_register_workspace(bytea, uuid, uuid, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'9b5b92381adf5110318cb48765df4d7560daea3205353fd3107f7dac0af845d7'),
    ('minidocs.command_remove_expired_receipts(uuid, integer)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'371726b55607c054cdc3da0785761f80323df3c1f70044f9899196249a745471'),
    ('minidocs.command_rename_document(uuid, uuid, bigint, text)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'a17cc9e581bb277cc74005e8fc96aae4ef43e8affcccb1ccc628573da8518986'),
    ('minidocs.command_replace_transcript(uuid, uuid, bigint, boolean, boolean, integer[], text[], boolean[])', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'72162c44ca2075aee2ab838e9cfe8f35a47cef2e0df0f4dde32b5ef9b1df5f39'),
    ('minidocs.command_reserve_upload(uuid, uuid, uuid, uuid, uuid, text, text)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'4d7b17383cea477d6240471d2ba565a8296757d480a953d9a1e7705e9250a2b6'),
    ('minidocs.command_restore(uuid, uuid, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'4afd78fea4e4c9e3596375a23b984181ee32fe8f51926f8d5f8b1110af81f1ae'),
    ('minidocs.command_retire_completed_uploads(uuid, integer)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'d5c21cc894f994b4d41c0b12fea21c410dec947970a4a0b8d43daa9c2496290e'),
    ('minidocs.command_revoke_invitation(uuid, bigint, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'7ad70e3473306fcf8bcc6544c41bcd98c45ce8ad6d2a5b83b0990dfba805b59f'),
    ('minidocs.command_set_document_tag(uuid, uuid, bigint, uuid, boolean)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false,'ace7e3205ae1e8460d07cae2358fceb3f5ad0e6cef2a4902c01990f1d6c71416'),
    ('minidocs.command_trash(uuid, uuid, bigint)', 'writer', 'integer', 'plpgsql', 'v', true, true, false,'6fa60b43b44b9dd5d89c63df51f70e748fe995c0419e37ead267823428100a5d'),
    ('minidocs.guard_file_operation()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'78a34b95599edcfff93411d02fe27eeb115f8b6fac86312aa8526bb4c02297ec'),
    ('minidocs.guard_file_transition()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'c1cd61414de3826e3204d831f3c2742956af86eeb5088b824ca97476b1b4b350'),
    ('minidocs.guard_identity_user()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'9032a8ebda7fe3876e0ce324a5b5c2d4ddc59ecda7036f96b2dbd27b429e4710'),
    ('minidocs.guard_prepared_technical_identity()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'b0b4843a55cd6ed8f81932f8a16410ef92778733dfc47924d57d3c759ab5f771'),
    ('minidocs.guard_provisioning_transition()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'f86e23effd6ddb64a90f711404326c59c6b4466ea6e0bab8cb476f711faefd60'),
    ('minidocs.guard_topic()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'7bb4099dd480b6dea3b3d0779356b4fff30bd92eb6d9d51bcd8e77a4d6c0daa6'),
    ('minidocs.guard_upload_tombstone()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'25a23b525d6b4944e8cadf7e3884c1d2ce79d76281dabe507371d6eb7f0cd72f'),
    ('minidocs.guard_workspace_owner()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'b2f90bd1ee3eb5263aa3b83938787aa4a79b33a52b11e087762f144bd58f4291'),
    ('minidocs.guard_workspace_record()', 'migrator', 'trigger', 'plpgsql', 'v', false, false, false,'d5e0aa7e93a3d74ca4a299327c71f29a8bda3d9aa49c463b17506138a5ed06c2'),
    ('minidocs.is_enabled_administrator(uuid)', 'migrator', 'boolean', 'sql', 's', false, false, true,'73f1da4585cafbbbc49b4a345c3edf05c44633aa8ad58027a914da17465fd792'),
    ('minidocs.lock_document(uuid, uuid)', 'migrator', 'boolean', 'plpgsql', 'v', false, false, true,'d67a4eefe059b1251d99f89ce6827c2927d71783f6697a415d66236521c5c20b'),
    ('minidocs.lock_workspace(uuid)', 'migrator', 'boolean', 'plpgsql', 'v', false, false, true,'425b2e06f1f135c796bce672e9d7910c1615f2b5f2abefc8b8f206abce0064ac'),
    ('minidocs.maximum_lease_duration()', 'migrator', 'interval', 'sql', 'i', false, false, true,'c3b83c31ca4f2ca7df47f9d5e6aeedcc55c993843029a9770818d7add1488388'),
    ('minidocs.require_command_argument(boolean, text)', 'migrator', 'void', 'plpgsql', 'i', false, false, true,'481bb12583d23114148d8e3972817a051d4ea6675b63d63ea6dc7de12e9764a6'),
    ('minidocs.serialize_workspace(uuid)', 'migrator', 'void', 'plpgsql', 'v', false, false, true,'18db661deb7f8502708b0e0d87426f775c81eae02af101d91a912ee60546d24e'),
    ('minidocs.valid_lease_duration(interval)', 'migrator', 'boolean', 'sql', 'i', false, false, true,'1d2b33c431d08b94fbe7c6c2ac4ad263feb15b8d309e9a837180b3ce8e5e200a'),
    ('minidocs.workspace_accepts_new_work(uuid)', 'migrator', 'boolean', 'plpgsql', 'v', false, false, true,'01f9ddd8902cb6f3c768b0979a27f1f92668cb74f4257701654787aa42cd63f6'),
    ('minidocs.workspace_accepts_provisioning(uuid)', 'migrator', 'boolean', 'plpgsql', 'v', false, false, true,'a1ba68b18362d69e7f6684342cdd3103abc9b2ad1c137b4ad33ebd5b171b3d9b'),
    ('minidocs.command_create_session(bytea, uuid, text, interval, interval)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '5e50a86a327a14b65259b43ddb103659b910442edfffdbd041f29664c7a04fba'),
    ('minidocs.command_renew_session(bytea, uuid, interval)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '878186c1db8802a9a9f1a0817b6c8faf874bba94fa59d8ae144f2d844bef5351'),
    ('minidocs.command_revoke_session(bytea, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '4eb9bb28acba4cf19eaf4d51340f93be2cbe6ef6ad764c46dc846a6080bff467'),
    ('minidocs.command_cleanup_sessions(integer)', 'writer', 'integer', 'plpgsql', 'v', true, true, false, '485540fa1f6006612e0f6a27910e06ba60b1987396de54aea838b3c161c7200a'),
    ('minidocs.lock_admin_authority(uuid, text, bytea, uuid, uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '416a47cc4cae6e790d8561d4190f197809dd0bf420ddb730a9efd1200216951d'),
    ('minidocs.command_set_account_disabled(uuid, text, bytea, uuid, text, text[], boolean)', 'writer', 'smallint', 'plpgsql', 'v', true, true, false, CASE WHEN d4c THEN '518be3fb466b2c484a6807e2e6260f4b0f1a833ffd5a4aaa83fa30eaf619d957' ELSE '1336a5281d40789ff41f1eb8eda0f39113dec0f1c759000913e2e18a3142bb41' END),
    ('minidocs.command_bootstrap_administrator(uuid, text, text, text)', 'writer', 'boolean', 'plpgsql', 'v', true, false, false, 'ef251e5a94a5981f0bc0000c7121706cb6eadbf76f1bd913887689bd936377f5'),
    ('minidocs.command_replace_technical_invitation(uuid, uuid, bigint, bytea)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '43217b0459b1444ca36020179cccdde3254336c921151d658b5cb2885f6f7c61'),
    ('minidocs.command_d6_lock(uuid)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, 'e0d22e989a31bf7f55c72c320a1a4d0dc88b61f55a80c1b9ca66f9756fcab750'),
    ('minidocs.command_d6_topic(uuid, uuid, bigint, text, uuid, boolean)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, CASE WHEN sibling_names THEN 'ca797bfd60fa7d7703916cf7de41d87a8135714063b4166098318fa29471b6e4' ELSE 'ab6927af2e79881ffd28edd70b25e2f0ad1cad44bf51c37d8ea18fcb47c37c08' END),
    ('minidocs.command_d6_tag(uuid, uuid, bigint, text, text, text)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, 'c4c616d5a6b7b546b96216f8cfee2660ab253d8957eb20cf7af7eb9bb6e4f097'),
    ('minidocs.command_d6_document(uuid, uuid, bigint, text, text, date, boolean)', 'writer', 'boolean', 'plpgsql', 'v', true, true, false, '160ee4bf1e19dc5736a5934b443e63e27d741b7c6014e4af462f503e0962d8ba')
  ) v(signature, owner_name, return_type, language_name, volatility, definer, runtime_execute, writer_grant, source_sha256)
  WHERE (d6 OR NOT starts_with(signature, 'minidocs.command_d6_')) AND
    (d4c OR signature NOT LIKE 'minidocs.command_replace_technical_invitation(%') AND
    (d4 OR signature NOT LIKE 'minidocs.%session%' AND
    signature NOT LIKE 'minidocs.lock_admin_authority(%' AND
    signature NOT LIKE 'minidocs.command_set_account_disabled(%' AND
    signature NOT LIKE 'minidocs.command_bootstrap_administrator(%')
  LOOP
    object_id := to_regprocedure(expected.signature);
    SELECT p.*, l.lanname INTO item FROM pg_proc p JOIN pg_language l ON l.oid = p.prolang WHERE p.oid = object_id;
    IF NOT FOUND OR item.prokind <> 'f'
       OR item.proowner <> (CASE WHEN expected.owner_name = 'writer' THEN writer ELSE migrator END)
       OR item.prosecdef <> expected.definer OR item.provolatile::text <> expected.volatility
       OR item.prorettype <> to_regtype(expected.return_type) OR item.lanname <> expected.language_name
       OR item.proretset OR item.proisstrict OR item.proleakproof OR item.proparallel <> 'u'
       OR item.provariadic <> 0 OR item.pronargdefaults <> 0
       OR item.proconfig IS DISTINCT FROM ARRAY['search_path=pg_catalog, pg_temp']
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected function definition or owner for %', expected.signature; END IF;
    -- Hash authored function bodies, normalizing only line endings.
    IF encode(sha256(convert_to(replace(item.prosrc, E'\r\n', E'\n'), 'UTF8')), 'hex') <> expected.source_sha256
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected function body for %', expected.signature; END IF;
    IF has_function_privilege(runtime, object_id, 'EXECUTE') <> expected.runtime_execute
       OR has_function_privilege(runtime, object_id, 'EXECUTE WITH GRANT OPTION')
       OR has_function_privilege(writer, object_id, 'EXECUTE') <> (expected.owner_name = 'writer' OR expected.writer_grant)
       OR (bootstrap IS NOT NULL AND
           (has_function_privilege(bootstrap, object_id, 'EXECUTE') <>
               (expected.signature LIKE 'minidocs.command_bootstrap_administrator(%')
            OR has_function_privilege(bootstrap, object_id, 'EXECUTE WITH GRANT OPTION')))
       OR EXISTS (SELECT 1 FROM aclexplode(coalesce(item.proacl, acldefault('f', item.proowner))) a
                  WHERE (a.grantee NOT IN (item.proowner, runtime, writer) AND a.grantee IS DISTINCT FROM bootstrap)
                      OR (a.grantee = writer AND item.proowner <> writer AND a.is_grantable))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected function privilege on %', expected.signature; END IF;
    function_count := function_count + 1;
  END LOOP;
  IF function_count <> (SELECT count(*) FROM pg_proc WHERE pronamespace = 'minidocs'::regnamespace)
     OR EXISTS (SELECT 1 FROM pg_proc WHERE pronamespace = 'public'::regnamespace
         AND (has_function_privilege(runtime, oid, 'EXECUTE') OR has_function_privilege(writer, oid, 'EXECUTE')
              OR coalesce(has_function_privilege(bootstrap, oid, 'EXECUTE'), false)))
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected function outside the approved inventory'; END IF;

  -- No non-owner grants may be introduced by defaults. The absent global
  -- function override would itself restore built-in PUBLIC EXECUTE.
  IF EXISTS (SELECT 1 FROM pg_default_acl d CROSS JOIN LATERAL aclexplode(d.defaclacl) a
      WHERE a.grantee <> d.defaclrole AND (d.defaclrole = ANY(protected_roles)
        OR (d.defaclnamespace IN ('minidocs'::regnamespace, 'public'::regnamespace)
            AND (a.grantee = 0 OR a.grantee = ANY(protected_roles)))))
     OR EXISTS (SELECT 1 FROM unnest(ARRAY[migrator, writer]) AS role(oid)
       WHERE NOT EXISTS (SELECT 1 FROM pg_default_acl d WHERE d.defaclrole = role.oid
                          AND d.defaclnamespace = 0 AND d.defaclobjtype = 'f'))
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected global or schema default ACL'; END IF;

  FOR expected IN SELECT * FROM (VALUES
    ('identity_users', 'guard_identity_user', 'guard_identity_user', 27, false, ''),
    ('workspaces', 'guard_workspace_owner', 'guard_workspace_owner', 31, false, ''),
    ('identity_users', 'check_user_workspace', 'check_user_workspace', 21, true, 'account_kind'),
    ('workspaces', 'check_workspace_integrity', 'check_workspace_integrity', 21, true, 'status'),
    ('via_docs_accounts', 'guard_via_docs_account', 'guard_workspace_record', 31, false, ''),
    ('documents', 'guard_document', 'guard_workspace_record', 31, false, 'workspace_id,id,topic_id,availability,deleted_at'),
    ('provisioning_operations', 'guard_provisioning_operation', 'guard_workspace_record', 31, false, ''),
    ('via_docs_accounts', 'check_account_workspace', 'check_account_workspace', 29, true, ''),
    ('topics', 'guard_topic', 'guard_topic', 31, false, ''),
    ('topics', 'check_topic_tree', 'check_topic_tree', 21, true, 'parent_id'),
    ('file_operations', 'guard_file_operation', 'guard_file_operation', 31, false, ''),
    ('upload_request_tombstones', 'guard_upload_tombstone', 'guard_upload_tombstone', 31, false, ''),
    ('file_operations', 'check_file_upload_reservation', 'check_upload_reservation', 29, true, ''),
    ('upload_request_tombstones', 'check_upload_tombstone', 'check_upload_reservation', 5, true, ''),
    ('identity_users', 'check_runtime_identity_insert', 'check_runtime_identity_insert', 7, false, ''),
    ('workspaces', 'check_workspace_provisioning', 'check_provisioning_complete', 21, true, 'status'),
    ('provisioning_operations', 'check_operation_provisioning', 'check_provisioning_complete', 29, true, ''),
    ('via_docs_accounts', 'check_account_provisioning', 'check_provisioning_complete', 29, true, ''),
    ('documents', 'check_document_transcript', 'check_transcript_integrity', 21, true, 'page_count,transcript_present'),
    ('transcript_pages', 'check_transcript_document', 'check_transcript_integrity', 21, true, ''),
    ('file_operations', 'check_completed_delete_receipt', 'check_completed_delete_receipt', 9, true, ''),
    ('file_operations', 'guard_file_transition', 'guard_file_transition', 19, false, 'state'),
    ('via_docs_accounts', 'guard_prepared_technical_identity', 'guard_prepared_technical_identity', 19, false, ''),
    ('provisioning_operations', 'guard_provisioning_transition', 'guard_provisioning_transition', 19, false, ''),
    ('pending_transcripts', 'check_pending_head_payload', 'check_pending_payload', 21, true, 'status'),
    ('pending_transcript_pages', 'check_pending_page_payload', 'check_pending_payload', 21, true, '')
  ) v(table_name, trigger_name, function_name, event_bits, delayed, columns)
  LOOP
    object_id := to_regclass(format('minidocs.%I', expected.table_name));
    IF NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid = object_id AND t.tgname = expected.trigger_name
        AND t.tgfoid = to_regprocedure(format('minidocs.%I()', expected.function_name))
        AND t.tgtype = expected.event_bits AND t.tgenabled = 'O' AND NOT t.tgisinternal
        AND t.tgdeferrable = expected.delayed AND t.tginitdeferred = expected.delayed
        AND t.tgnargs = 0 AND t.tgqual IS NULL
        AND ARRAY(SELECT a.attname::text FROM pg_attribute a WHERE a.attrelid = t.tgrelid AND a.attnum = ANY(t.tgattr)
                  ORDER BY a.attname) =
            ARRAY(SELECT value FROM unnest(string_to_array(expected.columns, ',')) u(value) ORDER BY value))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected trigger %.%', expected.table_name, expected.trigger_name; END IF;
    trigger_count := trigger_count + 1;
  END LOOP;
  IF trigger_count <> (SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
                       WHERE c.relnamespace = 'minidocs'::regnamespace AND NOT t.tgisinternal)
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected trigger inventory'; END IF;

  -- Canonical PostgreSQL expressions also bind the checks to their actual columns.
  FOR expected IN SELECT * FROM (VALUES
    ('via_docs_accounts', 'ck_via_credentials', $expr$((length(btrim(application_name)) > 0) AND (octet_length(application_secret_protected) > 0) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((application_id IS NULL) OR (application_id <> '00000000-0000-0000-0000-000000000000'::uuid)))$expr$),
    ('provisioning_operations', 'ck_provisioning_lifecycle', $expr$(((lease_owner IS NULL) OR (lease_owner <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((candidate_application_id IS NULL) OR (candidate_application_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND
CASE status
    WHEN 'pending_invitation'::text THEN ((attempt_no = 0) AND (lease_owner IS NULL) AND (candidate_application_id IS NULL) AND (invitation_protected IS NULL))
    WHEN 'ready'::text THEN ((lease_owner IS NULL) AND (candidate_application_id IS NULL) AND (COALESCE(octet_length(invitation_protected), 0) > 0))
    WHEN 'registering'::text THEN ((attempt_no > 0) AND (lease_owner IS NOT NULL) AND (COALESCE(octet_length(invitation_protected), 0) > 0))
    WHEN 'recovery_required'::text THEN (attempt_no > 0)
    WHEN 'active'::text THEN ((attempt_no > 0) AND (lease_owner IS NULL) AND (candidate_application_id IS NOT NULL) AND (invitation_protected IS NULL) AND (failure_category IS NULL))
    WHEN 'cancelled'::text THEN ((lease_owner IS NULL) AND (candidate_application_id IS NULL) AND (invitation_protected IS NULL))
    ELSE true
END)$expr$),
    ('documents', 'ck_document_transcript_flags', $expr$(transcript_present OR (NOT transcript_edited))$expr$),
    ('tags', 'ck_tag_name_key', $expr$(length(btrim(name_key)) > 0)$expr$),
    ('file_operations', 'ck_file_lifecycle', $expr$((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((request_id IS NULL) OR (request_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((remote_file_id IS NULL) OR (remote_file_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((lease_owner IS NULL) OR (lease_owner <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((verified_size IS NULL) OR ((kind = 'upload'::text) AND (verified_size > 0) AND (remote_file_id IS NOT NULL))) AND ((kind <> 'delete'::text) OR (remote_file_id IS NOT NULL)) AND
CASE state
    WHEN 'prepared'::text THEN ((attempt_no = 0) AND (lease_owner IS NULL) AND ((kind = 'delete'::text) OR (remote_file_id IS NULL)))
    WHEN 'in_flight'::text THEN ((attempt_no = 1) AND (lease_owner IS NOT NULL) AND (remote_file_id IS NULL))
    WHEN 'unknown'::text THEN ((attempt_no = 1) AND (lease_owner IS NULL) AND (remote_file_id IS NULL))
    WHEN 'remote_known'::text THEN ((attempt_no = 1) AND (remote_file_id IS NOT NULL))
    WHEN 'delete_in_flight'::text THEN ((attempt_no > 0) AND (lease_owner IS NOT NULL))
    WHEN 'delete_unknown'::text THEN (attempt_no > 0)
    WHEN 'completed'::text THEN ((attempt_no > 0) AND (lease_owner IS NULL) AND (remote_file_id IS NOT NULL) AND ((kind = 'delete'::text) OR (verified_size IS NOT NULL)))
    WHEN 'failed'::text THEN ((attempt_no = 0) AND (lease_owner IS NULL) AND ((kind = 'delete'::text) OR (remote_file_id IS NULL)))
    ELSE false
END)$expr$),
    ('file_operations', 'ck_file_failure_category', $expr$((failure_category IS NULL) OR (failure_category = ANY (ARRAY['not_sent'::text, 'transport_unknown'::text, 'verification_failed'::text, 'manual_review'::text])))$expr$),
    ('pending_transcripts', 'ck_pending_terminal_metadata', $expr$((status = 'pending'::text) OR ((source_name IS NULL) AND (meta_date IS NULL) AND (meta_topic IS NULL) AND (meta_title IS NULL) AND (meta_tags IS NULL)))$expr$),
    ('file_operations', 'ck_file_lease_maximum', $expr$((lease_until IS NULL) OR (isfinite(lease_until) AND (lease_until <= (updated_at + minidocs.maximum_lease_duration()))))$expr$),
    ('provisioning_operations', 'ck_provisioning_lease_maximum', $expr$((lease_until IS NULL) OR (isfinite(lease_until) AND (lease_until <= (updated_at + minidocs.maximum_lease_duration()))))$expr$),
    ('workspaces', 'ck_workspaces_positive_version', $expr$(version > 0)$expr$),
    ('user_invitations', 'ck_user_invitations_positive_version', $expr$(version > 0)$expr$),
    ('via_docs_accounts', 'ck_via_docs_accounts_positive_version', $expr$(version > 0)$expr$),
    ('provisioning_operations', 'ck_provisioning_operations_positive_version', $expr$(version > 0)$expr$),
    ('topics', 'ck_topics_positive_version', $expr$(version > 0)$expr$),
    ('documents', 'ck_documents_positive_version', $expr$(version > 0)$expr$),
    ('tags', 'ck_tags_positive_version', $expr$(version > 0)$expr$),
    ('transcript_pages', 'ck_transcript_pages_positive_version', $expr$(version > 0)$expr$),
    ('pending_transcripts', 'ck_pending_transcripts_positive_version', $expr$(version > 0)$expr$),
    ('pending_transcript_pages', 'ck_pending_transcript_pages_positive_version', $expr$(version > 0)$expr$),
    ('file_operations', 'ck_file_operations_positive_version', $expr$(version > 0)$expr$)
  ) v(table_name, constraint_name, check_expression)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid = to_regclass(format('minidocs.%I', expected.table_name))
         AND c.conname = expected.constraint_name AND c.contype = 'c' AND c.convalidated AND c.conenforced)
    THEN RAISE EXCEPTION 'MiniDocs gate: missing or inactive constraint %', expected.constraint_name; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid = to_regclass(format('minidocs.%I', expected.table_name))
         AND c.conname = expected.constraint_name AND c.contype = 'c' AND NOT c.connoinherit
         AND pg_get_expr(c.conbin, c.conrelid) = replace(expected.check_expression, chr(13), ''))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D2 constraint %', expected.constraint_name; END IF;
  END LOOP;
  -- The D2 migration fixes the identity, workspace and relation keys. A restored
  -- schema must not silently replace a composite tenant FK with an ID-only FK.
  FOR expected IN SELECT * FROM (VALUES
    ('identity_users', 'PK_identity_users', 'p', ARRAY['id']),
    ('user_invitations', 'PK_user_invitations', 'p', ARRAY['id']),
    ('workspaces', 'PK_workspaces', 'p', ARRAY['id']),
    ('file_operation_receipts', 'PK_file_operation_receipts', 'p', ARRAY['id']),
    ('pending_transcripts', 'PK_pending_transcripts', 'p', ARRAY['id']),
    ('pending_transcripts', 'AK_pending_transcripts_workspace_id_id', 'u', ARRAY['workspace_id','id']),
    ('provisioning_operations', 'PK_provisioning_operations', 'p', ARRAY['id']),
    ('tags', 'PK_tags', 'p', ARRAY['id']),
    ('tags', 'AK_tags_workspace_id_id', 'u', ARRAY['workspace_id','id']),
    ('topics', 'PK_topics', 'p', ARRAY['id']),
    ('topics', 'AK_topics_workspace_id_id', 'u', ARRAY['workspace_id','id']),
    ('upload_request_tombstones', 'PK_upload_request_tombstones', 'p', ARRAY['workspace_id','request_id']),
    ('via_docs_accounts', 'PK_via_docs_accounts', 'p', ARRAY['id']),
    ('pending_transcript_pages', 'PK_pending_transcript_pages', 'p', ARRAY['workspace_id','pending_id','page_no']),
    ('documents', 'PK_documents', 'p', ARRAY['id']),
    ('documents', 'AK_documents_workspace_id_id', 'u', ARRAY['workspace_id','id']),
    ('document_tags', 'PK_document_tags', 'p', ARRAY['workspace_id','document_id','tag_id']),
    ('file_operations', 'PK_file_operations', 'p', ARRAY['id']),
    ('file_operations', 'AK_file_operations_workspace_id_id', 'u', ARRAY['workspace_id','id']),
    ('transcript_pages', 'PK_transcript_pages', 'p', ARRAY['workspace_id','document_id','page_no'])
  ) v(table_name, constraint_name, constraint_type, columns)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c
        JOIN pg_index i ON i.indexrelid = c.conindid
        JOIN pg_class idx ON idx.oid = i.indexrelid
        JOIN pg_am am ON am.oid = idx.relam
        WHERE c.conrelid = to_regclass(format('minidocs.%I', expected.table_name))
          AND c.conname = expected.constraint_name AND c.contype::text = expected.constraint_type
          AND c.convalidated AND c.conenforced AND NOT c.condeferrable AND NOT c.condeferred
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY AS k(attnum, position)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
              WHERE a.attnotnull AND NOT a.attisdropped ORDER BY k.position) = expected.columns
          AND i.indrelid = c.conrelid AND i.indisunique AND i.indisvalid AND i.indisready AND i.indislive
          AND i.indimmediate AND NOT i.indnullsnotdistinct
          AND i.indisprimary = (expected.constraint_type = 'p') AND am.amname = 'btree'
          AND i.indpred IS NULL AND i.indexprs IS NULL
          AND i.indnatts = cardinality(expected.columns) AND i.indnkeyatts = i.indnatts
          AND ARRAY(SELECT pg_get_indexdef(i.indexrelid, k, true)
              FROM generate_series(1, i.indnkeyatts::integer) AS keys(k)) = expected.columns)
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D2 key %', expected.constraint_name; END IF;
  END LOOP;

  FOR expected IN SELECT * FROM (VALUES
    ('user_invitations', 'FK_user_invitations_identity_users_created_by', ARRAY['created_by'], 'identity_users', ARRAY['id']),
    ('user_invitations', 'FK_user_invitations_identity_users_redeemed_user_id', ARRAY['redeemed_user_id'], 'identity_users', ARRAY['id']),
    ('workspaces', 'FK_workspaces_identity_users_owner_user_id', ARRAY['owner_user_id'], 'identity_users', ARRAY['id']),
    ('file_operation_receipts', 'FK_file_operation_receipts_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('pending_transcripts', 'FK_pending_transcripts_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('provisioning_operations', 'FK_provisioning_operations_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('tags', 'FK_tags_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('topics', 'FK_topics_topics_workspace_id_parent_id', ARRAY['workspace_id','parent_id'], 'topics', ARRAY['workspace_id','id']),
    ('topics', 'FK_topics_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('upload_request_tombstones', 'FK_upload_request_tombstones_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('via_docs_accounts', 'FK_via_docs_accounts_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('pending_transcript_pages', 'FK_pending_transcript_pages_pending_transcripts_workspace_id_p~', ARRAY['workspace_id','pending_id'], 'pending_transcripts', ARRAY['workspace_id','id']),
    ('pending_transcript_pages', 'FK_pending_transcript_pages_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('documents', 'FK_documents_topics_workspace_id_topic_id', ARRAY['workspace_id','topic_id'], 'topics', ARRAY['workspace_id','id']),
    ('documents', 'FK_documents_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('document_tags', 'FK_document_tags_documents_workspace_id_document_id', ARRAY['workspace_id','document_id'], 'documents', ARRAY['workspace_id','id']),
    ('document_tags', 'FK_document_tags_tags_workspace_id_tag_id', ARRAY['workspace_id','tag_id'], 'tags', ARRAY['workspace_id','id']),
    ('document_tags', 'FK_document_tags_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('file_operations', 'FK_file_operations_documents_workspace_id_document_id', ARRAY['workspace_id','document_id'], 'documents', ARRAY['workspace_id','id']),
    ('file_operations', 'FK_file_operations_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id']),
    ('transcript_pages', 'FK_transcript_pages_documents_workspace_id_document_id', ARRAY['workspace_id','document_id'], 'documents', ARRAY['workspace_id','id']),
    ('transcript_pages', 'FK_transcript_pages_workspaces_workspace_id', ARRAY['workspace_id'], 'workspaces', ARRAY['id'])
  ) v(table_name, constraint_name, columns, target_table, target_columns)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = to_regclass(format('minidocs.%I', expected.table_name))
          AND c.conname = expected.constraint_name AND c.contype = 'f'
          AND c.convalidated AND c.conenforced AND NOT c.condeferrable AND NOT c.condeferred
          AND c.confrelid = to_regclass(format('minidocs.%I', expected.target_table))
          AND c.confmatchtype = 's' AND c.confupdtype = 'a' AND c.confdeltype = 'r'
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY AS k(attnum, position)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
              WHERE NOT a.attisdropped AND a.atttypid = 'uuid'::regtype
                AND a.attnotnull = (NOT ((expected.table_name = 'topics' AND a.attname = 'parent_id')
                  OR (expected.table_name = 'user_invitations' AND a.attname = 'redeemed_user_id')))
              ORDER BY k.position) = expected.columns
          AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY AS k(attnum, position)
              JOIN pg_attribute a ON a.attrelid = c.confrelid AND a.attnum = k.attnum
              WHERE NOT a.attisdropped AND a.atttypid = 'uuid'::regtype AND a.attnotnull
              ORDER BY k.position) = expected.target_columns
          AND NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgconstraint = c.oid
              AND t.tgenabled NOT IN ('O', 'A')))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D2 foreign key %', expected.constraint_name; END IF;
  END LOOP;

  IF NOT EXISTS (SELECT 1 FROM pg_attribute a
      WHERE a.attrelid = 'minidocs.tags'::regclass AND a.attname = 'name_key'
        AND a.attnum > 0 AND NOT a.attisdropped AND a.atttypid = 'text'::regtype AND a.attnotnull)
  THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D2 column tags.name_key'; END IF;

  -- Business uniqueness is just as important as existence of the named index.
  FOR expected IN SELECT * FROM (VALUES
    ('tags', 'IX_tags_workspace_id_name_key', ARRAY['workspace_id','name_key'], NULL::text),
    ('topics', 'IX_topics_workspace_id', ARRAY['workspace_id'], '(system_key = ''UNASSIGNED''::text)'),
    ('documents', 'IX_documents_workspace_id_via_file_id', ARRAY['workspace_id','via_file_id'], '(via_file_id IS NOT NULL)'),
    ('workspaces', 'IX_workspaces_owner_user_id', ARRAY['owner_user_id'], NULL),
    ('via_docs_accounts', 'IX_via_docs_accounts_workspace_id', ARRAY['workspace_id'], NULL)
  ) v(table_name, index_name, columns, predicate)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_index i JOIN pg_class idx ON idx.oid = i.indexrelid
        JOIN pg_am am ON am.oid = idx.relam
        WHERE i.indexrelid = to_regclass(format('minidocs.%I', expected.index_name))
          AND i.indrelid = to_regclass(format('minidocs.%I', expected.table_name))
          AND i.indisunique AND i.indisvalid AND i.indisready AND i.indislive AND i.indimmediate
          AND NOT i.indnullsnotdistinct AND NOT i.indisprimary AND am.amname = 'btree'
          AND i.indexprs IS NULL AND i.indnatts = cardinality(expected.columns) AND i.indnkeyatts = i.indnatts
          AND pg_get_expr(i.indpred, i.indrelid) IS NOT DISTINCT FROM expected.predicate
          AND ARRAY(SELECT pg_get_indexdef(i.indexrelid, k, true)
              FROM generate_series(1, i.indnkeyatts::integer) AS keys(k)) = expected.columns)
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D2 unique index %', expected.index_name; END IF;
  END LOOP;
  IF sibling_names THEN
    IF NOT EXISTS (SELECT 1 FROM pg_index i JOIN pg_class idx ON idx.oid = i.indexrelid
        JOIN pg_am am ON am.oid = idx.relam
        WHERE i.indexrelid = to_regclass('minidocs.ux_topics_sibling_name')
          AND i.indrelid = 'minidocs.topics'::regclass AND am.amname = 'btree'
          AND i.indisunique AND i.indnullsnotdistinct AND i.indisvalid AND i.indisready
          AND i.indislive AND i.indimmediate AND i.indnatts = 3 AND i.indnkeyatts = 3 AND i.indpred IS NULL
          AND pg_get_indexdef(i.indexrelid, 1, true) = 'workspace_id'
          AND pg_get_indexdef(i.indexrelid, 2, true) = 'parent_id'
          AND encode(sha256(convert_to(pg_get_indexdef(i.indexrelid, 3, true), 'UTF8')), 'hex')
            = 'c28f9496ad238d95a48622fc60ab41efc0ef930c523cac8ca459879716e116f0')
    THEN RAISE EXCEPTION 'MiniDocs gate: invalid topic sibling unique index'; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid = 'minidocs.topics'::regclass
        AND c.conname = 'ck_topics_reserved_root_name' AND c.contype = 'c'
        AND c.convalidated AND c.conenforced
        AND encode(sha256(convert_to(pg_get_expr(c.conbin, c.conrelid, true), 'UTF8')), 'hex')
          = '9d2db2f714f6fb2a74546736deea9bc8b43b9819d9cda954b76b968f96bed3b5')
    THEN RAISE EXCEPTION 'MiniDocs gate: invalid reserved root topic name check'; END IF;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_index i WHERE i.indexrelid = to_regclass('minidocs.ux_provisioning_active_workspace')
      AND i.indrelid = 'minidocs.provisioning_operations'::regclass AND i.indisunique AND i.indisvalid AND i.indisready
      AND i.indnatts = 1 AND pg_get_indexdef(i.indexrelid, 1, true) = 'workspace_id'
      AND pg_get_expr(i.indpred, i.indrelid) = '(status = ''active''::text)')
  THEN RAISE EXCEPTION 'MiniDocs gate: invalid active provisioning index'; END IF;
  IF d4 AND NOT EXISTS (SELECT 1 FROM pg_index i WHERE
      i.indexrelid = to_regclass('minidocs.ix_identity_sessions_expiry')
      AND i.indrelid = to_regclass('minidocs.identity_sessions') AND i.indisvalid AND i.indisready
      AND i.indnatts = 2 AND i.indpred IS NULL AND pg_get_indexdef(i.indexrelid, 1, true) = 'idle_until'
      AND pg_get_indexdef(i.indexrelid, 2, true) = 'handle_hash')
  THEN RAISE EXCEPTION 'MiniDocs gate: invalid identity session expiry index'; END IF;
  IF d4 THEN
    IF d4c AND (NOT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_attrdef d
        ON d.adrelid = a.attrelid AND d.adnum = a.attnum
        WHERE a.attrelid = 'minidocs.via_docs_accounts'::regclass AND a.attname = 'credential_generation'
          AND a.atttypid = 'bigint'::regtype AND a.attnotnull AND NOT a.attisdropped
          AND pg_get_expr(d.adbin, d.adrelid) = '1')
      OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid = 'minidocs.via_docs_accounts'::regclass
          AND c.conname = 'ck_via_docs_credential_generation' AND c.contype = 'c' AND c.convalidated AND c.conenforced
          AND pg_get_expr(c.conbin, c.conrelid) = '(credential_generation > 0)'))
    THEN RAISE EXCEPTION 'MiniDocs gate: unexpected credential generation invariant'; END IF;
    -- Compare column names and types as well as constraints so that the same
    -- constraint cannot be rebound to differently typed or nullable columns.
    FOR expected IN SELECT * FROM (VALUES
      ('identity_sessions', 'handle_hash', 'bytea', true),
      ('identity_sessions', 'user_id', 'uuid', true),
      ('identity_sessions', 'security_stamp', 'text', true),
      ('identity_sessions', 'issued_at', 'timestamp with time zone', true),
      ('identity_sessions', 'idle_until', 'timestamp with time zone', true),
      ('identity_sessions', 'absolute_until', 'timestamp with time zone', true),
      ('identity_sessions', 'revoked_at', 'timestamp with time zone', false),
      ('administrator_bootstrap', 'singleton', 'boolean', true),
      ('administrator_bootstrap', 'user_id', 'uuid', true),
      ('administrator_bootstrap', 'completed_at', 'timestamp with time zone', true)
    ) v(table_name, column_name, column_type, not_null)
    LOOP
      IF NOT EXISTS (SELECT 1 FROM pg_attribute a
          WHERE a.attrelid = to_regclass(format('minidocs.%I', expected.table_name))
          AND a.attname = expected.column_name AND a.attnum > 0 AND NOT a.attisdropped
          AND format_type(a.atttypid, a.atttypmod) = expected.column_type
          AND a.attnotnull = expected.not_null)
      THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D4 column %.%', expected.table_name, expected.column_name; END IF;
    END LOOP;

    FOR expected IN SELECT * FROM (VALUES
      ('identity_sessions', 'identity_sessions_pkey', 'p', ARRAY['handle_hash'], NULL),
      ('identity_sessions', 'identity_sessions_handle_hash_check', 'c', ARRAY['handle_hash'],
        '(octet_length(handle_hash) = 32)'),
      ('identity_sessions', 'identity_sessions_user_id_fkey', 'f', ARRAY['user_id'], NULL),
      ('identity_sessions', 'ck_identity_session_times', 'c', ARRAY['issued_at','idle_until','absolute_until','revoked_at'],
        '((issued_at < idle_until) AND (idle_until <= absolute_until) AND ((revoked_at IS NULL) OR (revoked_at >= issued_at)))'),
      ('administrator_bootstrap', 'administrator_bootstrap_pkey', 'p', ARRAY['singleton'], NULL),
      ('administrator_bootstrap', 'administrator_bootstrap_singleton_check', 'c', ARRAY['singleton'], 'singleton'),
      ('administrator_bootstrap', 'administrator_bootstrap_user_id_fkey', 'f', ARRAY['user_id'], NULL)
    ) v(table_name, constraint_name, constraint_type, columns, check_expression)
    LOOP
      IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE
          c.conrelid = to_regclass(format('minidocs.%I', expected.table_name))
          AND c.conname = expected.constraint_name AND c.contype::text = expected.constraint_type
          AND c.convalidated AND c.conenforced AND NOT c.condeferrable AND NOT c.condeferred
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY AS k(attnum, position)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
              ORDER BY k.position) = expected.columns
          AND CASE expected.constraint_type
            WHEN 'c' THEN pg_get_expr(c.conbin, c.conrelid) = expected.check_expression
            WHEN 'f' THEN c.confrelid = 'minidocs.identity_users'::regclass
                AND c.confkey = ARRAY[(SELECT a.attnum FROM pg_attribute a
                  WHERE a.attrelid = c.confrelid AND a.attname = 'id' AND NOT a.attisdropped)]
                AND c.confmatchtype = 's' AND c.confupdtype = 'a' AND c.confdeltype = 'a'
            WHEN 'p' THEN EXISTS (SELECT 1 FROM pg_index i WHERE i.indexrelid = c.conindid
                AND i.indisprimary AND i.indisvalid AND i.indisready)
          END)
      THEN RAISE EXCEPTION 'MiniDocs gate: unexpected D4 constraint %', expected.constraint_name; END IF;
    END LOOP;
  END IF;
END;
$gate$ LANGUAGE plpgsql;
