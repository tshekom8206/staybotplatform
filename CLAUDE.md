# CLAUDE.md - Project Configuration & Commands

## Database Connection Information

**IMPORTANT: Always use these exact database connection details:**

- **Database Name**: `hostr`  
- **Username**: `postgres`
- **Password**: `postgres`
- **Host**: `localhost`
- **Port**: `5432`

**Connection String**: `Host=staybot-prod-psql.postgres.database.azure.com;Database=staybot;Username=staybot_admin;Password=5tayB0t2025Prod;SSL Mode=Require;`

## Common Commands

### Database Operations
```bash
# Connect to PostgreSQL
psql -h staybot-prod-psql.postgres.database.azure.com -U staybot_admin -d staybot

# Run EF migrations
cd "C:\Users\Administrator\Downloads\hostr\apps\api"
dotnet ef database update

# Create new migration  
dotnet ef migrations add <MigrationName>
```

### Development Server
```bash
# Run API server
cd "C:\Users\Administrator\Downloads\hostr\apps\api"
dotnet run --urls=http://localhost:5000
```

### Testing API Endpoints
```bash
# Test message routing (replace tenantId and phoneNumber as needed)
curl -X POST "http://localhost:5000/api/message/route" -H "Content-Type: application/json" -d '{
  "tenantId": 2,
  "phoneNumber": "+27123456789", 
  "messageText": "I need towels"
}'
```

## Project Structure

- **API**: `apps/api/` - ASP.NET Core 8.0 Web API
- **Database**: PostgreSQL with Entity Framework Core
- **Features**: Multi-tenant hotel chatbot with real-time notifications (SignalR)

## Key Database Tables

- `Tenants` - Hotel tenants
- `Bookings` - Guest bookings with room numbers
- `Conversations` - Chat conversations  
- `Messages` - Individual chat messages
- `StaffTasks` - Tasks created from guest requests
- `EmergencyIncidents` - Emergency incident tracking
- `RequestItems` - Available hotel items/services

## WhatsApp Cloud API Integration

For complete instructions on setting up WhatsApp Business for multiple tenants, see:

**📘 [WhatsApp Multi-Tenant Setup Guide](WHATSAPP_MULTI_TENANT_SETUP.md)**

This comprehensive guide includes:
- Screenshots from Meta dashboard showing exactly where to find credentials
- Step-by-step phone number addition process for each tenant
- Database configuration with SQL examples
- Troubleshooting for common webhook and authentication issues
- Production deployment considerations

## Recent Changes

- **Migrated from Twilio to WhatsApp Cloud API** (October 2025)
- Removed Twilio-specific code and configuration
- Implemented proper webhook verification for Cloud API
- Updated EmergencyContactService to use WhatsAppService
- Added `RoomNumber` column to `Bookings` table
- Updated all response methods to include specific room numbers
- Enhanced notification system with room-specific messaging
- Updated AI prompt templates for room number inclusion