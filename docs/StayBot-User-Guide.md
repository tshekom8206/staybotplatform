# StayBot User Guide

> Version: Draft
> Property: Tenant 1 (Demo)

## Branding
- Logo: `docs/images/staybot-logo.png` (add if needed)
- Primary color: aligned to StayBot website

## 1. Introduction
- **Purpose**: How to operate StayBot for hotel operations, communication, and guest services.
- **Audience**: Admins and operations staff.
- **Prerequisites**: Login URL, user account, role permissions.

## 2. Login & Navigation
1) Open `http://localhost:4200`
2) Sign in with your credentials
3) Explore the sidebar for modules

![Dashboard](../screenshots/01_dashboard_or_home.png)

## 3. Configuration
### 3.1 Tenant Settings
- Purpose: Configure core property details.
![Tenant Settings](../screenshots/route__settings_tenant.png)

### 3.2 Hotel Information
- Purpose: Property details used across the platform.
![Hotel Info](../screenshots/route__settings_hotel.png)

### 3.3 WhatsApp (WABA)
- Purpose: Channel configuration.
![WhatsApp](../screenshots/route__settings_whatsapp.png)

### 3.4 Users
- Purpose: Staff and roles.
![Users](../screenshots/route__settings_users.png)

### 3.5 Departments
- Purpose: Operational groups.
![Departments](../screenshots/route__settings_departments.png)

## 4. Knowledge & Content
### 4.1 FAQs
![FAQ List](../screenshots/route__knowledge_faq.png)
![FAQ Add](../screenshots/add__knowledge_faq.png)
![FAQ View](../screenshots/view__knowledge_faq.png)
![FAQ Edit](../screenshots/edit__knowledge_faq.png)

### 4.2 Templates
![Templates](../screenshots/route__templates.png)
![Templates Add](../screenshots/add__templates.png)
![Templates View](../screenshots/view__templates.png)
![Templates Edit](../screenshots/edit__templates.png)

### 4.3 Welcome Messages
![Welcome Messages](../screenshots/route__welcome-messages.png)
![Welcome Add](../screenshots/add__welcome-messages.png)
![Welcome View](../screenshots/view__welcome-messages.png)
![Welcome Edit](../screenshots/edit__welcome-messages.png)

## 5. Concierge & Menu
### 5.1 Menu
- Manage categories, items, specials.
![Menu](../screenshots/route__catalog_menu.png)
![Menu Add](../screenshots/add__catalog_menu.png)
![Menu View](../screenshots/view__catalog_menu.png)
![Menu Edit](../screenshots/edit__catalog_menu.png)

## 6. Operations
### 6.1 Property Directions
![Property Directions](../screenshots/route__property_directions.png)
![Property Directions Add](../screenshots/add__property_directions.png)
![Property Directions View](../screenshots/view__property_directions.png)
![Property Directions Edit](../screenshots/edit__property_directions.png)

### 6.2 Emergency
![Emergency](../screenshots/route__emergency.png)
![Emergency Add](../screenshots/add__emergency.png)
![Emergency View](../screenshots/view__emergency.png)
![Emergency Edit](../screenshots/edit__emergency.png)

## 7. Conversations
![Conversations](../screenshots/route__conversations.png)
![Active Chats](../screenshots/route__conversations_active.png)
![Assignments](../screenshots/route__conversations_assignments.png)
![Transfer Queue](../screenshots/route__conversations_transfer-queue.png)
![History](../screenshots/route__conversations_history.png)

## 8. Guests
![Bookings](../screenshots/route__guests_bookings.png)
![Check-ins Today](../screenshots/route__guests_checkins-today.png)
![Guest History](../screenshots/route__guests_history.png)

## 9. Tasks
![Tasks All](../screenshots/route__tasks_all.png)
![My Tasks](../screenshots/route__tasks_my.png)
![Housekeeping](../screenshots/route__tasks_housekeeping.png)
![Maintenance](../screenshots/route__tasks_maintenance.png)
![Front Desk](../screenshots/route__tasks_front-desk.png)

