# Business Rules Management Module

A comprehensive Angular module for managing business rules, validation logic, and upselling strategies in the Hostr hotel chatbot admin portal.

## Overview

The Business Rules Management System allows hotel administrators to:
- Configure validation rules for hotel services (spa, room service, amenities, etc.)
- Set up rules for request items (towels, pillows, amenities)
- Manage upsell items and strategies
- Monitor upsell performance and conversion metrics
- Track all changes through a comprehensive audit log

## Module Structure

```
business-rules/
├── models/
│   └── business-rules.models.ts       # TypeScript interfaces and types
├── services/
│   └── business-rules.service.ts      # API integration service
├── pages/
│   ├── dashboard/                     # Main dashboard with overview widgets
│   ├── services-list/                 # Service business rules management
│   ├── upselling/                     # Upsell items and performance analytics
│   └── audit-log/                     # Change tracking and audit trail
├── business-rules.routes.ts           # Module routing configuration
└── README.md                          # This file
```

## Features

### 1. Dashboard (`/business-rules`)
- **Overview Widgets**: Active rules, draft rules, inactive rules, upsell revenue
- **Quick Stats**: Services with rules, request items with rules, upsell items
- **Recent Changes**: Latest audit log entries
- **Top Performers**: Best performing upsell items
- **Quick Actions**: Direct links to manage rules and upselling

### 2. Service Business Rules (`/business-rules/services`)
- **Services List**: Grid view of all hotel services
- **Expandable Rules**: Click to view rules for each service
- **Rule Types**:
  - Maximum Group Size
  - Minimum Advance Booking Hours
  - Restricted Service Hours
  - Custom Validation
- **Rule Editor**: Create and edit rules with validation
- **Toggle Active/Inactive**: Enable/disable rules without deletion
- **Delete Rules**: Remove rules with confirmation dialog

### 3. Upselling Management (`/business-rules/upselling`)
- **Tab 1 - Upsell Items**: Library of upsell items with CRUD operations
- **Tab 2 - Strategies**: Configure when and how to suggest upsells (placeholder)
- **Tab 3 - Performance**: Analytics dashboard with:
  - Total revenue from upsells
  - Conversion rate
  - Top performing items
  - Suggestions and acceptances tracking

### 4. Audit Log (`/business-rules/audit-log`)
- **Comprehensive Tracking**: All changes to rules and configurations
- **Filter Options**:
  - Search by user or entity
  - Filter by action type (CREATE, UPDATE, DELETE, ACTIVATE, DEACTIVATE)
  - Filter by entity type
  - Date range filtering
- **User Information**: Who made changes, when, and from which IP address

## Data Models

### ServiceBusinessRule
```typescript
{
  id: number;
  tenantId: number;
  serviceId: number;
  ruleType: string;                    // 'MaxGroupSize', 'MinAdvanceHours', etc.
  ruleKey: string;                     // 'max_group_size', 'min_advance_booking'
  ruleValue: string;                   // JSON string: '{"maxPeople": 8}'
  validationMessage?: string;          // Guest-facing message
  priority: number;                    // 1-5, higher = checked first
  isActive: boolean;
  minConfidenceScore?: number;         // 0.0 - 1.0
  createdAt: Date;
  updatedAt?: Date;
}
```

### UpsellItem
```typescript
{
  id: number;
  tenantId: number;
  title: string;
  description: string;
  priceCents: number;
  unit: string;
  categories: string[];
  isActive: boolean;
  priority: number;
  leadTimeMinutes?: number;
  createdAt: Date;
  updatedAt?: Date;
}
```

### AuditLogEntry
```typescript
{
  id: number;
  tenantId: number;
  userId: number;
  userName: string;
  userEmail: string;
  action: 'CREATE' | 'UPDATE' | 'DELETE' | 'ACTIVATE' | 'DEACTIVATE';
  entityType: 'ServiceBusinessRule' | 'RequestItemRule' | 'UpsellItem';
  entityId: number;
  entityName?: string;
  changesBefore?: string;              // JSON
  changesAfter?: string;               // JSON
  ipAddress: string;
  timestamp: Date;
}
```

## API Endpoints

### Service Business Rules
- `GET /api/admin/services/{tenantId}` - List all services
- `GET /api/admin/services/{tenantId}/{serviceId}/rules` - Get rules for a service
- `POST /api/admin/services/{tenantId}/{serviceId}/rules` - Create rule
- `PUT /api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}` - Update rule
- `DELETE /api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}` - Delete rule
- `PATCH /api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}/toggle` - Toggle active status

### Request Item Rules
- `GET /api/admin/request-items/{tenantId}` - List all request items
- `GET /api/admin/request-items/{tenantId}/{itemId}/rules` - Get rules for an item
- `POST /api/admin/request-items/{tenantId}/{itemId}/rules` - Create rule
- `PUT /api/admin/request-items/{tenantId}/{itemId}/rules/{ruleId}` - Update rule
- `DELETE /api/admin/request-items/{tenantId}/{itemId}/rules/{ruleId}` - Delete rule

### Upselling
- `GET /api/admin/upsell-items/{tenantId}` - List all upsell items
- `POST /api/admin/upsell-items/{tenantId}` - Create upsell item
- `PUT /api/admin/upsell-items/{tenantId}/{itemId}` - Update upsell item
- `DELETE /api/admin/upsell-items/{tenantId}/{itemId}` - Delete upsell item
- `GET /api/admin/upsell-analytics/{tenantId}?days=30` - Get performance analytics

### Audit Log
- `GET /api/admin/audit-log/{tenantId}` - Get audit log entries
- `GET /api/admin/audit-log/{tenantId}/{entryId}` - Get single entry details

