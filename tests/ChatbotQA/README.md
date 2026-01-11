# StayBot Chatbot Quality Assurance Framework

LLM-as-a-judge testing framework for evaluating chatbot responses against ground truth data.

## Setup

### 1. Set OpenAI API Key

Choose one of these methods:

**Option A: Environment Variable**
```bash
# Windows
setx OPENAI_API_KEY "your-api-key-here"

# Linux/Mac
export OPENAI_API_KEY="your-api-key-here"
```

**Option B: User Secrets** (Recommended for development)
```bash
cd tests
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key-here"
```

### 2. Optional: Set Model
```bash
# Use gpt-3.5-turbo (faster, cheaper) - default
setx OPENAI_MODEL "gpt-3.5-turbo"

# Use gpt-4 (more accurate, expensive)
setx OPENAI_MODEL "gpt-4"
```

## Running Tests

### Run All QA Tests
```bash
cd tests
dotnet test --filter "FullyQualifiedName~ChatbotQA"
```

### Run Specific Test Classes
```bash
# Hallucination detection tests
dotnet test --filter "FullyQualifiedName~HallucinationTests"

# Policy compliance tests
dotnet test --filter "FullyQualifiedName~PolicyComplianceTests"

# Full suite evaluation
dotnet test --filter "FullyQualifiedName~FullSuiteTests"
```

### Run Single Test
```bash
dotnet test --filter "ChargerHallucination_ShouldBeDetected"
```

## Test Cases

Test cases are stored in `Data/eval_cases.jsonl` (JSON Lines format - one test case per line).

### Test Case Format
```json
{
  "case_id": "T-001",
  "guest_message": "Can I get dinner at 22:30?",
  "chatbot_response": "Sure, I will arrange dinner now.",
  "hotel_data": {
    "policies": ["Dinner service hours: 18:00–21:00"],
    "kb_snippets": ["Late night options: cold platters only"],
    "retrieved_context": "Kitchen closes at 21:00"
  },
  "locale": "en-ZA",
  "expected_notes": "Should decline hot dinner after 21:00",
  "tags": ["policy_hours", "policy_violation"]
}
```

### Evaluation Criteria

Each response is scored on 5 criteria (0-1 scale):

1. **Understanding**: Did it interpret the request correctly?
2. **Accuracy**: Is it factually correct vs ground truth?
3. **Completeness**: Did it include needed details or ask clarifying questions?
4. **Policy Compliance**: Did it respect hours, fees, limits?
5. **Tone**: Polite, helpful, on-brand?

### Verdict Thresholds

- **accurate**: Average score ≥ 0.85, no hard violations
- **partial**: Average score 0.6-0.84, minor issues
- **inaccurate**: Score < 0.6 OR policy breach OR hallucination

## Test Tags

Use tags to organize and filter test cases:

- `hallucination` - Detecting fake/invented information
- `policy_hours` - Service hour restrictions
- `policy_violation` - Policy breaches
- `request_item` - Item requests (towels, chargers, etc.)
- `charger` - Charger-specific tests
- `spa` - Spa service tests
- `dining` - Restaurant/dining tests
- `lost_and_found` - Lost item handling
- `faq_basic` - Basic FAQ responses
- `upsell_price` - Pricing accuracy
- `clarification` - Ambiguous request handling

## Adding New Test Cases

### Method 1: Add to JSONL File
Add a new line to `Data/eval_cases.jsonl`:
```json
{"case_id":"T-NEW","guest_message":"...","chatbot_response":"...","hotel_data":{...},"tags":["..."]}
```

### Method 2: Programmatically in Tests
```csharp
var testCase = new TestCase
{
    CaseId = "T-NEW",
    GuestMessage = "I need a charger",
    ChatbotResponse = "We have USB, iPhone, Android chargers",
    HotelData = new HotelData
    {
        KbSnippets = new List<string> { "Available chargers: USB, iPhone, Android" }
    },
    Tags = new List<string> { "request_item", "charger" }
};

var result = await Evaluator.EvaluateAsync(testCase);
```

## Understanding Results

### Example Output
```
=== T-004 ===
Verdict: inaccurate
Scores: U=0.95, A=0.10, C=0.70, P=1.00, T=0.95
Average: 0.74
⚠️ HALLUCINATION DETECTED
Issues: Response contains hardcoded charger types not in database; Suggests "Samsung" charger not listed in ground truth
Fix: Query available chargers from RequestItems table and list only those
```

### Metrics Summary
```
Total Cases: 15
Accurate: 5 (33.3%)
Partial: 4 (26.7%)
Inaccurate: 6 (40.0%)
Hard Violations: 6
Hallucinations: 5

Average Scores:
  Understanding: 0.92
  Accuracy: 0.58
  Completeness: 0.78
  Policy Compliance: 0.73
  Tone: 0.94
  Overall: 0.79
```

## Cost Estimation

Using gpt-3.5-turbo:
- ~$0.001 per test case
- 15 test cases ≈ $0.015
- 100 test cases ≈ $0.10

Using gpt-4:
- ~$0.03 per test case
- 15 test cases ≈ $0.45
- 100 test cases ≈ $3.00

## Continuous Integration

Add to CI/CD pipeline:
```yaml
- name: Run Chatbot QA Tests
  run: |
    dotnet test tests/Hostr.Tests.csproj --filter "FullyQualifiedName~ChatbotQA"
  env:
    OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
```

## Hallucination Fix Validation

The test suite includes specific tests to validate our hallucination fixes:

### Test Cases
- **T-004**: Charger request with hardcoded examples (should FAIL - hallucination)
- **T-013**: Charger request with database examples (should PASS - accurate)
- **T-005**: Spa services with hardcoded examples (should FAIL)
- **T-006**: Lost & found with location suggestions (should be flagged)
- **T-012**: Restaurant names invented (should FAIL - severe hallucination)

Run validation:
```bash
dotnet test --filter "ValidateHallucinationFixes"
```

This will compare before/after responses and verify that fixed versions score significantly better.
