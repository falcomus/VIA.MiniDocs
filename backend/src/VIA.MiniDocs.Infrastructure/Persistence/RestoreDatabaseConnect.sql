-- DBA step for a fresh, dedicated MiniDocs restore target.
-- A dump without CREATE DATABASE does not carry the source database ACL.
-- Preserve schema ACLs, function ACLs, owners and defaults from the archive
-- so the post-restore gate verifies the restored state without repairing it.
DO $$
BEGIN
  EXECUTE pg_catalog.format(
      'REVOKE ALL ON DATABASE %I FROM PUBLIC, minidocs_migrator, minidocs_runtime, minidocs_command_owner',
      pg_catalog.current_database());
  EXECUTE pg_catalog.format('GRANT CONNECT ON DATABASE %I TO minidocs_migrator, minidocs_runtime',
      pg_catalog.current_database());
  IF pg_catalog.to_regrole('minidocs_bootstrap') IS NOT NULL THEN
    EXECUTE pg_catalog.format('REVOKE ALL ON DATABASE %I FROM minidocs_bootstrap', pg_catalog.current_database());
    EXECUTE pg_catalog.format('GRANT CONNECT ON DATABASE %I TO minidocs_bootstrap', pg_catalog.current_database());
  END IF;
END;
$$;
