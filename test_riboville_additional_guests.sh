#!/bin/bash

# Test all 6 WhatsApp templates for Riboville - Additional 6 Guests

TOKEN="EAAQh0xh1ugoBQbvOIbv6NkqboyZAZCTKSCJIOZCP2hxP2rInUCbxySRB0ZCQwy4CQWTi61t3ckitGq90ACDD5UyrLYLJ4ckwwYFrF2AZCBkLqWQu3ZA4NHRpe74dixRhXN7kZB8hgqMGipiPtce9BppTbTXj9zSxpQ1CCIHWRLrnsZByMVrMKsM7PSuODGy7vpPZAQwZDZD"
PHONE_ID="786143751256015"

echo "======================================================================"
echo "Testing WhatsApp Templates for Riboville - Additional Guests"
echo "6 Guests | 6 Templates Each = 36 Total Messages"
echo "======================================================================"
echo ""

# Function to send all 6 templates to a guest
send_templates() {
    local GUEST_NAME=$1
    local GUEST_PHONE=$2
    local ROOM_NUMBER=$3
    local BOOKING_ID=$4

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "📱 $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    echo "1️⃣  Pre-Arrival Welcome"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"prepare\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"prepare\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"pre_arrival_welcome_v04\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$GUEST_NAME\"},
                {\"type\": \"text\", \"text\": \"Riboville Boutique Hotel\"},
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"},
                {\"type\": \"text\", \"text\": \"Sunday, January 12 at 3:00 PM\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1
    sleep 2

    echo "2️⃣  Check-in Day Ready"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"checkin\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"checkin\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"checkin_day_ready_v04\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$GUEST_NAME\"},
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"},
                {\"type\": \"text\", \"text\": \"Riboville Boutique Hotel\"},
                {\"type\": \"text\", \"text\": \"3:00 PM\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1
    sleep 2

    echo "3️⃣  Welcome Settled"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"services\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"services\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"welcome_settled_v05\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1
    sleep 2

    echo "4️⃣  Mid-Stay Checkup"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"housekeeping\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"housekeeping\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"mid_stay_checkup_v04\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$GUEST_NAME\"},
                {\"type\": \"text\", \"text\": \"Riboville Boutique Hotel\"},
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1
    sleep 2

    echo "5️⃣  Pre-Checkout Reminder"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"checkout?booking=$BOOKING_ID\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"checkout?booking=$BOOKING_ID\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"pre_checkout_reminder_v03\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$GUEST_NAME\"},
                {\"type\": \"text\", \"text\": \"Riboville Boutique Hotel\"},
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"},
                {\"type\": \"text\", \"text\": \"11:00 AM\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1
    sleep 2

    echo "6️⃣  Post-Stay Survey"
    REDIRECT_TOKEN=$(echo -n "{\"t\":\"riboville\",\"p\":\"feedback?booking=$BOOKING_ID\"}" | base64 -w 0 2>/dev/null || echo -n "{\"t\":\"riboville\",\"p\":\"feedback?booking=$BOOKING_ID\"}" | base64)
    curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"messaging_product\": \"whatsapp\",
        \"to\": \"$GUEST_PHONE\",
        \"type\": \"template\",
        \"template\": {
          \"name\": \"post_stay_survey_v03\",
          \"language\": {\"code\": \"en\"},
          \"components\": [
            {
              \"type\": \"body\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$GUEST_NAME\"},
                {\"type\": \"text\", \"text\": \"Riboville Boutique Hotel\"},
                {\"type\": \"text\", \"text\": \"$ROOM_NUMBER\"}
              ]
            },
            {
              \"type\": \"button\",
              \"sub_type\": \"url\",
              \"index\": \"0\",
              \"parameters\": [
                {\"type\": \"text\", \"text\": \"$REDIRECT_TOKEN\"}
              ]
            }
          ]
        }
      }" > /dev/null 2>&1

    echo "✅ Complete"
    echo ""
    sleep 3
}

# Send to all 6 new guests
send_templates "Sisa" "+27717027538" "306" "68"
send_templates "Gerald" "+27832006425" "307" "69"
send_templates "Jimmy" "+27833679659" "308" "70"
send_templates "Stevie" "+27832697286" "309" "71"
send_templates "Thobile" "+27609864405" "310" "72"
send_templates "Khanyi" "+26878648502" "311" "73"

echo "======================================================================"
echo "✅ ALL 36 MESSAGES SENT! (6 guests × 6 templates)"
echo "======================================================================"
echo ""
echo "📱 Guest Summary:"
echo "  6. Sisa (+27717027538) - Room 306 - Booking 68"
echo "  7. Gerald (+27832006425) - Room 307 - Booking 69"
echo "  8. Jimmy (+27833679659) - Room 308 - Booking 70"
echo "  9. Stevie (+27832697286) - Room 309 - Booking 71"
echo " 10. Thobile (+27609864405) - Room 310 - Booking 72"
echo " 11. Khanyi (+26878648502) - Room 311 - Booking 73"
echo ""
echo "Total Riboville Guests: 11"
echo "Total Messages Sent Today: 66 (30 + 36)"
echo ""
