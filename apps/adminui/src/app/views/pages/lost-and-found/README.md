# Lost & Found Module

Complete implementation of the Lost & Found management system for the Hostr Admin UI.

## Overview

This module provides hotel staff with an intuitive interface to manage lost and found items, verify matches between lost and found items, and process claims. It follows the design specification from `ADMIN_UI_UX_BUSINESS_RULES_PLAN.md` (Page 7).

## Features Implemented

### 1. Quick Stats Dashboard
- Open Reports counter
- Items in Storage counter
- Pending Matches counter
- Urgent Items counter (checkout today)

### 2. Tab Navigation
- **Lost Items Reported**: View all guest-reported lost items
- **Found Items**: Manage items in storage
- **Matches to Verify**: Review AI-generated matches
- **Claimed Items**: History of claimed items

### 3. Filter & Search System
- Text search (item name, guest name, reference number)
- Category filter (Electronics, Clothing, Jewelry, Documents, Keys, Personal, Other)
- Urgency filter (Urgent checkout, High-value items, Recent reports)
- Sort options (Newest, Most urgent, Best matches)

### 4. Visual Card-Based UI
- Color-coded status system:
  - Yellow/Warning = Lost item (guest report)
  - Blue/Info = Found item (in storage)
  - Green/Success = Matched (needs verification)
  - Orange/Danger = Urgent (checkout today)
- Category icons using Feather Icons
- Visual match comparison (split-view)

### 5. Register Found Item Modal
- Visual category selection grid
- Quick entry form optimized for housekeeping staff
- Auto-security flag for high-value items
- Storage location tracking
- Automatic match finding after registration

### 6. Match Verification Interface
- Side-by-side comparison (Lost vs Found)
- Match confidence score with color coding
- Match reason breakdown
- One-click confirm/reject actions
- Verification notes and rejection reasons

### 7. Real-time Updates
- Auto-refresh every 30 seconds
- SignalR ready structure for live updates

## File Structure

```
lost-and-found/
├── models/
│   └── lost-and-found.models.ts      # TypeScript interfaces
├── services/
│   └── lost-and-found.service.ts     # API integration service
├── lost-and-found.component.ts        # Main component logic
├── lost-and-found.component.html      # Template
├── lost-and-found.component.scss      # Styles
├── lost-and-found.routes.ts           # Routing configuration
└── README.md                          # This file
```

## API Integration

The service integrates with the following backend endpoints:

- `GET /api/lostandfound/lost-items` - List lost item reports
- `GET /api/lostandfound/found-items` - List found items in storage
- `GET /api/lostandfound/matches` - Get potential matches
- `POST /api/lostandfound/found-items` - Register new found item
- `PUT /api/lostandfound/matches/{id}/verify` - Confirm/reject match
- `PUT /api/lostandfound/lost-items/{id}/close` - Mark as found
- `GET /api/lostandfound/stats` - Dashboard statistics
- `POST /api/lostandfound/found-items/{id}/find-matches` - Trigger match finding

## Setup Instructions

### 1. Routing Already Configured
The module route has been added to `app.routes.ts`:
```typescript
{
  path: 'lost-and-found',
  loadChildren: () => import('./views/pages/lost-and-found/lost-and-found.routes')
}
```

### 2. Navigation Menu (Optional)
Add to your sidebar navigation (`sidebar.component.ts` or navigation config):
```typescript
{
  title: 'Lost & Found',
  icon: 'package',
  path: '/lost-and-found'
}
```

### 3. Access the Module
Navigate to: `http://localhost:4200/lost-and-found`

### 4. Verify Backend API
Ensure the Lost & Found backend service is running and accessible at `/api/lostandfound`

## Component Usage

### Standalone Component
This is a standalone Angular component using:
- CommonModule
- FormsModule & ReactiveFormsModule
- NgBootstrap (NgbNav, NgbTooltip, NgbDropdown, NgbModal)
- Custom FeatherIconDirective

### Key Methods

