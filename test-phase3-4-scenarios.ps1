# Test Scenarios for Phase 3 and 4 Enhancements
# Testing Context Processing, Ambiguity Detection, and Human-like Response Generation

$baseUrl = "http://localhost:5000"
$webhookUrl = "$baseUrl/webhook/twilio"
$phoneFrom = "%2B27783776207"
$phoneTo = "%2B14155238886"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "PHASE 3 & 4 ENHANCEMENT TESTING" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Function to send test message
function Test-Message {
    param(
        [string]$Message,
        [string]$Category,
        [string]$Description
    )

    Write-Host "`n[TEST] $Category - $Description" -ForegroundColor Yellow
    Write-Host "Message: '$Message'" -ForegroundColor White

    $encodedMessage = [System.Uri]::EscapeDataString($Message).Replace(" ", "+")
    $body = "From=$phoneFrom&To=$phoneTo&Body=$encodedMessage"

    try {
        $response = Invoke-RestMethod -Uri $webhookUrl -Method Post -ContentType "application/x-www-form-urlencoded" -Body $body -TimeoutSec 10
        Write-Host "✓ Response received" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Error: $_" -ForegroundColor Red
    }

    Start-Sleep -Seconds 2
}

Write-Host "`n=== PHASE 3: CONTEXT PROCESSING TESTS ===" -ForegroundColor Magenta

# Test 1: Semantic Similarity (Synonym Recognition)
Write-Host "`n--- Testing Semantic Similarity ---" -ForegroundColor Cyan
Test-Message "I need my room cleaned" "Semantic Similarity" "Testing 'cleaned' as synonym for housekeeping"
Test-Message "Can you tidy my accommodation?" "Semantic Similarity" "Testing 'tidy' and 'accommodation' synonyms"
Test-Message "Please send someone to maintain my suite" "Semantic Similarity" "Testing 'maintain' and 'suite' synonyms"

# Test 2: Intent Similarity Detection
Write-Host "`n--- Testing Intent Similarity ---" -ForegroundColor Cyan
Test-Message "I want to reserve a table for dinner" "Intent Detection" "Testing booking intent with 'reserve'"
Test-Message "Can I schedule an appointment at the spa?" "Intent Detection" "Testing booking intent with 'schedule'"
Test-Message "Help me book a massage treatment" "Intent Detection" "Testing booking intent with 'book'"

# Test 3: Entity Extraction and Overlap
Write-Host "`n--- Testing Entity Extraction ---" -ForegroundColor Cyan
Test-Message "My room number is 305 and I need towels" "Entity Extraction" "Testing room number extraction"
Test-Message "Call me at 555-1234 when ready" "Entity Extraction" "Testing phone number extraction"
Test-Message "I'll need this by 3:30 PM today" "Entity Extraction" "Testing time extraction"
Test-Message "Room 412 needs service at 2pm tomorrow" "Entity Extraction" "Testing multiple entities"

# Test 4: Ambiguity Detection - Temporal Vague
Write-Host "`n--- Testing Ambiguity Detection: Temporal ---" -ForegroundColor Cyan
Test-Message "I'll need room service later" "Ambiguity - Temporal" "Testing vague time 'later'"
Test-Message "Can you clean my room sometime today?" "Ambiguity - Temporal" "Testing vague time 'sometime'"
Test-Message "Book a spa appointment soonish" "Ambiguity - Temporal" "Testing vague time 'soonish'"

# Test 5: Ambiguity Detection - Missing Context
Write-Host "`n--- Testing Ambiguity Detection: Missing Context ---" -ForegroundColor Cyan
Test-Message "Is it available?" "Ambiguity - Context" "Testing missing context 'it'"
Test-Message "Can I get that?" "Ambiguity - Context" "Testing missing context 'that'"
Test-Message "How much does it cost?" "Ambiguity - Context" "Testing missing cost context"

# Test 6: Ambiguity Detection - Multiple Options
Write-Host "`n--- Testing Ambiguity Detection: Multiple Options ---" -ForegroundColor Cyan
Test-Message "Change my booking" "Ambiguity - Multiple" "Testing ambiguous booking reference"
Test-Message "Cancel my reservation" "Ambiguity - Multiple" "Testing ambiguous reservation"
Test-Message "Update the order" "Ambiguity - Multiple" "Testing ambiguous order reference"

