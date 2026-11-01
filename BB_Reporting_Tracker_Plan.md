# Southeast Bank PLC — BB Reporting Tracker
## Revised functional design: tracking only

## 1. Purpose and scope
The system maintains responsibility, deadline, handover, submission and correction status for reporting obligations. Business report data is prepared, reviewed, consolidated and delivered outside this system through existing bank procedures. The application records what happened; its buttons do not deliver reporting data.

It stores report names, reporting periods, data-component names, responsible divisions/officers, recipients, deadlines, actual sending times, channels, references, status declarations, receipt confirmations, reminders, extension decisions and audit history. It sends administrative reminders and status notifications only.

It does not store report datasets, report files, corrected datasets, report packages or copies of report attachments. It does not prepare, consolidate, validate or transmit business reports, upload them to BB, or determine whether their contents are correct.

Supporting confirmation and deadline-permission documents remain allowed under the original requirement. These must be separate evidence-only documents, not report contents. Do not archive complete emails with report attachments. Capture selected message metadata or a confirmation-only document instead. Evidence upload guidance, restrictions and review must reflect this boundary; file extensions alone cannot establish whether a file contains business data. Notes must not be used to paste report contents.

This proposal supersedes the earlier design wherever it implied report-data storage, report-file uploads, content review, package freezing or submission from the application. Actual dates and approval policies come from the bank's approved instructions; examples below are illustrative.

## 2. Design principle: track obligations and recorded events
Each report type creates a separate tracking cycle for each reporting period. Each cycle contains internal handover obligations, where applicable, and final external submission obligations. Completion of an internal handover never automatically completes a BB submission.

| Record | Meaning |
|---|---|
| Report definition | Reusable report name, recurrence, ownership and rules |
| Report cycle | A specific period, such as RIT for October 2026 |
| Internal obligation | Named data component to be sent externally to the application by a division to the final sender |
| External obligation | A final report to be sent externally to BB by the accountable division |
| Sending event | A user's record that sending occurred, including actual time, recipient, channel and reference |
| Receipt confirmation | The receiving party's acknowledgement of receipt, where configured |
| Correction case | A linked follow-up tracking case for a partial or full correction |
| Extension | Authorized deadline amendment, documents and decision history |

Internal and external describe the recipient: internal means another bank team/division; external means BB. The system tracks both. A cycle may contain only an external obligation when no internal handover needs tracking. The provider and final sender may be the same division; do not generate artificial internal handovers.

Each obligation has one accountable division and primary officer, plus optional deputy. Separate data components from the same division are allowed. Record the acting user separately from the responsible officer.

## 3. Illustrative RIT tracking cycle
Report period: October 2026. Final sender: the bank-designated coordinating division.

| Obligation | Responsible party | Recipient | Illustrative deadline | Recorded result |
|---|---|---|---|---|
| Internet Banking Data handover | Internet Banking team | Final sending division | 8 November, 3 PM | Sent; receipt confirmed |
| CBS Data handover | CBS Division | Final sending division | 9 November, 3 PM | Sent; receipt pending |
| Card Data handover | Card Division | Final sending division | 10 November, 3 PM | Not sent |
| RIT submission to BB | Final sending division | Designated BB recipient | 12 November, 5 PM | Not sent |

No Internet Banking, CBS or Card report data is uploaded. Each provider records sending after using the existing external process. The final sender records receipt if required. Later, the final sender records actual BB submission and attaches confirmation-only proof.

## 4. Status model
Keep separate status dimensions to avoid ambiguous labels.

| Dimension | Values |
|---|---|
| Sending | Not Sent; Partially Sent; Sent |
| Receipt | Not Required; Awaiting Confirmation; Receipt Confirmed; Receipt Disputed |
| Evidence | Not Required; Missing; Provided; Verified; Rejected |
| Timeliness | Upcoming; Due Today; Overdue; Sent On Time; Sent Late |
| Correction | None; Open; Closed |
| Exception | None; Approved Not Applicable; Cancelled |

Partially Sent applies when declared scope or required obligations remain unfinished. For several mandatory channels or recipients, create child obligations and derive the parent status: all unfinished = Not Sent; some fulfilled = Partially Sent; all fulfilled = Sent. An optional or alternative channel does not create an extra mandatory obligation.

Internal workflow: Not Sent → Record Internal Sending → Awaiting Receipt → Receipt Confirmed, with receipt confirmation optional per report. A receipt dispute creates a follow-up action and preserves the original declaration. Receipt confirmation acknowledges delivery, not correctness of contents.

External workflow: Not Sent → Record BB Submission → Sent. Evidence and acknowledgement remain separate. A BB rejection or return records a follow-up/correction requirement without erasing the historical fact of sending.

