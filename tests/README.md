# Hostr Survey System - Comprehensive Testing Suite

This comprehensive testing suite covers the complete automated survey system implementation in the Hostr hotel management API.

## Test Structure

```
tests/
├── Infrastructure/           # Base test classes and fixtures
│   ├── TestDatabaseFixture.cs     # In-memory database setup
│   └── BaseTest.cs                # Base test class with common utilities
├── Builders/                # Test data builders for consistent test data
│   ├── BookingBuilder.cs          # Booking entity builder
│   ├── PostStaySurveyBuilder.cs   # Survey entity builder
│   ├── GuestBusinessMetricsBuilder.cs  # Metrics entity builder
│   └── TenantBuilder.cs           # Tenant entity builder
├── UnitTests/               # Unit tests for individual components
│   ├── Services/
│   │   ├── SurveyOrchestrationServiceTests.cs
│   │   └── GuestLifecycleServiceTests.cs
│   ├── Controllers/
│   │   └── AnalyticsControllerTests.cs
│   └── Jobs/
│       ├── SurveyProcessingJobTests.cs
│       └── GuestMetricsJobTests.cs
├── IntegrationTests/        # End-to-end workflow tests
│   └── SurveyWorkflowIntegrationTests.cs
├── PerformanceTests/        # Performance and scalability tests
│   └── SurveyPerformanceTests.cs
├── Mocks/                   # Mock implementations for external services
│   ├── MockWhatsAppService.cs
│   └── MockRatingService.cs
├── TestHelpers/             # Test utilities and data seeders
│   └── TestDataSeeder.cs
└── README.md               # This documentation
```

## Test Coverage

### 1. **SurveyOrchestrationService Tests**
**File**: `UnitTests/Services/SurveyOrchestrationServiceTests.cs`

Tests the core survey automation logic including:
- ✅ Survey eligibility determination (`ShouldSendSurveyAsync`)
- ✅ Opt-out handling and staff exclusion
- ✅ Extended stay logic (surveys only on final checkout)
- ✅ Rate limiting and spam prevention
- ✅ Recent survey cooldown periods (7-day window)
- ✅ Rate limiter functionality and concurrency handling
- ✅ Batch processing of eligible checkouts

**Key Test Scenarios**:
- Guest opted out of surveys → No survey sent
- Staff booking → No survey sent
- Missing phone number → No survey sent
- Survey already exists → No survey sent
- Extended stay with continuation → No survey sent
- Extended stay final checkout → Survey sent
- Recent survey to same phone → No survey sent
- Rate limit exceeded → Survey skipped
- Service errors → Graceful error handling

### 2. **GuestLifecycleService Tests**
**File**: `UnitTests/Services/GuestLifecycleServiceTests.cs`

Tests guest business metrics calculations:
- ✅ Guest metrics creation for new guests
- ✅ Guest metrics updates for existing guests
- ✅ Lifetime value accumulation across stays
- ✅ Average satisfaction calculation from ratings and surveys
- ✅ Days since last stay calculation
- ✅ Return likelihood based on NPS scores
- ✅ Repeat guest identification and marking
- ✅ Batch processing of all guest metrics

**Key Test Scenarios**:
- No bookings exist → Early return with logging
- New guest → Creates metrics record
- Existing guest → Updates existing metrics
- Multiple satisfaction sources → Calculates average from ratings and surveys
- NPS score ≥ 7 → Sets WillReturn to true
- NPS score < 7 → Sets WillReturn to false
- Multiple bookings → Marks repeat guest status correctly
- Error handling → Logs errors gracefully

### 3. **AnalyticsController Tests**
**File**: `UnitTests/Controllers/AnalyticsControllerTests.cs`

Tests all new analytics endpoints:
- ✅ `/api/analytics/satisfaction-revenue-correlation`
- ✅ `/api/analytics/guest-segments`
- ✅ `/api/analytics/survey-performance`
- ✅ `/api/analytics/revenue-impact`