### Dashboard Statistics
- `GET /api/admin/business-rules/stats/{tenantId}` - Get dashboard statistics

## Usage

### Navigating to Business Rules
From the main admin portal, navigate to `/business-rules` to access the dashboard.

### Creating a Service Rule

1. Navigate to **Service Business Rules** (`/business-rules/services`)
2. Find the service you want to configure
3. Click **Add Rule** button
4. In the modal:
   - Select **Rule Type** (e.g., "Maximum Group Size")
   - The **Rule Key** auto-generates (e.g., `max_group_size`)
   - Enter **Rule Value** as JSON (e.g., `{"maxPeople": 8}`)
   - Write a friendly **Validation Message** for guests
   - Set **Priority** (1-5, higher = more important)
   - Optionally set **Minimum Confidence Score** (0.0-1.0)
   - Toggle **Activate rule immediately** if ready
5. Click **Create Rule**

### Editing a Rule

1. Expand the service to view its rules
2. Click the **Edit** icon on the rule card
3. Modify fields as needed
4. Click **Update Rule**

### Deleting a Rule

1. Expand the service to view its rules
2. Click the **Delete** icon on the rule card
3. Confirm deletion in the dialog
4. Rule is permanently removed

### Toggling Rule Active/Inactive

1. Expand the service to view its rules
2. Use the **Active** toggle switch on each rule card
3. Rule is immediately activated or deactivated

### Viewing Upsell Performance

1. Navigate to **Upselling Management** (`/business-rules/upselling`)
2. Click the **Performance** tab
3. View:
   - Total revenue, conversion rate, suggestions, acceptances
   - Top performing upsell items
   - Detailed conversion metrics per item

### Tracking Changes

1. Navigate to **Audit Log** (`/business-rules/audit-log`)
2. Use filters to find specific changes:
   - Search by user name or email
   - Filter by action type
   - Filter by entity type
   - Set date range
3. View detailed information about who changed what and when

## Mock Data

The service includes comprehensive mock data for development and testing when backend APIs are not yet available. This allows full UI testing and demonstration.

Mock data is automatically returned when API calls fail, providing:
- Sample services (Spa Services, Room Service, Swimming Pool)
- Sample rules with various configurations
- Sample upsell items (Champagne Package, Premium Bath Amenities)
- Sample analytics data
- Sample audit log entries

## Validation

### Rule Form Validation
- **Rule Type**: Required
- **Rule Key**: Required, must be lowercase with underscores only (`^[a-z_]+$`)
- **Rule Value**: Required, must be valid JSON
- **Validation Message**: Required, max 500 characters
- **Priority**: Required, must be between 1-5
- **Min Confidence Score**: Optional, must be between 0.0-1.0

### Real-time JSON Validation
The rule value field validates JSON in real-time and displays:
- ✓ Valid JSON (green checkmark)
- ❌ Invalid JSON format (red error message)

## Design Patterns

### NobleUI Bootstrap Styling
- Uses Bootstrap 5 components and utilities
- Card-based layouts for visual hierarchy
- Consistent button styling and sizing
- Responsive grid system
- NgBootstrap components (modals, tooltips, tabs)

### Feather Icons
All icons use Feather Icons via the `appFeatherIcon` directive for consistency.

### Reactive Forms
All forms use Angular Reactive Forms with proper validation and error handling.

### RxJS Pattern
- Services use Observables for async operations
- Components use `takeUntil(destroy$)` pattern for subscription cleanup
- Proper error handling with user-friendly messages

### Standalone Components
All components are standalone (Angular 17+) for better tree-shaking and lazy loading.

## Accessibility

- Proper ARIA labels on interactive elements
- Semantic HTML structure
- Keyboard navigation support
- Screen reader friendly text alternatives
- High contrast color scheme
- Focus management in modals

## Future Enhancements

### Phase 2 (Request Item Rules)
- Dedicated request items list page
- Department-based filtering (Housekeeping, Maintenance, etc.)
- Stock quantity tracking
- Request item rule editor with item-specific validations

### Phase 3 (Advanced Upselling)
- Upsell strategy builder (when X, suggest Y)
- A/B testing framework for upsell messages
- Seasonal campaign scheduling
- Advanced analytics with charts and trends
- Revenue forecasting

### Phase 4 (Templates & Bulk Operations)
- Rule templates library (industry best practices)
- Save custom rules as templates
- Bulk import/export of rules (CSV, JSON)
- Duplicate rules across services
- Apply templates to multiple services

### Phase 5 (Testing & Preview)
- Live rule testing with sample guest requests
- Preview rule changes before activation
- Rollback functionality for changes
- Rule conflict detection
- Performance impact analysis

## Troubleshooting

### Rules Not Appearing
- Check that the service is expanded (click the chevron icon)
- Verify the service has rules configured
- Check browser console for API errors
- Ensure tenantId is correctly set

### Cannot Create Rule
- Verify all required fields are filled
- Check that Rule Value is valid JSON
- Ensure Rule Key follows naming convention
- Check for duplicate rule keys within the service

### Upsell Analytics Not Loading
- Verify tenantId is correct
- Check backend API is running
- Review browser console for errors
- Mock data will load automatically if API fails

## Support

For issues, questions, or feature requests:
1. Check this README
2. Review ADMIN_UI_UX_BUSINESS_RULES_PLAN.md for detailed specifications
3. Check browser console for errors
4. Contact the development team

## Version History

- **v1.0.0** (2025-10-08): Initial release
  - Dashboard with overview widgets
  - Service business rules management
  - Upselling hub with performance analytics
  - Audit log tracking
  - Mock data for development
  - Full CRUD operations for service rules
  - Real-time rule activation/deactivation