Actual sending may be recorded even if internal tracking confirmations are missing. Show the discrepancy and request a reason; never block a truthful historical record solely because a tracker prerequisite is incomplete. Internal readiness is advisory and based only on recorded handover/receipt statuses.

Overdue is calculated from the applicable due time and unfulfilled scope. A sent item entered late is assessed against actual sending time; retain the system entry time and actor for audit. Reports with missing proof remain visibly unverified. Management must see claimed sending separately from verified sending.

## 5. Screen design and navigation
Main menu: Overview; Report Setup; Internal Reporting Tracker; BB Submission Tracker; Corrections; Extensions; Calendar; Monitoring Reports; Administration.

### 5.1 Report Type Entry
Tabs and fields:
- General: code, name, description, owner division, BB recipient/department, instruction reference, active dates.
- Schedule: daily, weekly, monthly, quarterly, half-yearly, yearly or ad hoc; period convention; fixed date or offset rule; due time; calendar/business-day basis; authorized holiday adjustment.
- Internal Responsibilities: repeatable rows with data name, provider division/team, officer, deputy, receiving division/officer, deadline rule and receipt-confirmation requirement. No data-entry template or report attachment field.
- External Responsibilities: accountable final division/officer, deputy, BB recipient, deadline, allowed channels and whether channels/recipients are mandatory, optional or alternatives.
- Notifications: lead days, daily time, channels, responsible recipients, supervisors, escalation rules and templates.
- Evidence and Access: confirmation-only evidence policy, verifier where required, permitted users and extension approver.

Preview upcoming cycle dates before activating schedules. Version configurations with effective dates. Existing cycles retain their deadline and responsibility snapshots; authorized amendments are explicit and audited. Support temporary report suspension with effective dates and reason, without deleting history.

### 5.2 Internal Reporting Tracker
Purpose: record whether a provider sent the named component to the final sender through the existing process.
Tabs: My Pending; Due Today; Overdue; Sent; Awaiting Receipt; Disputed; All Authorized.
Grid columns: report, period, data name, provider division/officer, recipient, due time, sending status, actual sent time, receipt status and action.

Actions:
- Record Internal Sending: actual sent time, receiving division/officer, channel, reference, concise tracking remarks and optional confirmation-only evidence.
- Confirm Receipt: receiving officer records actual received time and acknowledgement.
- Dispute Receipt / Request Follow-up: reason and assigned owner; no report contents.
- Request Extension; View History; Create Correction Follow-up.

The save action is labelled Save Sending Record. The confirmation states that this records an event already performed outside the application. Status notifications to the recipient contain task metadata only.

### 5.3 BB Submission Tracker
Tabs: Not Sent; Partially Sent; Sent; Overdue; Evidence Pending; Open Corrections; All Authorized.
Grid columns: report, period, accountable division/officer, internal handover summary, BB deadline, sending status, actual submission time, channel, evidence and correction indicators.
Detail tabs: Summary; Internal Handover Status; BB Sending Records; Confirmation Evidence; Corrections; Extensions; Activity History.

Actions: Record BB Submission; Add Confirmation Evidence; Record BB Acknowledgement; Open Correction Case; Request Extension; View History. No Send Report, Upload Report, Compile Data, Approve Report Contents or Freeze Package actions.

Submission form: recipient, channel, actual sent date/time, dispatch/portal/message reference, declared full/partial scope, confirmation evidence and tracking remarks. Partial scope uses component names only. Store channel-specific events if different channels were used at different times.

## 6. Corrections and resubmissions
A correction is a tracking case linked to the original cycle and sending event. Capture reason category, non-sensitive explanation, affected data-component names, partial/full scope, requesting authority, responsible divisions/officers, recipients, due dates and required channels.

Generate internal correction-handover tasks only for affected providers and an external correction-submission obligation where needed. Follow the same record-sending and receipt-confirmation flows. Correction contents remain outside the system. Allow multiple cycles and correction channels different from the original.

Close when the correction's required sending obligations and configured confirmation checks are satisfied, or an authorized closure exception is recorded. Keep original status and open-correction flag side by side; an on-time original submission does not hide an overdue correction.

## 7. Reminders and escalation
Default proposal: one daily pending reminder on D−2, D−1 and the due date, followed by one daily overdue reminder until actual full sending is recorded. Use Asia/Dhaka and configurable dispatch time. The first overdue message is sent on the next scheduled run after the cutoff, with no second overdue message on the same day unless the bank explicitly configures one.

Separate policies for internal handovers, final BB submissions, corrections, receipt follow-up and missing evidence. Lead days, calendar/business-day basis, daily time, email/SMS, recipients, CC, supervisors, escalation thresholds and templates are configurable by responsibility.