**Key Test Scenarios**:
- Unauthorized access (no tenant ID) → Returns 401
- Empty data → Returns appropriate empty responses
- Valid data → Returns correct analytical insights
- Survey funnel metrics → Calculates delivery, open, completion rates
- Performance metrics → Calculates average completion times
- Revenue correlation → Groups guests by satisfaction levels
- Guest segmentation → Categorizes by value and satisfaction
- Error handling → Returns 500 with error logging

### 4. **Background Jobs Tests**
**Files**:
- `UnitTests/Jobs/SurveyProcessingJobTests.cs`
- `UnitTests/Jobs/GuestMetricsJobTests.cs`

Tests Quartz.NET job execution:
- ✅ Service scope creation and disposal
- ✅ Dependency injection in job context
- ✅ Service method invocation
- ✅ Exception handling and logging
- ✅ Resource cleanup (scope disposal)

**Key Test Scenarios**:
- Successful execution → Calls service and logs success
- Service exceptions → Logs errors and continues
- Scope management → Creates and disposes scopes properly
- Multiple executions → Creates new scope for each run

### 5. **Integration Tests**
**File**: `IntegrationTests/SurveyWorkflowIntegrationTests.cs`

Tests complete end-to-end workflows:
- ✅ Checkout → Survey Creation → Completion → Metrics Update
- ✅ Repeat guest workflow with metric accumulation
- ✅ Survey eligibility business rules enforcement
- ✅ Extended stay survey logic
- ✅ Rate limiting and spam prevention

**Key Workflows Tested**:
1. **Complete Survey Workflow**: Checkout → Survey sent → Survey completed → Metrics updated
2. **Repeat Guest Journey**: Multiple stays → Accumulated metrics → Repeat guest marking
3. **Business Rules Enforcement**: Staff exclusion, opt-outs, timing rules
4. **Extended Stay Logic**: Only final checkout gets survey
5. **Anti-Spam Protection**: Recent survey prevents new surveys

### 6. **Performance Tests**
**File**: `PerformanceTests/SurveyPerformanceTests.cs`

Tests system performance and scalability:
- ✅ Large volume processing (1000+ bookings)
- ✅ Guest metrics calculation efficiency (500+ guests)
- ✅ Query performance with large datasets
- ✅ Rate limiter concurrency handling
- ✅ Linear scaling verification

**Performance Benchmarks**:
- Survey processing: < 30ms per survey
- Guest metrics update: < 120ms per guest
- Analytics queries: < 2 seconds with 5000+ records
- Rate limiter: Handles 100 concurrent requests < 1 second
- Eligibility checks: < 1 second with 10,000+ existing surveys

## Mock Services

### MockWhatsAppService
**File**: `Mocks/MockWhatsAppService.cs`

Provides controllable WhatsApp service for testing:
- Message sending simulation
- Error injection capabilities
- Delay simulation for performance tests
- Message tracking for verification

### MockRatingService
**File**: `Mocks/MockRatingService.cs`

Provides controllable rating service for testing:
- Survey sending simulation
- Error injection capabilities
- Delay simulation
- Operation tracking

## Test Data Builders

### Fluent Builders for Consistent Test Data
All builders use the Builder pattern for readable, maintainable test data:

```csharp
// Example usage
var booking = new BookingBuilder()
    .WithTenantId(1)
    .WithPhone("+27123456789")
    .WithCheckoutRecentlyEligibleForSurvey()
    .WithTotalRevenue(2500.00m)
    .Build();

var survey = new PostStaySurveyBuilder()
    .WithBooking(booking)
    .WithHighSatisfaction()
    .WithCompleted(DateTime.UtcNow)
    .Build();
```

### Builder Features
- **Default Values**: Sensible defaults for all fields
- **Fluent Interface**: Chainable method calls
- **Convenience Methods**: `WithHighSatisfaction()`, `AsVipGuest()`, etc.
- **Relationship Handling**: Automatic foreign key setup
- **Scenario Support**: Pre-configured common test scenarios

## Running Tests

