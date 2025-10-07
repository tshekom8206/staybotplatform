# Hostr Backend

Production-ready monorepo backend for Hostr, a multi-tenant WhatsApp concierge for hotels/BnBs in South Africa.

## 🏗️ Architecture

**Stack**: .NET 8 (ASP.NET Core Web API + SignalR), EF Core, PostgreSQL (pgvector + pg_trgm), Redis, OpenAI

**Structure**:
```
hostr/
├── apps/
│   ├── api/                 # ASP.NET Core Web API + SignalR
│   └── workers/             # Background services (embeddings, ratings, etc.)
├── packages/
│   ├── contracts/           # Shared DTOs and contracts
│   └── prompts/             # LLM system & few-shot prompts
├── tests/                   # Unit & integration tests
├── infra/
│   └── docker-compose.yml   # Redis + MailHog
└── azure-pipelines.yml      # CI/CD pipeline
```

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- PostgreSQL with `pgvector` and `pg_trgm` extensions
- Redis (via Docker or local install)
- Your OpenAI API key

### 1. Database Setup
```bash
# Create database
createdb hostr

# Enable extensions
psql hostr -c "CREATE EXTENSION IF NOT EXISTS vector;"
psql hostr -c "CREATE EXTENSION IF NOT EXISTS pg_trgm;"

# Run schema and seed
psql hostr -f apps/api/Data/Migrations/CreateInitialSchema.sql
psql hostr -f apps/api/Data/SeedData.sql
```

### 2. Configuration
Update `apps/api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=hostr;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key"
  },
  "WhatsApp": {
    "VerifyToken": "your-webhook-verify-token"
  }
}
```

### 3. Start Services
```bash
# Start Redis
cd infra && docker-compose up -d

# Start API
cd apps/api && dotnet run

# Start Workers (in another terminal)
cd apps/workers && dotnet run
```

## 🌐 Multi-Tenant Architecture

**Tenant Resolution**:
- Subdomain: `panoramaview.hostr.co.za` → tenant slug `panoramaview`
- Query param: `/api/auth/login?tenantSlug=panoramaview`
- Development: `X-Tenant` header

**Data Isolation**:
- EF Core global query filters enforce `TenantId` filtering
- All tenant-scoped entities automatically filtered by resolved tenant

## 🔐 Authentication & Authorization

**JWT-based** with tenant-aware claims:
- `sub`: User ID
- `tid`: Tenant ID  
- `role`: Owner|Manager|Agent|SuperAdmin
- `plan`: Basic|Standard|Premium

**Roles**:
- **Owner**: Full tenant management
- **Manager**: Tenant operations, user management
- **Agent**: Daily operations, no admin
- **SuperAdmin**: Cross-tenant admin access

## 📱 WhatsApp Integration

### Webhook Endpoints
```
GET  /webhook              # Verification
POST /webhook              # Message processing
```

### Message Routing Pipeline
1. **Rules Check**: Emergency/handover keywords
2. **FAQ Trigram**: PostgreSQL trigram similarity ≥ 0.82
3. **Semantic FAQ**: OpenAI embeddings similarity ≥ 0.85  
4. **RAG + LLM**: pgvector cosine similarity ≥ 0.6
5. **Clarify**: Fallback response

### Room Service Integration
LLM can create tasks with structured actions:
```json
{
  "reply": "✅ Two towels will be sent to room 12 shortly.",
  "action": {
    "type": "create_task", 
    "task": {
      "item_slug": "towels",
      "quantity": 2,
      "room_number": "12"
    }
  }
}
```

## 🎯 Premium Features (Plan-Gated)

**Standard/Premium**: All basic features + advanced analytics
**Premium Only**:
- ✅ Stock management & tracking
- ✅ Collection workflows
- ✅ Inventory in LLM context
- ✅ Advanced inventory reports