| Event | Primary recipient | Default reminder behavior |
|---|---|---|
| Internal obligation pending | Assigned provider | Daily from D−2; separate overdue policy |
| BB obligation pending | Assigned final sender | Daily from D−2; separate overdue policy |
| Partial sending | Owner of unfinished scope | Continue for remaining scope |
| Sending recorded; receipt outstanding | Receiving officer | Separate receipt follow-up |
| Confirmation proof missing | Responsible sender | Separate evidence follow-up |
| Correction pending | Assigned correction owner | Its own deadline policy |
| Extension requested/decided | Approver/requester | Event notice |

Sending reminders stop for fulfilled scope; receipt/evidence reminders follow independent stop rules. An approved extension reschedules future reminders. Pending/rejected extensions do not. Deduplicate logical messages by task, local date, policy and channel; record failures and retry without duplicate successful delivery. Provide daily digest mode and a notification-failure dashboard.

System notices contain report name, period, task, owner, deadline, status and authorized application link. They contain no report datasets or report attachments. Email/SMS delivery success never changes report sending status.

## 8. Deadline extensions
Support separate extensions for internal, BB and correction obligations. Record original due date, current due date, requested due date, reason, permission reference, permission-only documents, requester, designated approver and decision time. Retain every request and decision.

Internal approval alone is not evidence of BB permission. A bank-designated authority verifies the applicable permission before changing a BB deadline. An internal extension does not automatically extend the final BB deadline. Flag dependency conflicts and preserve original versus extended deadline performance, including past overdue periods. Prevent self-approval by default.

## 9. Dashboards and calendar
Personal dashboard: my unsent obligations, due today, overdue, receipt confirmations, missing evidence and pending extension decisions.
Division dashboard: internal sending performance; incoming receipt confirmations; BB obligations owned; overdue corrections; unassigned/delegated work.
Management dashboard: cycles due; not/partially/sent; declared on-time rate; verified on-time rate; late sending; open corrections; extended deadlines; evidence outstanding. Distinguish internal handovers from BB submissions and original submissions from corrections.

Filters: reporting period, division, report, frequency, responsible officer, channel and status. Every count drills into records. Do not combine task counts, channel counts and report-cycle counts into one denominator.

Calendar: month/week/agenda, with distinct labels/icons for internal, BB and correction deadlines. Filters: Mine, My Division, All Authorized. Show effective due date; original date and extension on details. Use labels as well as colors. Export only authorized tracking metadata.

Suggested external on-time metric: applicable cycles due in the selected period whose mandatory original external sending obligations were all fulfilled by their effective deadlines / all applicable cycles due. Show evidence verification separately and report original-deadline performance alongside extended-deadline performance. Approved non-applicable/cancelled items are shown separately, not as sent.

## 10. Outlook integration within tracking scope
Initial scope: administrative email reminders, application links and calendar deadline entries/exports. Calendar subjects and bodies contain tracking metadata only. Update the existing event after deadline change/reassignment instead of creating duplicates.

Optional later enhancement: Record Sending Status from Outlook. The user selects an already-sent email and chooses report/cycle/component. Capture only approved metadata such as subject, recipient, sent time and message reference; never import its body or attachments. User confirms the mapping before saving. Subjects may need redaction under bank policy.

A selected email or mailbox send result does not establish BB receipt or content correctness. Confirmation evidence must follow the dedicated evidence rules. Choose technical integration only after confirming Microsoft 365 versus on-premises Exchange and approved permissions. No mailbox-wide archiving or report transmission is in scope.

## 11. User access and accountability
| Role | Authorized functions |
|---|---|
| System Administrator | Identity, settings and integrations; no implicit business approval rights |
| Report Coordinator | Report definitions, schedules, scoped assignments and monitoring |
| Provider Officer | Record own assigned internal sending; request extensions |
| Receiving / Final Sending Officer | Confirm internal receipt; record assigned BB submissions and corrections |
| Division Supervisor | Monitor division, delegate and approve when explicitly designated |
| Extension Approver | Decide scoped extension requests |
| Evidence Verifier | Verify confirmation-only evidence where configured |
| Management Viewer | Authorized monitoring views and tracking exports |
| Auditor | Read status history, evidence, decisions and audit trail |

Combine role permissions with division, report and assignment scope. Separate record sending, confirm receipt, amend event, upload/view evidence, verify evidence, approve extension, reassign and export permissions. Enforce in APIs and attachment access as well as screens. Use SSO where available. Delegation is time-bound; preserve owner, acting user, grantor and effective dates.

