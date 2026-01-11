#!/bin/bash

# Test all 6 WhatsApp templates for Riboville - Mabetlela & Karabo

TOKEN="EAAQh0xh1ugoBQbvOIbv6NkqboyZAZCTKSCJIOZCP2hxP2rInUCbxySRB0ZCQwy4CQWTi61t3ckitGq90ACDD5UyrLYLJ4ckwwYFrF2AZCBkLqWQu3ZA4NHRpe74dixRhXN7kZB8hgqMGipiPtce9BppTbTXj9zSxpQ1CCIHWRLrnsZByMVrMKsM7PSuODGy7vpPZAQwZDZD"
PHONE_ID="786143751256015"

echo "======================================================================"
echo "Testing WhatsApp Templates for Riboville - Mabetlela & Karabo"
echo "2 Guests | 6 Templates Each = 12 Total Messages"
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

# Send to Mabetlela and Karabo
send_templates "Mabetlela" "+27723495123" "101" "79"
send_templates "Karabo" "+27813225198" "102" "80"

echo "======================================================================"
echo "✅ ALL 12 MESSAGES SENT! (2 guests × 6 templates)"
echo "======================================================================"
echo ""
echo "📱 Guest Summary:"
echo " 17. Mabetlela (+27723495123) - Room 101 - Booking 79"
echo " 18. Karabo (+27813225198) - Room 102 - Booking 80"
echo ""
echo "Total Messages: 12 (6 templates × 2 guests)"
echo ""