Server-side enforcement returns `403` with:
```json
{
  "code": "plan_required",
  "plan": "Premium",
  "current_plan": "Standard"
}
```

## 📊 Background Workers

**Quartz.NET Jobs**:
- **EmbeddingsWorker**: Re-embed FAQs/KB chunks (hourly)
- **RatingsScheduler**: Send checkout rating requests (15min)
- **RetentionWorker**: POPIA-compliant data purging (daily)
- **AnalyticsRollupWorker**: Daily usage aggregation (daily)

## 🛡️ Observability

**Health Checks**: `/health`
- PostgreSQL connectivity
- Redis connectivity  
- WhatsApp API reachability

**Logging**: Serilog with structured JSON
- Request/response correlation IDs
- Tenant context in all logs
- Error tracking and alerting ready

## 🗂️ Database Schema

**Core Tables**:
- `Tenants`, `AspNetUsers`, `UserTenants` (multi-tenancy)
- `Conversations`, `Messages` (WhatsApp data)
- `FAQs`, `KnowledgeBaseChunks` (content + embeddings)
- `RequestItems`, `StaffTasks`, `StockEvents` (room service)
- `Bookings`, `Ratings` (guest feedback)
- `UsageDaily`, `AuditLogs` (analytics & compliance)

**Key Indexes**:
- Trigram on FAQ questions: `gin_trgm_ops`
- Vector similarity: `ivfflat` with `vector_cosine_ops`
- Tenant + time-series optimized

## 🧪 Testing

```bash
# Unit tests
dotnet test

# Integration tests with TestContainers
dotnet test --logger trx --collect:"XPlat Code Coverage"
```

## 🚢 Deployment

**Azure DevOps Pipeline**:
- Build & test on commits
- Publish artifacts (API + Workers)
- Deploy to Azure App Service
- Database migrations

**Environment Variables**:
```bash
ConnectionStrings__Default=...
OpenAI__ApiKey=...
WhatsApp__VerifyToken=...
Redis__Connection=...
```

## 📋 API Endpoints

### Public
- `GET  /webhook` - WhatsApp verification
- `POST /webhook` - WhatsApp messages
- `GET  /health` - Health check

### Auth
- `POST /api/auth/login?tenantSlug=...` - Login
- `POST /api/auth/switch-tenant` - Switch tenant context

### Tenant Operations  
- `GET  /api/tenant` - Tenant info
- `GET  /api/conversations` - Message history
- `POST /api/conversations/{id}/reply` - Send reply
- `GET  /api/faqs` - FAQ management
- `GET  /api/tasks` - Staff tasks
- `GET  /api/ratings/overview` - CSAT analytics

### Premium Only
- `POST /api/request-items/{id}/adjust-stock` - Inventory
- `POST /api/tasks/{id}/status` - Collection workflows

## 🌍 Demo Tenant

**Slug**: `panoramaview` (Panorama View Hotel)
**Plan**: Standard
**Includes**: Sample FAQs, guide items, room service items, bookings

## 🔧 Development

```bash
# API
cd apps/api
dotnet watch run

# Workers  
cd apps/workers
dotnet watch run

# Tests
dotnet watch test
```

## ✅ Acceptance Criteria

- [x] Subdomain → tenant resolved; queries filtered by tenant
- [x] POST /webhook persists inbound, routes, and replies  
- [x] FAQ/RAG thresholds implemented as specified
- [x] POST /api/tasks works; stock/collect enforced Premium-only
- [x] Ratings: scheduler sends ask; webhook records; overview computes avg
- [x] Health endpoint green with PostgreSQL + Redis
- [x] Migrations create pgvector & pg_trgm; seed script runs
- [x] Premium plan gating enforced server-side
- [x] Background workers process embeddings, ratings, retention
- [x] Authentication with JWT and tenant-aware claims

---

**Built for South African hospitality** 🇿🇦 | **Production-ready** ⚡ | **Multi-tenant** 🏢