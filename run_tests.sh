#!/bin/bash

# Start server
cd "/c/Users/Administrator/Downloads/hostr/apps/api"
dotnet run --urls=http://localhost:5000 > /tmp/server_test.log 2>&1 &
SERVER_PID=$!

echo "Waiting for server to start (PID: $SERVER_PID)..."
sleep 25

echo "=========================================="
echo "   PHASE 1 IMPLEMENTATION TESTS"
echo "=========================================="
echo ""

# Test 1: Late Checkout
echo "TEST 1: LATE_CHECKOUT - 'Can I checkout at 2pm?'"
echo "-------------------------------------------"
RESPONSE1=$(curl -s -X POST "http://localhost:5000/api/test/simulate-message" \
  -H "Content-Type: application/json" \
  -d '{"tenantId": 1, "phoneNumber": "+27783776207", "messageText": "Can I checkout at 2pm?"}')
echo "$RESPONSE1" | python -m json.tool 2>/dev/null || echo "$RESPONSE1"
echo ""
echo ""

# Test 2: Emergency Maintenance
echo "TEST 2: EMERGENCY MAINTENANCE - 'I smell gas in my room'"
echo "-------------------------------------------"
RESPONSE2=$(curl -s -X POST "http://localhost:5000/api/test/simulate-message" \
  -H "Content-Type: application/json" \
  -d '{"tenantId": 1, "phoneNumber": "+27783776207", "messageText": "I smell gas in my room"}')
echo "$RESPONSE2" | python -m json.tool 2>/dev/null || echo "$RESPONSE2"
echo ""
echo ""

# Test 3: Urgent Maintenance
echo "TEST 3: URGENT MAINTENANCE - 'Water is leaking from the ceiling'"
echo "-------------------------------------------"
RESPONSE3=$(curl -s -X POST "http://localhost:5000/api/test/simulate-message" \
  -H "Content-Type: application/json" \
  -d '{"tenantId": 1, "phoneNumber": "+27783776207", "messageText": "Water is leaking from the ceiling"}')
echo "$RESPONSE3" | python -m json.tool 2>/dev/null || echo "$RESPONSE3"
echo ""
echo ""

# Test 4: Normal Maintenance
echo "TEST 4: NORMAL MAINTENANCE - 'The light bulb is out'"
echo "-------------------------------------------"
RESPONSE4=$(curl -s -X POST "http://localhost:5000/api/test/simulate-message" \
  -H "Content-Type: application/json" \
  -d '{"tenantId": 1, "phoneNumber": "+27783776207", "messageText": "The light bulb is out"}')
echo "$RESPONSE4" | python -m json.tool 2>/dev/null || echo "$RESPONSE4"
echo ""
echo ""

echo "=========================================="
echo "   ALL TESTS COMPLETE"
echo "=========================================="

# Kill server
kill $SERVER_PID 2>/dev/null
