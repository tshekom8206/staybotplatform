# CLAUDE.md - Project Configuration & Commands

## Database Connection Information

**IMPORTANT: Always use these exact database connection details:**

- **Database Name**: `hostr`  
- **Username**: `postgres`
- **Password**: `postgres`
- **Host**: `localhost`
- **Port**: `5432`

**Connection String**: `Host=localhost;Database=hostr;Username=postgres;Password=postgres`

## Common Commands

### Database Operations
```bash
# Connect to PostgreSQL
psql -h localhost -U postgres -d hostr

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

## Recent Changes

- Added `RoomNumber` column to `Bookings` table
- Updated all response methods to include specific room numbers
- Enhanced notification system with room-specific messaging
- Updated AI prompt templates for room number inclusion