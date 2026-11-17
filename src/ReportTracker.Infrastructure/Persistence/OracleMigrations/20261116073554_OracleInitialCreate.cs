using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.OracleMigrations
{
    /// <inheritdoc />
    public partial class OracleInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rt_app_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_app_roles_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    entity_type = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    entity_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    action = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    data_json = table.Column<string>(type: "NCLOB", nullable: false),
                    correlation_id = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_audit_events_id", x => x.id);
                    table.CheckConstraint("ck_rt_audit_events_data_json", "\"data_json\" IS JSON");
                });

            migrationBuilder.CreateTable(
                name: "rt_calendar_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    provider = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    external_event_id = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    last_synchronized_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_calendar_links_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_channel_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    channel = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    requirement_type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    is_fulfilled = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_channel_requirements_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_component_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    definition_version_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    provider_division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    primary_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    deputy_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    receiving_division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    deadline_rule_json = table.Column<string>(type: "NCLOB", nullable: true),
                    contributors_json = table.Column<string>(type: "NCLOB", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_component_definitions_id", x => x.id);
                    table.CheckConstraint("ck_rt_component_definitions_contributors_json", "\"contributors_json\" IS NULL OR \"contributors_json\" IS JSON");
                    table.CheckConstraint("ck_rt_component_definitions_deadline_json", "\"deadline_rule_json\" IS NULL OR \"deadline_rule_json\" IS JSON");
                });

            migrationBuilder.CreateTable(
                name: "rt_correction_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    report_cycle_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    original_sending_event_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    reason_category = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    explanation = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    affected_component_names = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    is_full_scope = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_correction_cases_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_delegations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    delegate_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    scope = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_delegations_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_divisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    code = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    is_active = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_divisions_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_evidence_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    evidence_document_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    decision = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_evidence_decisions_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_evidence_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    sending_event_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    extension_request_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    purpose = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    original_file_name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    storage_key = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    content_type = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    size_bytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    sha256 = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_evidence_documents_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_extension_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    extension_request_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    decision = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_extension_decisions_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_extension_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    original_due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    requested_due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    permission_reference = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    approver_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    decision_reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_extension_requests_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_holiday_calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    year = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    time_zone_id = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_holiday_calendars_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    notification_job_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    attempted_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    succeeded = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    provider_reference = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    failure_reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_notification_deliveries_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_notification_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    reminder_policy_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    logical_key = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    local_date = table.Column<DateTime>(type: "DATE", nullable: false),
                    channel = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    recipient = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    status = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    attempt_count = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_notification_jobs_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    code = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_permissions_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_receipt_confirmations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    sending_event_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    acting_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    is_disputed = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_receipt_confirmations_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_reminder_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    obligation_category = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    lead_days_csv = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    dispatch_time = table.Column<TimeSpan>(type: "INTERVAL DAY(0) TO SECOND(6)", nullable: false),
                    time_zone_id = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    channels_csv = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    recipients_csv = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    daily_overdue_reminders = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    working_days_csv = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    quiet_hours_start = table.Column<TimeSpan>(type: "INTERVAL DAY(0) TO SECOND(6)", nullable: true),
                    quiet_hours_end = table.Column<TimeSpan>(type: "INTERVAL DAY(0) TO SECOND(6)", nullable: true),
                    escalation_after_days = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    is_active = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_reminder_policies_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_reporting_frequencies",
                columns: table => new
                {
                    id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    code = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_reporting_frequencies_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_responsibility_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    primary_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    deputy_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    responsibility_type = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_responsibility_assignments_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_role_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    role_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    permission_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_role_permissions_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_schedule_presets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    rule_code = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    version = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    months_per_period = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    period_end_days = table.Column<string>(type: "NVARCHAR2(47)", maxLength: 47, nullable: false),
                    weekly_start_days = table.Column<string>(type: "NVARCHAR2(35)", maxLength: 35, nullable: true),
                    deadline_month_offset = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    fixed_deadline_day = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_schedule_presets_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_schedule_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    definition_version_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    period_convention = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    fixed_day = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    offset_days = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    uses_business_days = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    holiday_adjustment_authorized = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_schedule_rules_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_status_amendments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    replaced_sending_event_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    previous_status = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    replacement_status = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    reason = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    acting_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_status_amendments_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_submission_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(250)", maxLength: 250, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_submission_recipients_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    role_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    division_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_user_roles_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rt_app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    employee_id = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    email = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    mobile_number = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    is_active = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_app_users_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_app_users_rt_divisions_division_id",
                        column: x => x.division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rt_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_teams_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_teams_rt_divisions_division_id",
                        column: x => x.division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rt_holiday_dates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    holiday_calendar_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    date = table.Column<DateTime>(type: "DATE", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_holiday_dates_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_holiday_dates_rt_holiday_calendars_holiday_calendar_id",
                        column: x => x.holiday_calendar_id,
                        principalTable: "rt_holiday_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rt_report_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    code = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    owner_division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    submitting_division_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    receiving_division_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    bb_recipient = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    submission_recipient_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    instruction_reference = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_report_definitions_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_report_definitions_rt_divisions_owner_division_id",
                        column: x => x.owner_division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rt_report_definitions_rt_divisions_receiving_division_id",
                        column: x => x.receiving_division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rt_report_definitions_rt_divisions_submitting_division_id",
                        column: x => x.submitting_division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rt_report_definitions_rt_submission_recipients_submission_recipient_id",
                        column: x => x.submission_recipient_id,
                        principalTable: "rt_submission_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rt_submission_recipient_departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    submission_recipient_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_submission_recipient_departments_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_submission_recipient_departments_rt_submission_recipients_submission_recipient_id",
                        column: x => x.submission_recipient_id,
                        principalTable: "rt_submission_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rt_local_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    password_hash = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    failed_attempts = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_local_credentials_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_local_credentials_rt_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "rt_app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rt_definition_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    version_number = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    effective_from = table.Column<DateTime>(type: "DATE", nullable: false),
                    effective_to = table.Column<DateTime>(type: "DATE", nullable: true),
                    frequency = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    due_time = table.Column<TimeSpan>(type: "INTERVAL DAY(0) TO SECOND(6)", nullable: false),
                    component_deadline_offset_days = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    time_zone_id = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    schedule_rule_json = table.Column<string>(type: "NCLOB", nullable: true),
                    receipt_confirmation_required = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_definition_versions_id", x => x.id);
                    table.CheckConstraint("ck_rt_definition_versions_schedule_json", "\"schedule_rule_json\" IS NULL OR \"schedule_rule_json\" IS JSON");
                    table.ForeignKey(
                        name: "fk_rt_definition_versions_rt_report_definitions_report_definition_id",
                        column: x => x.report_definition_id,
                        principalTable: "rt_report_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rt_report_cycles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    definition_version_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    period_key = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    period_label = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    period_start = table.Column<DateTime>(type: "DATE", nullable: false),
                    period_end = table.Column<DateTime>(type: "DATE", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_report_cycles_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_report_cycles_rt_report_definitions_report_definition_id",
                        column: x => x.report_definition_id,
                        principalTable: "rt_report_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rt_report_submission_departments",
                columns: table => new
                {
                    report_definition_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    department_id = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_report_submission_departments_report_definition_id_department_id", x => new { x.report_definition_id, x.department_id });
                    table.ForeignKey(
                        name: "fk_rt_report_submission_departments_rt_report_definitions_report_definition_id",
                        column: x => x.report_definition_id,
                        principalTable: "rt_report_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rt_report_submission_departments_rt_submission_recipient_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "rt_submission_recipient_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rt_tracking_obligations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    contributors_json = table.Column<string>(type: "NCLOB", nullable: true),
                    is_required = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    report_cycle_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    parent_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    correction_case_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(250)", maxLength: 250, nullable: false),
                    accountable_division_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    receiving_division_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    primary_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    deputy_officer_id = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    recipient = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    original_due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    effective_due_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    sending_status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    receipt_status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    evidence_status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    correction_status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    exception_status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_tracking_obligations_id", x => x.id);
                    table.CheckConstraint("ck_rt_tracking_obligations_contributors_json", "\"contributors_json\" IS NULL OR \"contributors_json\" IS JSON");
                    table.ForeignKey(
                        name: "fk_rt_tracking_obligations_rt_divisions_accountable_division_id",
                        column: x => x.accountable_division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rt_tracking_obligations_rt_divisions_receiving_division_id",
                        column: x => x.receiving_division_id,
                        principalTable: "rt_divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rt_tracking_obligations_rt_report_cycles_report_cycle_id",
                        column: x => x.report_cycle_id,
                        principalTable: "rt_report_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rt_sending_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    tracking_obligation_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    acting_user_id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    actual_sent_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    recipient = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    channel = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    reference = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    is_partial = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    scope_names = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    tracking_remarks = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    is_reversed = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TIMESTAMP(6)", nullable: true),
                    row_version = table.Column<byte[]>(type: "RAW(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_sending_events_id", x => x.id);
                    table.ForeignKey(
                        name: "fk_rt_sending_events_rt_tracking_obligations_tracking_obligation_id",
                        column: x => x.tracking_obligation_id,
                        principalTable: "rt_tracking_obligations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_rt_app_roles_name",
                table: "rt_app_roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rt_app_users_division_id",
                table: "rt_app_users",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "uq_rt_app_users_email",
                table: "rt_app_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_app_users_employee_id",
                table: "rt_app_users",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_calendar_links_tracking_obligation_id_user_id_provider",
                table: "rt_calendar_links",
                columns: new[] { "tracking_obligation_id", "user_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_channel_requirements_tracking_obligation_id_channel",
                table: "rt_channel_requirements",
                columns: new[] { "tracking_obligation_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_definition_versions_report_definition_id_version_number",
                table: "rt_definition_versions",
                columns: new[] { "report_definition_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_divisions_code",
                table: "rt_divisions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_holiday_calendars_name_year",
                table: "rt_holiday_calendars",
                columns: new[] { "name", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_holiday_dates_holiday_calendar_id_date",
                table: "rt_holiday_dates",
                columns: new[] { "holiday_calendar_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_local_credentials_user_id",
                table: "rt_local_credentials",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_notification_jobs_logical_key",
                table: "rt_notification_jobs",
                column: "logical_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_permissions_code",
                table: "rt_permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_report_cycles_report_definition_id_period_key",
                table: "rt_report_cycles",
                columns: new[] { "report_definition_id", "period_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_definitions_owner_division_id",
                table: "rt_report_definitions",
                column: "owner_division_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_definitions_receiving_division_id",
                table: "rt_report_definitions",
                column: "receiving_division_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_definitions_submission_recipient_id",
                table: "rt_report_definitions",
                column: "submission_recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_definitions_submitting_division_id",
                table: "rt_report_definitions",
                column: "submitting_division_id");

            migrationBuilder.CreateIndex(
                name: "uq_rt_report_definitions_code",
                table: "rt_report_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_submission_departments_department_id",
                table: "rt_report_submission_departments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "uq_rt_reporting_frequencies_code",
                table: "rt_reporting_frequencies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_role_permissions_role_id_permission_id",
                table: "rt_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_schedule_presets_rule_code_version",
                table: "rt_schedule_presets",
                columns: new[] { "rule_code", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rt_sending_events_tracking_obligation_id",
                table: "rt_sending_events",
                column: "tracking_obligation_id");

            migrationBuilder.CreateIndex(
                name: "uq_rt_submission_recipient_departments_submission_recipient_id_name",
                table: "rt_submission_recipient_departments",
                columns: new[] { "submission_recipient_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_submission_recipients_name",
                table: "rt_submission_recipients",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rt_teams_division_id",
                table: "rt_teams",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_tracking_obligations_accountable_division_id",
                table: "rt_tracking_obligations",
                column: "accountable_division_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_tracking_obligations_receiving_division_id",
                table: "rt_tracking_obligations",
                column: "receiving_division_id");

            migrationBuilder.CreateIndex(
                name: "ix_rt_tracking_obligations_report_cycle_id",
                table: "rt_tracking_obligations",
                column: "report_cycle_id");

            migrationBuilder.CreateIndex(
                name: "uq_rt_user_roles_user_id_role_id_division_id_report_definition_id",
                table: "rt_user_roles",
                columns: new[] { "user_id", "role_id", "division_id", "report_definition_id" },
                unique: true,
                filter: "\"division_id\" IS NOT NULL AND \"report_definition_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rt_app_roles");

            migrationBuilder.DropTable(
                name: "rt_audit_events");

            migrationBuilder.DropTable(
                name: "rt_calendar_links");

            migrationBuilder.DropTable(
                name: "rt_channel_requirements");

            migrationBuilder.DropTable(
                name: "rt_component_definitions");

            migrationBuilder.DropTable(
                name: "rt_correction_cases");

            migrationBuilder.DropTable(
                name: "rt_definition_versions");

            migrationBuilder.DropTable(
                name: "rt_delegations");

            migrationBuilder.DropTable(
                name: "rt_evidence_decisions");

            migrationBuilder.DropTable(
                name: "rt_evidence_documents");

            migrationBuilder.DropTable(
                name: "rt_extension_decisions");

            migrationBuilder.DropTable(
                name: "rt_extension_requests");

            migrationBuilder.DropTable(
                name: "rt_holiday_dates");

            migrationBuilder.DropTable(
                name: "rt_local_credentials");

            migrationBuilder.DropTable(
                name: "rt_notification_deliveries");

            migrationBuilder.DropTable(
                name: "rt_notification_jobs");

            migrationBuilder.DropTable(
                name: "rt_permissions");

            migrationBuilder.DropTable(
                name: "rt_receipt_confirmations");

            migrationBuilder.DropTable(
                name: "rt_reminder_policies");

            migrationBuilder.DropTable(
                name: "rt_report_submission_departments");

            migrationBuilder.DropTable(
                name: "rt_reporting_frequencies");

            migrationBuilder.DropTable(
                name: "rt_responsibility_assignments");

            migrationBuilder.DropTable(
                name: "rt_role_permissions");

            migrationBuilder.DropTable(
                name: "rt_schedule_presets");

            migrationBuilder.DropTable(
                name: "rt_schedule_rules");

            migrationBuilder.DropTable(
                name: "rt_sending_events");

            migrationBuilder.DropTable(
                name: "rt_status_amendments");

            migrationBuilder.DropTable(
                name: "rt_teams");

            migrationBuilder.DropTable(
                name: "rt_user_roles");

            migrationBuilder.DropTable(
                name: "rt_holiday_calendars");

            migrationBuilder.DropTable(
                name: "rt_app_users");

            migrationBuilder.DropTable(
                name: "rt_submission_recipient_departments");

            migrationBuilder.DropTable(
                name: "rt_tracking_obligations");

            migrationBuilder.DropTable(
                name: "rt_report_cycles");

            migrationBuilder.DropTable(
                name: "rt_report_definitions");

            migrationBuilder.DropTable(
                name: "rt_divisions");

            migrationBuilder.DropTable(
                name: "rt_submission_recipients");
        }
    }
}
