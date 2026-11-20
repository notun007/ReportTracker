DECLARE
V_COUNT INTEGER;
BEGIN
SELECT COUNT(TABLE_NAME) INTO V_COUNT from USER_TABLES where TABLE_NAME = 'rt_ef_migrations_history';
IF V_COUNT = 0 THEN
Begin
BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_ef_migrations_history" (
    "MigrationId" NVARCHAR2(150) NOT NULL,
    "ProductVersion" NVARCHAR2(32) NOT NULL,
    CONSTRAINT "PK_rt_ef_migrations_history" PRIMARY KEY ("MigrationId")
)';
END;

End;

END IF;
EXCEPTION
WHEN OTHERS THEN
    IF(SQLCODE != -942)THEN
        RAISE;
    END IF;
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_app_roles" (
    "id" RAW(16) NOT NULL,
    "name" NVARCHAR2(100) NOT NULL,
    "description" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_app_roles_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_audit_events" (
    "id" RAW(16) NOT NULL,
    "entity_type" NVARCHAR2(2000) NOT NULL,
    "entity_id" RAW(16) NOT NULL,
    "action" NVARCHAR2(2000) NOT NULL,
    "actor_user_id" RAW(16),
    "data_json" NCLOB NOT NULL,
    "correlation_id" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_audit_events_id" PRIMARY KEY ("id"),
    CONSTRAINT "ck_rt_audit_events_data_json" CHECK ("data_json" IS JSON)
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_calendar_links" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "user_id" RAW(16) NOT NULL,
    "provider" NVARCHAR2(50) NOT NULL,
    "external_event_id" NVARCHAR2(2000) NOT NULL,
    "last_synchronized_at_utc" TIMESTAMP(6) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_calendar_links_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_channel_requirements" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "channel" NVARCHAR2(50) NOT NULL,
    "requirement_type" NUMBER(10) NOT NULL,
    "is_fulfilled" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_channel_requirements_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_component_definitions" (
    "id" RAW(16) NOT NULL,
    "definition_version_id" RAW(16) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "description" NVARCHAR2(2000),
    "provider_division_id" RAW(16) NOT NULL,
    "primary_officer_id" RAW(16),
    "deputy_officer_id" RAW(16),
    "receiving_division_id" RAW(16) NOT NULL,
    "deadline_rule_json" NCLOB,
    "contributors_json" NCLOB,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_component_definitions_id" PRIMARY KEY ("id"),
    CONSTRAINT "ck_rt_component_definitions_contributors_json" CHECK ("contributors_json" IS NULL OR "contributors_json" IS JSON),
    CONSTRAINT "ck_rt_component_definitions_deadline_json" CHECK ("deadline_rule_json" IS NULL OR "deadline_rule_json" IS JSON)
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_correction_cases" (
    "id" RAW(16) NOT NULL,
    "report_cycle_id" RAW(16) NOT NULL,
    "original_sending_event_id" RAW(16),
    "reason_category" NVARCHAR2(2000) NOT NULL,
    "explanation" NVARCHAR2(1000),
    "affected_component_names" NVARCHAR2(2000) NOT NULL,
    "is_full_scope" NUMBER(1) NOT NULL,
    "status" NUMBER(10) NOT NULL,
    "due_at_utc" TIMESTAMP(6) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_correction_cases_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_delegations" (
    "id" RAW(16) NOT NULL,
    "owner_user_id" RAW(16) NOT NULL,
    "delegate_user_id" RAW(16) NOT NULL,
    "granted_by_user_id" RAW(16) NOT NULL,
    "effective_from_utc" TIMESTAMP(6) NOT NULL,
    "effective_to_utc" TIMESTAMP(6) NOT NULL,
    "scope" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_delegations_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_divisions" (
    "id" RAW(16) NOT NULL,
    "code" NVARCHAR2(40) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "is_active" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_divisions_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_evidence_decisions" (
    "id" RAW(16) NOT NULL,
    "evidence_document_id" RAW(16) NOT NULL,
    "decided_by_user_id" RAW(16) NOT NULL,
    "decision" NUMBER(10) NOT NULL,
    "reason" NVARCHAR2(2000),
    "decided_at_utc" TIMESTAMP(6) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_evidence_decisions_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_evidence_documents" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16),
    "sending_event_id" RAW(16),
    "extension_request_id" RAW(16),
    "purpose" NUMBER(10) NOT NULL,
    "original_file_name" NVARCHAR2(2000) NOT NULL,
    "storage_key" NVARCHAR2(2000) NOT NULL,
    "content_type" NVARCHAR2(2000) NOT NULL,
    "size_bytes" NUMBER(19) NOT NULL,
    "sha256" NVARCHAR2(2000) NOT NULL,
    "status" NUMBER(10) NOT NULL,
    "uploaded_by_user_id" RAW(16) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_evidence_documents_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_extension_decisions" (
    "id" RAW(16) NOT NULL,
    "extension_request_id" RAW(16) NOT NULL,
    "decided_by_user_id" RAW(16) NOT NULL,
    "decision" NUMBER(10) NOT NULL,
    "reason" NVARCHAR2(2000) NOT NULL,
    "decided_at_utc" TIMESTAMP(6) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_extension_decisions_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_extension_requests" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "original_due_at_utc" TIMESTAMP(6) NOT NULL,
    "requested_due_at_utc" TIMESTAMP(6) NOT NULL,
    "reason" NVARCHAR2(2000) NOT NULL,
    "permission_reference" NVARCHAR2(2000),
    "requested_by_user_id" RAW(16) NOT NULL,
    "approver_user_id" RAW(16) NOT NULL,
    "status" NUMBER(10) NOT NULL,
    "decided_at_utc" TIMESTAMP(6),
    "decision_reason" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_extension_requests_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_holiday_calendars" (
    "id" RAW(16) NOT NULL,
    "name" NVARCHAR2(200) NOT NULL,
    "year" NUMBER(10) NOT NULL,
    "time_zone_id" NVARCHAR2(2000) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_holiday_calendars_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_notification_deliveries" (
    "id" RAW(16) NOT NULL,
    "notification_job_id" RAW(16) NOT NULL,
    "attempted_at_utc" TIMESTAMP(6) NOT NULL,
    "succeeded" NUMBER(1) NOT NULL,
    "provider_reference" NVARCHAR2(2000),
    "failure_reason" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_notification_deliveries_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_notification_jobs" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "reminder_policy_id" RAW(16) NOT NULL,
    "logical_key" NVARCHAR2(500) NOT NULL,
    "local_date" DATE NOT NULL,
    "channel" NVARCHAR2(2000) NOT NULL,
    "recipient" NVARCHAR2(2000) NOT NULL,
    "due_at_utc" TIMESTAMP(6) NOT NULL,
    "status" NVARCHAR2(2000) NOT NULL,
    "attempt_count" NUMBER(10) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_notification_jobs_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_permissions" (
    "id" RAW(16) NOT NULL,
    "code" NVARCHAR2(100) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_permissions_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_receipt_confirmations" (
    "id" RAW(16) NOT NULL,
    "sending_event_id" RAW(16) NOT NULL,
    "acting_user_id" RAW(16) NOT NULL,
    "received_at_utc" TIMESTAMP(6) NOT NULL,
    "is_disputed" NUMBER(1) NOT NULL,
    "reason" NVARCHAR2(2000),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_receipt_confirmations_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_reminder_policies" (
    "id" RAW(16) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "report_definition_id" RAW(16),
    "obligation_category" NVARCHAR2(2000) NOT NULL,
    "lead_days_csv" NVARCHAR2(2000) NOT NULL,
    "dispatch_time" INTERVAL DAY(0) TO SECOND(6) NOT NULL,
    "time_zone_id" NVARCHAR2(2000) NOT NULL,
    "channels_csv" NVARCHAR2(2000) NOT NULL,
    "recipients_csv" NVARCHAR2(2000) NOT NULL,
    "daily_overdue_reminders" NUMBER(1) NOT NULL,
    "working_days_csv" NVARCHAR2(2000) NOT NULL,
    "quiet_hours_start" INTERVAL DAY(0) TO SECOND(6),
    "quiet_hours_end" INTERVAL DAY(0) TO SECOND(6),
    "escalation_after_days" NUMBER(10) NOT NULL,
    "is_active" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_reminder_policies_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_reporting_frequencies" (
    "id" NUMBER(10) GENERATED BY DEFAULT ON NULL AS IDENTITY NOT NULL,
    "code" NVARCHAR2(32) NOT NULL,
    "name" NVARCHAR2(100) NOT NULL,
    "display_order" NUMBER(10) NOT NULL,
    CONSTRAINT "pk_rt_reporting_frequencies_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_responsibility_assignments" (
    "id" RAW(16) NOT NULL,
    "report_definition_id" RAW(16) NOT NULL,
    "division_id" RAW(16) NOT NULL,
    "primary_officer_id" RAW(16) NOT NULL,
    "deputy_officer_id" RAW(16),
    "responsibility_type" NVARCHAR2(2000) NOT NULL,
    "effective_from_utc" TIMESTAMP(6) NOT NULL,
    "effective_to_utc" TIMESTAMP(6),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_responsibility_assignments_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_role_permissions" (
    "id" RAW(16) NOT NULL,
    "role_id" RAW(16) NOT NULL,
    "permission_id" RAW(16) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_role_permissions_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_schedule_presets" (
    "id" RAW(16) NOT NULL,
    "rule_code" NVARCHAR2(32) NOT NULL,
    "name" NVARCHAR2(100) NOT NULL,
    "display_order" NUMBER(10) NOT NULL,
    "version" NUMBER(10) NOT NULL,
    "months_per_period" NUMBER(10) NOT NULL,
    "period_end_days" NVARCHAR2(47) NOT NULL,
    "weekly_start_days" NVARCHAR2(35),
    "deadline_month_offset" NUMBER(10) NOT NULL,
    "fixed_deadline_day" NUMBER(10),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_schedule_presets_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_schedule_rules" (
    "id" RAW(16) NOT NULL,
    "definition_version_id" RAW(16) NOT NULL,
    "period_convention" NVARCHAR2(2000) NOT NULL,
    "fixed_day" NUMBER(10),
    "offset_days" NUMBER(10),
    "uses_business_days" NUMBER(1) NOT NULL,
    "holiday_adjustment_authorized" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_schedule_rules_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_status_amendments" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "replaced_sending_event_id" RAW(16),
    "previous_status" NVARCHAR2(2000) NOT NULL,
    "replacement_status" NVARCHAR2(2000) NOT NULL,
    "reason" NVARCHAR2(2000) NOT NULL,
    "acting_user_id" RAW(16) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_status_amendments_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_submission_recipients" (
    "id" RAW(16) NOT NULL,
    "name" NVARCHAR2(250) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_submission_recipients_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_user_roles" (
    "id" RAW(16) NOT NULL,
    "user_id" RAW(16) NOT NULL,
    "role_id" RAW(16) NOT NULL,
    "division_id" RAW(16),
    "report_definition_id" RAW(16),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_user_roles_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_app_users" (
    "id" RAW(16) NOT NULL,
    "employee_id" NVARCHAR2(50) NOT NULL,
    "display_name" NVARCHAR2(2000) NOT NULL,
    "email" NVARCHAR2(320) NOT NULL,
    "mobile_number" NVARCHAR2(2000),
    "division_id" RAW(16) NOT NULL,
    "is_active" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_app_users_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_app_users_rt_divisions_division_id" FOREIGN KEY ("division_id") REFERENCES "rt_divisions" ("id") ON DELETE CASCADE
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_teams" (
    "id" RAW(16) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "division_id" RAW(16) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_teams_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_teams_rt_divisions_division_id" FOREIGN KEY ("division_id") REFERENCES "rt_divisions" ("id") ON DELETE CASCADE
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_holiday_dates" (
    "id" RAW(16) NOT NULL,
    "holiday_calendar_id" RAW(16) NOT NULL,
    "date" DATE NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_holiday_dates_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_holiday_dates_rt_holiday_calendars_holiday_calendar_id" FOREIGN KEY ("holiday_calendar_id") REFERENCES "rt_holiday_calendars" ("id") ON DELETE CASCADE
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_report_definitions" (
    "id" RAW(16) NOT NULL,
    "code" NVARCHAR2(40) NOT NULL,
    "name" NVARCHAR2(2000) NOT NULL,
    "description" NVARCHAR2(2000),
    "owner_division_id" RAW(16) NOT NULL,
    "submitting_division_id" RAW(16),
    "receiving_division_id" RAW(16),
    "bb_recipient" NVARCHAR2(2000) NOT NULL,
    "submission_recipient_id" RAW(16),
    "instruction_reference" NVARCHAR2(2000),
    "is_active" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_report_definitions_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_report_definitions_rt_divisions_owner_division_id" FOREIGN KEY ("owner_division_id") REFERENCES "rt_divisions" ("id"),
    CONSTRAINT "fk_rt_report_definitions_rt_divisions_receiving_division_id" FOREIGN KEY ("receiving_division_id") REFERENCES "rt_divisions" ("id"),
    CONSTRAINT "fk_rt_report_definitions_rt_divisions_submitting_division_id" FOREIGN KEY ("submitting_division_id") REFERENCES "rt_divisions" ("id"),
    CONSTRAINT "fk_rt_report_definitions_rt_submission_recipients_submission_recipient_id" FOREIGN KEY ("submission_recipient_id") REFERENCES "rt_submission_recipients" ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_submission_recipient_departments" (
    "id" RAW(16) NOT NULL,
    "submission_recipient_id" RAW(16) NOT NULL,
    "name" NVARCHAR2(150) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_submission_recipient_departments_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_submission_recipient_departments_rt_submission_recipients_submission_recipient_id" FOREIGN KEY ("submission_recipient_id") REFERENCES "rt_submission_recipients" ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_local_credentials" (
    "id" RAW(16) NOT NULL,
    "user_id" RAW(16) NOT NULL,
    "password_hash" NVARCHAR2(2000) NOT NULL,
    "failed_attempts" NUMBER(10) NOT NULL,
    "locked_until_utc" TIMESTAMP(6),
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_local_credentials_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_local_credentials_rt_app_users_user_id" FOREIGN KEY ("user_id") REFERENCES "rt_app_users" ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_definition_versions" (
    "id" RAW(16) NOT NULL,
    "report_definition_id" RAW(16) NOT NULL,
    "version_number" NUMBER(10) NOT NULL,
    "effective_from" DATE NOT NULL,
    "effective_to" DATE,
    "frequency" NUMBER(10) NOT NULL,
    "due_time" INTERVAL DAY(0) TO SECOND(6) NOT NULL,
    "component_deadline_offset_days" NUMBER(10) NOT NULL,
    "time_zone_id" NVARCHAR2(64) NOT NULL,
    "schedule_rule_json" NCLOB,
    "receipt_confirmation_required" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_definition_versions_id" PRIMARY KEY ("id"),
    CONSTRAINT "ck_rt_definition_versions_schedule_json" CHECK ("schedule_rule_json" IS NULL OR "schedule_rule_json" IS JSON),
    CONSTRAINT "fk_rt_definition_versions_rt_report_definitions_report_definition_id" FOREIGN KEY ("report_definition_id") REFERENCES "rt_report_definitions" ("id") ON DELETE CASCADE
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_report_cycles" (
    "id" RAW(16) NOT NULL,
    "report_definition_id" RAW(16) NOT NULL,
    "definition_version_id" RAW(16) NOT NULL,
    "period_key" NVARCHAR2(80) NOT NULL,
    "period_label" NVARCHAR2(2000) NOT NULL,
    "period_start" DATE NOT NULL,
    "period_end" DATE NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_report_cycles_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_report_cycles_rt_report_definitions_report_definition_id" FOREIGN KEY ("report_definition_id") REFERENCES "rt_report_definitions" ("id") ON DELETE CASCADE
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_report_submission_departments" (
    "report_definition_id" RAW(16) NOT NULL,
    "department_id" RAW(16) NOT NULL,
    CONSTRAINT "pk_rt_report_submission_departments_report_definition_id_department_id" PRIMARY KEY ("report_definition_id", "department_id"),
    CONSTRAINT "fk_rt_report_submission_departments_rt_report_definitions_report_definition_id" FOREIGN KEY ("report_definition_id") REFERENCES "rt_report_definitions" ("id") ON DELETE CASCADE,
    CONSTRAINT "fk_rt_report_submission_departments_rt_submission_recipient_departments_department_id" FOREIGN KEY ("department_id") REFERENCES "rt_submission_recipient_departments" ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_tracking_obligations" (
    "id" RAW(16) NOT NULL,
    "contributors_json" NCLOB,
    "is_required" NUMBER(1) DEFAULT 1 NOT NULL,
    "report_cycle_id" RAW(16) NOT NULL,
    "parent_obligation_id" RAW(16),
    "correction_case_id" RAW(16),
    "type" NUMBER(10) NOT NULL,
    "name" NVARCHAR2(250) NOT NULL,
    "accountable_division_id" RAW(16) NOT NULL,
    "receiving_division_id" RAW(16),
    "primary_officer_id" RAW(16),
    "deputy_officer_id" RAW(16),
    "recipient" NVARCHAR2(2000) NOT NULL,
    "original_due_at_utc" TIMESTAMP(6) NOT NULL,
    "effective_due_at_utc" TIMESTAMP(6) NOT NULL,
    "sending_status" NUMBER(10) NOT NULL,
    "receipt_status" NUMBER(10) NOT NULL,
    "evidence_status" NUMBER(10) NOT NULL,
    "correction_status" NUMBER(10) NOT NULL,
    "exception_status" NUMBER(10) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_tracking_obligations_id" PRIMARY KEY ("id"),
    CONSTRAINT "ck_rt_tracking_obligations_contributors_json" CHECK ("contributors_json" IS NULL OR "contributors_json" IS JSON),
    CONSTRAINT "fk_rt_tracking_obligations_rt_divisions_accountable_division_id" FOREIGN KEY ("accountable_division_id") REFERENCES "rt_divisions" ("id"),
    CONSTRAINT "fk_rt_tracking_obligations_rt_divisions_receiving_division_id" FOREIGN KEY ("receiving_division_id") REFERENCES "rt_divisions" ("id"),
    CONSTRAINT "fk_rt_tracking_obligations_rt_report_cycles_report_cycle_id" FOREIGN KEY ("report_cycle_id") REFERENCES "rt_report_cycles" ("id")
)';
END;
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_sending_events" (
    "id" RAW(16) NOT NULL,
    "tracking_obligation_id" RAW(16) NOT NULL,
    "acting_user_id" RAW(16) NOT NULL,
    "actual_sent_at_utc" TIMESTAMP(6) NOT NULL,
    "recipient" NVARCHAR2(2000) NOT NULL,
    "channel" NVARCHAR2(2000) NOT NULL,
    "reference" NVARCHAR2(2000),
    "is_partial" NUMBER(1) NOT NULL,
    "scope_names" NVARCHAR2(2000),
    "tracking_remarks" NVARCHAR2(1000),
    "is_reversed" NUMBER(1) NOT NULL,
    "created_at_utc" TIMESTAMP(6) NOT NULL,
    "updated_at_utc" TIMESTAMP(6),
    "row_version" RAW(8) NOT NULL,
    CONSTRAINT "pk_rt_sending_events_id" PRIMARY KEY ("id"),
    CONSTRAINT "fk_rt_sending_events_rt_tracking_obligations_tracking_obligation_id" FOREIGN KEY ("tracking_obligation_id") REFERENCES "rt_tracking_obligations" ("id")
)';
END;
/

CREATE UNIQUE INDEX "uq_rt_app_roles_name" ON "rt_app_roles" ("name")
/

CREATE INDEX "ix_rt_app_users_division_id" ON "rt_app_users" ("division_id")
/

CREATE UNIQUE INDEX "uq_rt_app_users_email" ON "rt_app_users" ("email")
/

CREATE UNIQUE INDEX "uq_rt_app_users_employee_id" ON "rt_app_users" ("employee_id")
/

CREATE UNIQUE INDEX "uq_rt_calendar_links_tracking_obligation_id_user_id_provider" ON "rt_calendar_links" ("tracking_obligation_id", "user_id", "provider")
/

CREATE UNIQUE INDEX "uq_rt_channel_requirements_tracking_obligation_id_channel" ON "rt_channel_requirements" ("tracking_obligation_id", "channel")
/

CREATE UNIQUE INDEX "uq_rt_definition_versions_report_definition_id_version_number" ON "rt_definition_versions" ("report_definition_id", "version_number")
/

CREATE UNIQUE INDEX "uq_rt_divisions_code" ON "rt_divisions" ("code")
/

CREATE UNIQUE INDEX "uq_rt_holiday_calendars_name_year" ON "rt_holiday_calendars" ("name", "year")
/

CREATE UNIQUE INDEX "uq_rt_holiday_dates_holiday_calendar_id_date" ON "rt_holiday_dates" ("holiday_calendar_id", "date")
/

CREATE UNIQUE INDEX "uq_rt_local_credentials_user_id" ON "rt_local_credentials" ("user_id")
/

CREATE UNIQUE INDEX "uq_rt_notification_jobs_logical_key" ON "rt_notification_jobs" ("logical_key")
/

CREATE UNIQUE INDEX "uq_rt_permissions_code" ON "rt_permissions" ("code")
/

CREATE UNIQUE INDEX "uq_rt_report_cycles_report_definition_id_period_key" ON "rt_report_cycles" ("report_definition_id", "period_key")
/

CREATE INDEX "ix_rt_report_definitions_owner_division_id" ON "rt_report_definitions" ("owner_division_id")
/

CREATE INDEX "ix_rt_report_definitions_receiving_division_id" ON "rt_report_definitions" ("receiving_division_id")
/

CREATE INDEX "ix_rt_report_definitions_submission_recipient_id" ON "rt_report_definitions" ("submission_recipient_id")
/

CREATE INDEX "ix_rt_report_definitions_submitting_division_id" ON "rt_report_definitions" ("submitting_division_id")
/

CREATE UNIQUE INDEX "uq_rt_report_definitions_code" ON "rt_report_definitions" ("code")
/

CREATE INDEX "ix_rt_report_submission_departments_department_id" ON "rt_report_submission_departments" ("department_id")
/

CREATE UNIQUE INDEX "uq_rt_reporting_frequencies_code" ON "rt_reporting_frequencies" ("code")
/

CREATE UNIQUE INDEX "uq_rt_role_permissions_role_id_permission_id" ON "rt_role_permissions" ("role_id", "permission_id")
/

CREATE UNIQUE INDEX "uq_rt_schedule_presets_rule_code_version" ON "rt_schedule_presets" ("rule_code", "version")
/

CREATE INDEX "ix_rt_sending_events_tracking_obligation_id" ON "rt_sending_events" ("tracking_obligation_id")
/

CREATE UNIQUE INDEX "uq_rt_submission_recipient_departments_submission_recipient_id_name" ON "rt_submission_recipient_departments" ("submission_recipient_id", "name")
/

CREATE UNIQUE INDEX "uq_rt_submission_recipients_name" ON "rt_submission_recipients" ("name")
/

CREATE INDEX "ix_rt_teams_division_id" ON "rt_teams" ("division_id")
/

CREATE INDEX "ix_rt_tracking_obligations_accountable_division_id" ON "rt_tracking_obligations" ("accountable_division_id")
/

CREATE INDEX "ix_rt_tracking_obligations_receiving_division_id" ON "rt_tracking_obligations" ("receiving_division_id")
/

CREATE INDEX "ix_rt_tracking_obligations_report_cycle_id" ON "rt_tracking_obligations" ("report_cycle_id")
/

CREATE UNIQUE INDEX "uq_rt_user_roles_user_id_role_id_division_id_report_definition_id" ON "rt_user_roles" ("user_id", "role_id", "division_id", "report_definition_id")
/

INSERT INTO "rt_ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES (N'20261116073554_OracleInitialCreate', N'10.0.10')
/

ALTER TABLE "rt_report_definitions" ADD "report_type_id" NUMBER(10) DEFAULT 2 NOT NULL
/

BEGIN
EXECUTE IMMEDIATE 'CREATE TABLE
"rt_report_types" (
    "id" NUMBER(10) GENERATED BY DEFAULT ON NULL AS IDENTITY NOT NULL,
    "name" NVARCHAR2(50) NOT NULL,
    CONSTRAINT "pk_rt_report_types_id" PRIMARY KEY ("id")
)';
END;
/

BEGIN
INSERT INTO "rt_report_types" ("id", "name")
VALUES (1, N'RIT');
INSERT INTO "rt_report_types" ("id", "name")
VALUES (2, N'None RIT');
END;
/

CREATE INDEX "ix_rt_report_definitions_report_type_id" ON "rt_report_definitions" ("report_type_id")
/

ALTER TABLE "rt_report_definitions" ADD CONSTRAINT "fk_rt_report_definitions_rt_report_types_report_type_id" FOREIGN KEY ("report_type_id") REFERENCES "rt_report_types" ("id")
/

INSERT INTO "rt_ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES (N'20261117041357_AddReportType', N'10.0.10')
/

DECLARE
    duplicate_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO duplicate_count
    FROM (
        SELECT "report_definition_id", "period_start"
        FROM "rt_report_cycles"
        GROUP BY "report_definition_id", "period_start"
        HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Duplicate report-cycle period_start values exist. Resolve them before applying this migration.');
    END IF;

    SELECT COUNT(*) INTO duplicate_count
    FROM (
        SELECT "report_definition_id", "period_end"
        FROM "rt_report_cycles"
        GROUP BY "report_definition_id", "period_end"
        HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'Duplicate report-cycle period_end values exist. Resolve them before applying this migration.');
    END IF;
END;
/

CREATE UNIQUE INDEX "uq_rt_report_cycles_report_definition_id_period_end" ON "rt_report_cycles" ("report_definition_id", "period_end")
/

CREATE UNIQUE INDEX "uq_rt_report_cycles_report_definition_id_period_start" ON "rt_report_cycles" ("report_definition_id", "period_start")
/

INSERT INTO "rt_ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES (N'20261118145639_PreventDuplicateReportCycleDates', N'10.0.10')
/

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
