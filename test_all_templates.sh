#!/bin/bash

# Test all 6 WhatsApp templates with button redirects

TOKEN="EAAQh0xh1ugoBQbvOIbv6NkqboyZAZCTKSCJIOZCP2hxP2rInUCbxySRB0ZCQwy4CQWTi61t3ckitGq90ACDD5UyrLYLJ4ckwwYFrF2AZCBkLqWQu3ZA4NHRpe74dixRhXN7kZB8hgqMGipiPtce9BppTbTXj9zSxpQ1CCIHWRLrnsZByMVrMKsM7PSuODGy7vpPZAQwZDZD"
PHONE_ID="786143751256015"
TO="27783776207"

echo "Testing all 6 WhatsApp templates..."
echo "========================================"

# Template 1: pre_arrival_welcome_v04
echo ""
echo "1. Testing pre_arrival_welcome_v04..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "Tsheko"},
            {"type": "text", "text": "Dakarai B&B"},
            {"type": "text", "text": "101"},
            {"type": "text", "text": "Friday, March 15 at 2:00 PM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJwcmVwYXJlIn0="}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

sleep 2

# Template 2: checkin_day_ready_v04
echo ""
echo "2. Testing checkin_day_ready_v04..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "Tsheko"},
            {"type": "text", "text": "101"},
            {"type": "text", "text": "Dakarai B&B"},
            {"type": "text", "text": "2:00 PM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJjaGVja2luIn0="}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

sleep 2

# Template 3: welcome_settled_v05
echo ""
echo "3. Testing welcome_settled_v05..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "101"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJzZXJ2aWNlcyJ9"}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

sleep 2

# Template 4: mid_stay_checkup_v04
echo ""
echo "4. Testing mid_stay_checkup_v04..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "Tsheko"},
            {"type": "text", "text": "Dakarai B&B"},
            {"type": "text", "text": "101"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJob3VzZWtlZXBpbmcifQ=="}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

sleep 2

# Template 5: pre_checkout_reminder_v03
echo ""
echo "5. Testing pre_checkout_reminder_v03..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "Tsheko"},
            {"type": "text", "text": "Dakarai B&B"},
            {"type": "text", "text": "101"},
            {"type": "text", "text": "11:00 AM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJjaGVja291dCJ9"}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

sleep 2

# Template 6: post_stay_survey_v03
echo ""
echo "6. Testing post_stay_survey_v03..."
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$TO'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "Dakarai B&B"},
            {"type": "text", "text": "101"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "eyJ0IjoiZGFrYXJhaSIsInAiOiJmZWVkYmFjay8xMjMifQ=="}
          ]
        }
      ]
    }
  }' | grep -o '"message_status":"[^"]*"' || echo "FAILED"

echo ""
echo "========================================"
echo "All tests complete!"
echo "Check your WhatsApp for 6 messages."
echo ""
echo "Button redirects should go to:"
echo "1. https://dakarai.staybot.co.za/prepare"
echo "2. https://dakarai.staybot.co.za/checkin"
echo "3. https://dakarai.staybot.co.za/services"
echo "4. https://dakarai.staybot.co.za/housekeeping"
echo "5. https://dakarai.staybot.co.za/checkout"
echo "6. https://dakarai.staybot.co.za/feedback/123"