Incorrect status entries are reversed/amended with a reason and linked replacement event; never silently deleted. Confirmation verification checks evidence of sending, not business report accuracy. Unassigned responsibilities appear as exceptions and notify the coordinator.

## 12. Logical architecture and data model
Application UI → authenticated tracking API → relational tracking database. Separate protected storage holds confirmation/permission documents only. A durable background worker generates cycles, evaluates reminders, sends administrative notices and updates calendar links. No reporting-data connector, report-content store or BB-upload service exists.

Core entities: Division, Team, User, Role, Permission, ResponsibilityAssignment, Delegation, ReportDefinition, DefinitionVersion, ScheduleRule, ComponentDefinition, ReportCycle, TrackingObligation, ChannelRequirement, SendingEvent, ReceiptConfirmation, EvidenceDocument, EvidenceDecision, CorrectionCase, ExtensionRequest, ExtensionDecision, StatusAmendment, ReminderPolicy, NotificationJob, NotificationDelivery, CalendarLink, HolidayCalendar and AuditEvent.

TrackingObligation distinguishes internal, external, internal correction and external correction. SendingEvent is a recorded historical event, not a delivery job. EvidenceDocument has an allowed purpose: sending confirmation or extension permission. Do not create ReportData, ReportFile, DatasetVersion, SubmissionPackage or BusinessDataValidation tables/services.

Generate cycles idempotently with unique schedule/period identifiers. Use concurrency checks for status amendments/approvals, audit actor and server timestamps, durable notification delivery and bank-approved backup/retention controls. Technology remains a separate implementation choice.

## 13. Recommended enhancements
1. Two-sided internal acknowledgement: separate provider declaration from receiver confirmation.
2. Responsibility matrix: report × division × role, with deputies and unassigned-owner alerts.
3. Advisory dependency warning: highlight BB deadlines at risk because internal sending is outstanding.
4. Original/extended deadline comparison: avoid hiding historical delays.
5. Status amendment history: reverse incorrect declarations without erasing evidence.
6. Partial and multiple-channel tracking: show unfinished scope explicitly.
7. Bulk status recording for daily/weekly obligations: preview and confirm each selected task, actual date/time and reference; no automatic mass completion.
8. Notification health: failed email/SMS delivery and unresolved recipient details.
9. Period close/reconciliation: authorized supervisor reviews outstanding tracking exceptions; closing a period never automatically marks reports sent.
10. Import the initial report inventory and current statuses through controlled metadata-only templates, with validation and duplicate detection.

## 14. Implementation plan
Phase 1 — Inventory and policy: list actual reports, component names, recipients, accountable owners, recurrence/deadline rules, channel requirements, evidence rules, extension authorities and escalation recipients.
Phase 2 — MVP: registry and cycle generation; internal and BB tracking screens; permissions/delegation; sending/receipt records; evidence-only documents; corrections; documented extensions; daily email/SMS; dashboards/calendar; audit and metadata exports.
Phase 3 — Pilot: one single-division report, one multi-division report, one multiple-channel case, one partial correction and one extension. Reconcile the tracker against existing external records.
Phase 4 — Outlook and operational enhancements: approved calendar synchronization, selected-email metadata capture, bulk status entry and expanded analytics.

## 15. Acceptance checks
- A report cycle can be tracked end to end without storing any business report file or data.
- Recording internal sending transmits no data and cannot mark BB submission as complete.
- Receiver acknowledgement changes receipt status only and does not certify report contents.
- System reminder delivery cannot change sending status.
- Partial sending leaves unfinished obligations and reminders active.
- Multiple mandatory channels require separate completion; alternatives do not.
- Actual sending and system entry times remain separate; late entries do not distort timeliness.
- Missing evidence remains visible alongside declared sending.
- A Card-only correction records its tasks/status without storing corrected Card data or changing unaffected history.
- Pending extensions do not change deadlines; approved ones preserve original dates and prior events.
- Outlook capture excludes message bodies and attachments.
- Evidence upload is purpose-restricted and reviewed to prevent business report contents being stored.
- Users cannot update other divisions' tasks without scoped authority.
- Schedule changes do not silently rewrite existing cycles; retries do not duplicate cycles/reminders.
- Status reversals preserve previous declarations and reasons.

## 16. Decisions to confirm during detailed requirements
Confirm actual report inventory and authoritative schedules; whether internal receipt acknowledgement is mandatory per report; mandatory versus alternative channels; confirmation evidence and verifier rules; escalation recipients and dispatch time; BB extension verification authority; SSO/mail environment; document retention and deployment platform.

Recommended working name: BB Reporting Tracker. The central design is an auditable register of internal and external sending obligations, supported by reminders and confirmation records, while all reporting data stays in the existing reporting process.