# Test 7: Ambiguity Detection - Impossible Requests
Write-Host "`n--- Testing Ambiguity Detection: Impossible ---" -ForegroundColor Cyan
Test-Message "I want breakfast at midnight" "Ambiguity - Impossible" "Testing service outside hours"
Test-Message "Book spa treatment at 4am" "Ambiguity - Impossible" "Testing unreasonable time"
Test-Message "Reserve restaurant table for 50 people" "Ambiguity - Impossible" "Testing capacity constraint"

Write-Host "`n=== PHASE 4: RESPONSE GENERATION TESTS ===" -ForegroundColor Magenta

# Test 8: Temporal Awareness
Write-Host "`n--- Testing Temporal Awareness ---" -ForegroundColor Cyan
$currentHour = (Get-Date).Hour
if ($currentHour -lt 12) {
    Test-Message "Good morning! What is for breakfast?" "Temporal" "Testing morning context"
} elseif ($currentHour -lt 17) {
    Test-Message "Good afternoon! Any lunch specials?" "Temporal" "Testing afternoon context"
} else {
    Test-Message "Good evening! What time does the bar close?" "Temporal" "Testing evening context"
}

# Test 9: Emotional Context and Empathy
Write-Host "`n--- Testing Emotional Intelligence ---" -ForegroundColor Cyan
Test-Message "This is unacceptable! My room is dirty!" "Emotional - Frustrated" "Testing frustrated guest response"
Test-Message "I am so disappointed with the service" "Emotional - Disappointed" "Testing disappointed guest"
Test-Message "This is fantastic! Best hotel ever!" "Emotional - Excited" "Testing excited guest"
Test-Message "I am worried about my early checkout" "Emotional - Concerned" "Testing concerned guest"

# Test 10: Personality and Tone Variations
Write-Host "`n--- Testing Personality Variations ---" -ForegroundColor Cyan
Test-Message "Hey, can I get some food?" "Personality - Casual" "Testing casual request"
Test-Message "I would like to inquire about dining options" "Personality - Formal" "Testing formal request"
Test-Message "Urgently need medical assistance!" "Personality - Urgent" "Testing urgent tone"

# Test 11: Complex Multi-Intent Scenarios
Write-Host "`n--- Testing Complex Scenarios ---" -ForegroundColor Cyan
Test-Message "I need towels in room 305 and also want to book dinner at 7pm" "Multi-Intent" "Testing multiple requests"
Test-Message "Cancel my spa appointment and book a restaurant table instead" "Multi-Intent" "Testing cancel and book"
Test-Message "My room is too hot, can you fix it and also send up some ice?" "Multi-Intent" "Testing complaint with request"

# Test 12: Follow-up Context
Write-Host "`n--- Testing Follow-up Context ---" -ForegroundColor Cyan
Test-Message "I asked for towels earlier" "Follow-up" "Testing reference to previous request"
Test-Message "Any update on that?" "Follow-up" "Testing vague follow-up"
Test-Message "Never mind my last request" "Follow-up" "Testing cancellation"

# Test 13: Edge Cases and Error Scenarios
Write-Host "`n--- Testing Edge Cases ---" -ForegroundColor Cyan
Test-Message "😊 Hello! 🏨" "Edge Case" "Testing emoji handling"
Test-Message "" "Edge Case" "Testing empty message"
Test-Message "a" "Edge Case" "Testing single character"
Test-Message (("test " * 100)) "Edge Case" "Testing very long message"

# Test 14: Business Rules and Constraints
Write-Host "`n--- Testing Business Rules ---" -ForegroundColor Cyan
Test-Message "Book spa for 10 people at once" "Business Rules" "Testing capacity constraint"
Test-Message "I want to check out at 3am" "Business Rules" "Testing time constraint"
Test-Message "Order 100 towels to my room" "Business Rules" "Testing quantity constraint"

# Test 15: Privacy and Security
Write-Host "`n--- Testing Privacy Handling ---" -ForegroundColor Cyan
Test-Message "What room is John Smith in?" "Privacy" "Testing guest privacy protection"
Test-Message "Who is staying in room 405?" "Privacy" "Testing room privacy"
Test-Message "Give me the guest list" "Privacy" "Testing data protection"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "TEST SUITE COMPLETED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nPlease check the API logs for detailed responses and processing information." -ForegroundColor Yellow