## 10. Broadcast
![Broadcast Send](../screenshots/route__broadcast_send.png)
![Broadcast Emergency](../screenshots/route__broadcast_emergency.png)
![Broadcast Templates](../screenshots/route__broadcast_templates.png)
![Broadcast History](../screenshots/route__broadcast_history.png)

## 11. Lost & Found
![Lost & Found](../screenshots/route__lost-and-found.png)

## 12. Management
### 12.1 Configuration
![Config](../screenshots/route__management_config.png)
![Services & Amenities](../screenshots/route__management_config_services.png)
![Menu Items](../screenshots/route__management_config_menu-items.png)
![Emergency Settings](../screenshots/route__management_config_emergency-settings.png)
![Template Manager](../screenshots/route__management_config_template-manager.png)

### 12.2 Business Rules
![Rules Dashboard](../screenshots/route__management_rules_dashboard.png)
![Service Rules](../screenshots/route__management_rules_service-rules.png)
![Upselling](../screenshots/route__management_rules_upselling.png)
![Audit Log](../screenshots/route__management_rules_audit-log.png)

### 12.3 Users
![Staff Management](../screenshots/route__management_users_staff.png)
![Agent Dashboard](../screenshots/route__management_users_agent-dashboard.png)
![Roles & Permissions](../screenshots/route__management_users_roles.png)
![Activity Log](../screenshots/route__management_users_activity-log.png)

### 12.4 Reports & Analytics
![Analytics Dashboard](../screenshots/route__reports_analytics.png)
![Task Performance](../screenshots/route__reports_task-performance.png)
![Guest Satisfaction](../screenshots/route__reports_guest-satisfaction.png)
![Service Usage](../screenshots/route__reports_service-usage.png)
![Business Impact](../screenshots/route__analytics_business-impact.png)

## 13. Appendix: Onboarding Excel
- Use `Tenant_Onboarding_Questionnaire.xlsx`
- Follow `Start Here` tab
- Required fields highlighted; use dropdowns

## 14. Troubleshooting
- If a page is empty, ensure seed data is populated for your tenant.
- For login issues, verify user, Tenant link, and PasswordHash.

---

## 15. Screen Map and Element Reference

This section describes the common layout elements across StayBot screens and shows where to find primary actions.

### 15.1 Dashboard Overview

Image: `docs/screenshots/01_dashboard_or_home.png`

- **[Sidebar Navigation]**: Module tree with sections: Main, Hotel Operations, Management, Support. Active item is highlighted.
- **[Top Bar]**: Search bar, date range, language, user menu.
- **[Primary Actions]**: Right side buttons such as Export, Print when applicable.
- **[KPI Cards]**: Active Guests, Pending Tasks, Check-ins, Avg Response Time, Check-outs, Room Occupancy, Completed Tasks, Guest Satisfaction.
- **[Filters]**: Date range quick toggles Today/Week/Month.
- **[Content Area]**: Dynamic widgets and tables.

### 15.2 Conversations

Image: `docs/screenshots/route_conversations_active.png`

- **[Search Conversations]**: Global search input at top of list.
- **[Status Filter]**: Status dropdown to the right of search.
- **[Tabs/Sections]**: Active, Assignments, Transfers, History.
- **[Table/List]**: Columns for Guest, Room, Status, Priority, Last Message, Agent.
- **[Row Actions]**: Edit/View buttons where available; otherwise click row to open details.

Steps:
- Open: Sidebar → Conversations → Active Chats.
- Filter: Use search or status filter.
- View details: Click a row to open conversation.

### 15.3 Guests

Images: `docs/screenshots/route_guests_bookings.png`, `route_guests_checkins.png`, `route_guests_history.png`

- **[Filters]**: Date pickers and search.
- **[Primary Actions]**: Add/New for manual bookings (if role allows).
- **[Row Actions]**: View or Edit guest record (role-based).

Steps:
- Open Guests → Bookings.
- Search by name/room/date.
- Open booking to view details.

### 15.4 Tasks