**Data Loading:**
- `loadData()` - Load all data (stats, lost items, found items, matches)
- `refreshData()` - Manually refresh all data
- `applyFilters()` - Apply current filter settings

**Modal Actions:**
- `openRegisterFoundItemModal()` - Open found item registration
- `openMatchVerificationModal(match)` - Open match verification
- `confirmMatch()` - Confirm a match
- `rejectMatch()` - Reject a match

**Item Registration:**
- `selectCategory(category)` - Select item category
- `registerFoundItem()` - Submit found item registration
- `findMatchesForFoundItem(id)` - Trigger AI matching

## Styling

The component uses:
- **NobleUI Bootstrap classes** for base styling
- **Custom SCSS** for Lost & Found specific styles
- **Feather Icons** for all iconography
- **Color-coded status system** for visual clarity
- **Responsive grid layout** (Bootstrap grid)

### Key CSS Classes
- `.icon-badge` - Icon containers in stats cards
- `.lost-item-card` / `.found-item-card` - Item cards
- `.item-icon-lg` - Large item category icons
- `.details-grid` - Two-column details layout
- `.category-grid` - Category selection grid in modal
- `.match-item` - Match comparison cards

## Design Decisions & Best Practices

### 1. Visual-First Interface
- Icon-based category selection (no typing required)
- Color-coded status system (universal understanding)
- Large tap targets for mobile/tablet use
- Minimal text, maximum visual information

### 2. Non-Technical User Optimization
- Simple language (e.g., "Mark as Found" vs "Update Status")
- Step-by-step guided workflows
- Helpful tooltips and alerts
- One-click actions for common tasks

### 3. Performance
- Lazy loading with standalone component
- Auto-refresh on 30-second interval
- Efficient date parsing in service
- trackBy functions for list rendering

### 4. Accessibility
- Semantic HTML structure
- ARIA labels on interactive elements
- Keyboard navigation support (Bootstrap)
- Clear focus indicators

## Future Enhancements

Based on the design spec, potential future additions:

1. **Item Details Offcanvas Panel**
   - Full item history
   - Activity timeline
   - Guest information display

2. **Image Upload**
   - Photo upload for found items
   - Visual comparison in matches

3. **SMS/WhatsApp Integration**
   - Direct guest notifications
   - Contact buttons

4. **Barcode/QR Code Scanning**
   - Quick item logging
   - Storage location tracking

5. **Advanced Analytics**
   - Recovery rate tracking
   - Common lost item categories
   - Staff performance metrics

6. **Disposal Automation**
   - Automatic alerts before disposal
   - Disposal approval workflow

## Troubleshooting

### API Connection Issues
- Verify backend is running on correct port
- Check CORS configuration
- Ensure authentication token is valid

### Styling Issues
- Verify Feather Icons are initialized (FeatherIconDirective)
- Check Bootstrap CSS is loaded
- Clear browser cache

### Data Not Loading
- Check browser console for errors
- Verify API endpoints return correct data format
- Check date parsing (dates must be ISO 8601 format)

## Testing Checklist

- [ ] Register a new found item
- [ ] View found item in "Found Items" tab
- [ ] Verify auto-match generation
- [ ] Review match in "Matches to Verify" tab
- [ ] Confirm a match
- [ ] Reject a match
- [ ] View claimed items
- [ ] Filter by category
- [ ] Filter by urgency
- [ ] Search functionality
- [ ] Mobile responsiveness
- [ ] Auto-refresh (wait 30+ seconds)

## Support

For issues or questions:
1. Check design spec: `ADMIN_UI_UX_BUSINESS_RULES_PLAN.md` (Page 7)
2. Review API documentation
3. Check browser console for errors
4. Verify backend service is running

## Credits

Implementation follows the comprehensive UI/UX design specification from `ADMIN_UI_UX_BUSINESS_RULES_PLAN.md`.

Built with Angular 17+, NobleUI template (Bootstrap), and Feather Icons.