### All Tests
```bash
cd "C:\Users\Administrator\Downloads\hostr\tests"
dotnet test
```

### By Category
```bash
# Unit tests only
dotnet test --filter "Category!=Performance&Category!=Integration"

# Integration tests only
dotnet test --filter "Category=Integration"

# Performance tests only
dotnet test --filter "Category=Performance"
```

### Specific Test Class
```bash
dotnet test --filter "ClassName=SurveyOrchestrationServiceTests"
```

### Test Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Database

### In-Memory Database
- Uses Entity Framework Core In-Memory provider
- Isolated database per test fixture
- Automatic cleanup between tests
- Fast execution with no external dependencies

### Test Data Management
- `TestDatabaseFixture`: Provides clean database context
- `BaseTest`: Common cleanup and utilities
- `TestDataSeeder`: Comprehensive test data scenarios

## Assertions and Verification

### FluentAssertions
All tests use FluentAssertions for readable assertions:
```csharp
result.Should().BeTrue();
metrics.LifetimeValue.Should().Be(2500.00m);
surveys.Should().HaveCount(1);
```

### Logging Verification
Custom helper for verifying log messages:
```csharp
VerifyLogging(_mockLogger, LogLevel.Information, "Survey sent successfully");
```

### Mock Verification
Moq verification for service interactions:
```csharp
_mockRatingService.Verify(x => x.SendPostStaySurveyAsync(1), Times.Once);
```

## Best Practices Implemented

### Test Organization
- ✅ Clear test categorization (Unit, Integration, Performance)
- ✅ Descriptive test names following Given-When-Then pattern
- ✅ Logical grouping by system component
- ✅ Separate concerns (unit vs integration vs performance)

### Test Data Management
- ✅ Builder pattern for maintainable test data
- ✅ Isolated test data per test
- ✅ Realistic test scenarios
- ✅ Performance test data scaling

### Error Testing
- ✅ Exception handling verification
- ✅ Logging verification
- ✅ Graceful degradation testing
- ✅ Resource cleanup verification

### Performance Testing
- ✅ Scalability verification
- ✅ Response time benchmarks
- ✅ Concurrency testing
- ✅ Linear scaling validation

## Business Logic Coverage

### Survey Automation Rules
- ✅ Timing windows (2-4 hours post-checkout)
- ✅ Guest preferences (opt-out handling)
- ✅ Staff exclusion
- ✅ Extended stay logic
- ✅ Anti-spam protection (7-day cooldown)
- ✅ Rate limiting (20 messages/minute)

### Guest Metrics Calculations
- ✅ Lifetime value accumulation
- ✅ Stay counting and repeat guest identification
- ✅ Satisfaction averaging across sources
- ✅ Return likelihood prediction (NPS-based)
- ✅ Recency tracking (days since last stay)

### Analytics Accuracy
- ✅ Revenue correlation calculations
- ✅ Guest segmentation algorithms
- ✅ Survey performance metrics
- ✅ Funnel analysis (sent → delivered → opened → completed)
- ✅ Business impact projections

## Deployment and CI/CD

### Test Execution in Pipeline
```yaml
# Azure DevOps Pipeline Example
- task: DotNetCoreCLI@2
  displayName: 'Run Unit Tests'
  inputs:
    command: 'test'
    projects: 'tests/Hostr.Tests.csproj'
    arguments: '--filter "Category!=Performance" --collect:"XPlat Code Coverage"'

- task: DotNetCoreCLI@2
  displayName: 'Run Performance Tests'
  inputs:
    command: 'test'
    projects: 'tests/Hostr.Tests.csproj'
    arguments: '--filter "Category=Performance"'
  condition: eq(variables['Build.SourceBranch'], 'refs/heads/main')
```

### Coverage Requirements
- Minimum 80% code coverage for services
- 100% coverage for critical business logic
- Performance benchmarks must pass
- All integration tests must pass

This comprehensive testing suite ensures the reliability, performance, and correctness of the automated survey system, providing confidence in the business-critical survey automation and guest analytics functionality.