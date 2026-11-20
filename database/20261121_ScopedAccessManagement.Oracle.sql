ALTER TABLE "rt_user_roles" ADD "is_active" NUMBER(1) DEFAULT 1 NOT NULL
/

ALTER TABLE "rt_app_roles" ADD "code" NVARCHAR2(64)
/

UPDATE "rt_app_roles" SET "code" = 'ROLE_' || RAWTOHEX("id") WHERE "code" IS NULL
/

declare
   l_nullable user_tab_columns.nullable % type;
begin
   select nullable into l_nullable
   from user_tab_columns
  where table_name = 'rt_app_roles'
  and column_name = 'code'
;
   if l_nullable = 'N' then
        EXECUTE IMMEDIATE 'ALTER TABLE "rt_app_roles" MODIFY "code"  ';
 else
        EXECUTE IMMEDIATE 'ALTER TABLE "rt_app_roles" MODIFY "code"  NOT NULL';
 end if;
end;
/

ALTER TABLE "rt_app_roles" ADD "is_active" NUMBER(1) DEFAULT 1 NOT NULL
/

ALTER TABLE "rt_app_roles" ADD "is_system" NUMBER(1) DEFAULT 0 NOT NULL
/

CREATE UNIQUE INDEX "uq_rt_app_roles_code" ON "rt_app_roles" ("code")
/

INSERT INTO "rt_ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES (N'20261120215420_ScopedAccessManagement', N'10.0.10')
/
