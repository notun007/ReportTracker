ALTER TABLE "rt_tracking_obligations" RENAME COLUMN "primary_officer_id" TO "primary_assignee_id"
/

ALTER TABLE "rt_tracking_obligations" RENAME COLUMN "deputy_officer_id" TO "alternate_assignee_id"
/

UPDATE "rt_reminder_policies"
SET "recipients_csv" = REGEXP_REPLACE("recipients_csv",
    '(^|,)([[:space:]]*)Deputy([[:space:]]*)(,|$)', '\1\2Alternate\3\4', 1, 0, 'i')
WHERE REGEXP_LIKE("recipients_csv", '(^|,)[[:space:]]*Deputy[[:space:]]*(,|$)', 'i')
/

ALTER TABLE "rt_responsibility_assignments" RENAME COLUMN "primary_officer_id" TO "primary_assignee_id"
/

ALTER TABLE "rt_responsibility_assignments" RENAME COLUMN "deputy_officer_id" TO "alternate_assignee_id"
/

ALTER TABLE "rt_component_definitions" RENAME COLUMN "primary_officer_id" TO "primary_assignee_id"
/

ALTER TABLE "rt_component_definitions" RENAME COLUMN "deputy_officer_id" TO "alternate_assignee_id"
/

INSERT INTO "rt_ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES (N'20261120185023_RenameAssignmentAssignees', N'10.0.10')
/

