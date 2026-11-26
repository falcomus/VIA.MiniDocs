-- DBA step, ONLY on the explicitly selected, dedicated MiniDocs database.
-- The operator provisions two distinct LOGIN roles and their credentials separately.
-- The non-login owner exists solely to limit the privileges of the writing functions.
-- Run before EF migration. For an archive restore with pre-existing roles use
-- RestoreDatabaseConnect.sql and check the archive ACLs with the read-only gate.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'minidocs_command_owner') THEN
    CREATE ROLE minidocs_command_owner NOLOGIN NOINHERIT;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'minidocs_command_owner'
      AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))
     OR EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members
       WHERE member = 'minidocs_command_owner'::regrole)
  THEN RAISE EXCEPTION 'Expected an isolated non-login command owner.'; END IF;
  IF (SELECT count(*) FROM pg_catalog.pg_roles
      WHERE rolname IN ('minidocs_migrator', 'minidocs_runtime')
        AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole
        AND NOT rolreplication AND NOT rolbypassrls) <> 2
     OR EXISTS (
       SELECT 1 FROM pg_catalog.pg_auth_members m
       JOIN pg_catalog.pg_roles r ON r.oid = m.member
       WHERE r.rolname IN ('minidocs_migrator', 'minidocs_runtime')
         AND NOT (r.rolname = 'minidocs_migrator' AND m.roleid = 'minidocs_command_owner'::regrole
           AND m.set_option AND NOT m.inherit_option AND NOT m.admin_option))
     OR EXISTS (
       SELECT 1 FROM pg_catalog.pg_database d
       JOIN pg_catalog.pg_roles r ON r.oid = d.datdba
       WHERE d.datname = current_database()
         AND r.rolname IN ('minidocs_migrator', 'minidocs_runtime'))
  THEN
    RAISE EXCEPTION 'Expected two isolated, non-owner, least-privilege MiniDocs login roles.';
  END IF;
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC, minidocs_migrator, minidocs_runtime',
      current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO minidocs_migrator, minidocs_runtime',
      current_database());
END;
$$;

REVOKE ALL ON SCHEMA public FROM PUBLIC, minidocs_runtime;
GRANT USAGE ON SCHEMA public TO minidocs_runtime;
-- EF stores its migration history in public; only the migration role may create it.
GRANT USAGE, CREATE ON SCHEMA public TO minidocs_migrator;

CREATE SCHEMA IF NOT EXISTS minidocs AUTHORIZATION minidocs_migrator;
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_catalog.pg_namespace n JOIN pg_catalog.pg_roles r ON r.oid = n.nspowner
    WHERE n.nspname = 'minidocs' AND r.rolname = 'minidocs_migrator')
  THEN
    RAISE EXCEPTION 'MiniDocs schema must be owned by the migration role.';
  END IF;
END;
$$;
REVOKE ALL ON SCHEMA minidocs FROM PUBLIC, minidocs_runtime;
GRANT USAGE ON SCHEMA minidocs TO minidocs_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE minidocs_migrator REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE minidocs_command_owner REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
GRANT minidocs_command_owner TO minidocs_migrator WITH INHERIT FALSE, SET TRUE;
