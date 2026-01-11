#!/bin/bash

# Test all 6 WhatsApp templates for Test Hotel
# Booking ID: 62
# Guest: Test Guest
# Room: 202
# Phone: +27783776207

TOKEN="EAAQh0xh1ugoBQbvOIbv6NkqboyZAZCTKSCJIOZCP2hxP2rInUCbxySRB0ZCQwy4CQWTi61t3ckitGq90ACDD5UyrLYLJ4ckwwYFrF2AZCBkLqWQu3ZA4NHRpe74dixRhXN7kZB8hgqMGipiPtce9BppTbTXj9zSxpQ1CCIHWRLrnsZByMVrMKsM7PSuODGy7vpPZAQwZDZD"
PHONE_ID="786143751256015"
TO="+27783776207"

echo "======================================================================"
echo "Testing WhatsApp Templates for Test Hotel"
echo "Booking ID: 62 | Guest: Test Guest | Room: 202"
echo "Phone: $TO"
echo "======================================================================"

# Template 1: Pre-Arrival Welcome
echo ""
echo "1️⃣  Sending: pre_arrival_welcome_v04 (Pre-Arrival)"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"prepare"}' | base64)
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
            {"type": "text", "text": "Test"},
            {"type": "text", "text": "Test Hotel"},
            {"type": "text", "text": "202"},
            {"type": "text", "text": "Sunday, January 12 at 3:00 PM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"'

sleep 3

# Template 2: Check-in Day Ready
echo ""
echo "2️⃣  Sending: checkin_day_ready_v04 (Check-in Day)"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"checkin"}' | base64)
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
            {"type": "text", "text": "Test"},
            {"type": "text", "text": "202"},
            {"type": "text", "text": "Test Hotel"},
            {"type": "text", "text": "3:00 PM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"'

sleep 3

# Template 3: Welcome Settled
echo ""
echo "3️⃣  Sending: welcome_settled_v05 (After Check-in)"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"services"}' | base64)
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
            {"type": "text", "text": "202"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"'

sleep 3

# Template 4: Mid-Stay Check-up
echo ""
echo "4️⃣  Sending: mid_stay_checkup_v04 (Mid-Stay)"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"housekeeping"}' | base64)
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
            {"type": "text", "text": "Test"},
            {"type": "text", "text": "Test Hotel"},
            {"type": "text", "text": "202"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"'

sleep 3

# Template 5: Pre-Checkout Reminder (with booking ID)
echo ""
echo "5️⃣  Sending: pre_checkout_reminder_v03 (Pre-Checkout) - NEW with Booking ID!"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"checkout?booking=62"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"checkout?booking=62"}' | base64)
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
            {"type": "text", "text": "Test"},
            {"type": "text", "text": "Test Hotel"},
            {"type": "text", "text": "202"},
            {"type": "text", "text": "11:00 AM"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"' || echo "⏳ Template still in review - will work once approved"

sleep 3

# Template 6: Post-Stay Survey (with booking ID)
echo ""
echo "6️⃣  Sending: post_stay_survey_v03 (Post-Stay)"
REDIRECT_TOKEN=$(echo -n '{"t":"test-hotel","p":"feedback?booking=62"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"test-hotel","p":"feedback?booking=62"}' | base64)
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
            {"type": "text", "text": "Test"},
            {"type": "text", "text": "Test Hotel"},
            {"type": "text", "text": "202"}
          ]
        },
        {
          "type": "button",
          "sub_type": "url",
          "index": "0",
          "parameters": [
            {"type": "text", "text": "'$REDIRECT_TOKEN'"}
          ]
        }
      ]
    }
  }' | jq -r '.messages[0].id // "FAILED"'

echo ""
echo "======================================================================"
echo "✅ All templates sent!"
echo "======================================================================"
echo ""
echo "📱 Check your WhatsApp (+27783776207) for 6 messages"
echo ""
echo "Button redirects will go to:"
echo "  1. https://test-hotel.staybot.co.za/prepare"
echo "  2. https://test-hotel.staybot.co.za/checkin"
echo "  3. https://test-hotel.staybot.co.za/services"
echo "  4. https://test-hotel.staybot.co.za/housekeeping"
echo "  5. https://test-hotel.staybot.co.za/checkout?booking=62"
echo "  6. https://test-hotel.staybot.co.za/feedback?booking=62"
echo ""
echo "All redirects via: https://api.staybot.co.za/g/{token}"
echo ""