Images: `docs/screenshots/route_tasks_all.png`, `route_tasks_my.png`, `route_tasks_housekeeping.png`, `route_tasks_maintenance.png`, `route_tasks_frontdesk.png`

- **[Filters]**: Status, priority, assignee.
- **[Primary Actions]**: Create task.
- **[Boards/Tables]**: Kanban or table view depending on configuration.

Steps:
- Open Tasks → All.
- Create: Click Create or Add.
- Assign: Select assignee and due date.

### 15.5 Broadcast

Images: `docs/screenshots/route_broadcast_compose.png`, `route_broadcast_templates.png`, `route_broadcast_history.png`, `route_broadcast_emergency.png`

- **[Template Picker]**: Choose saved content.
- **[Audience Filters]**: Segment by tags, room, language.
- **[Primary Actions]**: Send/Queue, Save Template, Emergency broadcast.

### 15.6 Configuration

Images: `docs/screenshots/route_configuration_services.png`, `route_configuration_menu.png`, `route_configuration_templates.png`, `route_configuration_emergency.png`, `route_configuration_faqs.png`, `route_configuration_hotel-info.png`

- **[Sections]**: Services & Amenities, Menu, Templates, Emergency, FAQs, Hotel Info.
- **[Primary Actions]**: Add/New, Edit, Save.
- **[Tables/Forms]**: List → View/Edit → Save flow.

### 15.7 Business Rules

Images: `docs/screenshots/route_business-rules_dashboard.png`, `route_business-rules_services.png`, `route_business-rules_upselling.png`, `route_business-rules_audit-log.png`

- **[Dashboards]**: KPIs for rules impact.
- **[Rule Editors]**: Service rules and upselling conditions.

### 15.8 Users

Images: `docs/screenshots/route_users_staff.png`, `route_users_agents.png`, `route_users_roles.png`, `route_users_activity.png`

- **[Staff Directory]**: Search and role filters.
- **[Agents]**: Channel agent status.
- **[Roles]**: Permission sets.
- **[Activity]**: Audit trail.

### 15.9 Reports & Analytics

Images: `docs/screenshots/route_reports_analytics.png`, `route_reports_tasks.png`, `route_reports_satisfaction.png`, `route_reports_usage.png`, `route_analytics_business-impact.png`

- **[Filters]**: Date range, department, channel.
- **[Charts/Tables]**: Export to CSV or PDF.

---

## 16. Common Tasks (Step-by-Step)

### 16.1 Create and assign a task

Reference image: `docs/screenshots/route_tasks_all.png`

- **[Open Tasks]**: Sidebar → Tasks → All.
- **[New Task]**: Click Create/Add.
- **[Enter Details]**: Title, description, priority, due date.
- **[Assign]**: Select staff/department.
- **[Save]**: Click Save. The task appears in the list/board.

### 16.2 Send a broadcast

Reference images: `route_broadcast_compose.png`, `route_broadcast_templates.png`

- **[Open Broadcast]**: Sidebar → Broadcast → Compose.
- **[Template]**: Pick a template or write a message.
- **[Audience]**: Set filters (tags/rooms/language).
- **[Send]**: Click Send/Queue. Check History for status.

### 16.3 Update services & amenities

Reference image: `route_configuration_services.png`

- **[Open Config]**: Sidebar → Configuration → Services.
- **[Add/Edit]**: Use the Add button or Edit action.
- **[Save]**: Confirm and save changes.

### 16.4 Review analytics

Reference image: `route_reports_analytics.png`

- **[Open Analytics]**: Sidebar → Reports → Analytics.
- **[Filter]**: Set date/channel filters.
- **[Export]**: Use Export/Print controls.

---

## 17. Image Notes

- Images are generated automatically by Playwright and saved to `docs/screenshots/`.
- If annotations are enabled, overlays and callouts are drawn directly on the images.
- To regenerate screenshots, run:

```powershell
npx --prefix tools/snapshots playwright test tools/snapshots/tests/capture.pw.spec.ts --reporter=list
```

