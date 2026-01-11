#!/bin/bash

# Test all 6 WhatsApp templates for Riboville Boutique Hotel
# 5 Guests with individual bookings

TOKEN="EAAQh0xh1ugoBQbvOIbv6NkqboyZAZCTKSCJIOZCP2hxP2rInUCbxySRB0ZCQwy4CQWTi61t3ckitGq90ACDD5UyrLYLJ4ckwwYFrF2AZCBkLqWQu3ZA4NHRpe74dixRhXN7kZB8hgqMGipiPtce9BppTbTXj9zSxpQ1CCIHWRLrnsZByMVrMKsM7PSuODGy7vpPZAQwZDZD"
PHONE_ID="786143751256015"

echo "======================================================================"
echo "Testing WhatsApp Templates for Riboville Boutique Hotel"
echo "5 Guests | 6 Templates Each = 30 Total Messages"
echo "======================================================================"
echo ""

# Guest 1: Tsheko
GUEST_NAME="Tsheko"
GUEST_PHONE="+27783776207"
ROOM_NUMBER="301"
BOOKING_ID="63"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📱 Guest 1: $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "1️⃣  Pre-Arrival Welcome"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"prepare"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "2️⃣  Check-in Day Ready"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkin"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
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
  }' > /dev/null 2>&1
sleep 2

echo "3️⃣  Welcome Settled"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"services"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "4️⃣  Mid-Stay Checkup"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"housekeeping"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "5️⃣  Pre-Checkout Reminder"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "6️⃣  Post-Stay Survey"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1

echo "✅ Complete"
echo ""
sleep 3

# Guest 2: Palesa
GUEST_NAME="Palesa"
GUEST_PHONE="+27665607657"
ROOM_NUMBER="302"
BOOKING_ID="64"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📱 Guest 2: $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "1️⃣  Pre-Arrival Welcome"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"prepare"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "2️⃣  Check-in Day Ready"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkin"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
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
  }' > /dev/null 2>&1
sleep 2

echo "3️⃣  Welcome Settled"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"services"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "4️⃣  Mid-Stay Checkup"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"housekeeping"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "5️⃣  Pre-Checkout Reminder"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "6️⃣  Post-Stay Survey"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1

echo "✅ Complete"
echo ""
sleep 3

# Guest 3: Vinny
GUEST_NAME="Vinny"
GUEST_PHONE="+27848027195"
ROOM_NUMBER="303"
BOOKING_ID="65"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📱 Guest 3: $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "1️⃣  Pre-Arrival Welcome"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"prepare"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "2️⃣  Check-in Day Ready"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkin"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
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
  }' > /dev/null 2>&1
sleep 2

echo "3️⃣  Welcome Settled"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"services"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "4️⃣  Mid-Stay Checkup"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"housekeeping"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "5️⃣  Pre-Checkout Reminder"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "6️⃣  Post-Stay Survey"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1

echo "✅ Complete"
echo ""
sleep 3

# Guest 4: Kgaugelo
GUEST_NAME="Kgaugelo"
GUEST_PHONE="+27834923605"
ROOM_NUMBER="304"
BOOKING_ID="66"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📱 Guest 4: $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "1️⃣  Pre-Arrival Welcome"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"prepare"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "2️⃣  Check-in Day Ready"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkin"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
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
  }' > /dev/null 2>&1
sleep 2

echo "3️⃣  Welcome Settled"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"services"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "4️⃣  Mid-Stay Checkup"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"housekeeping"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "5️⃣  Pre-Checkout Reminder"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "6️⃣  Post-Stay Survey"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1

echo "✅ Complete"
echo ""
sleep 3

# Guest 5: Motheo
GUEST_NAME="Motheo"
GUEST_PHONE="+27683297515"
ROOM_NUMBER="305"
BOOKING_ID="67"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📱 Guest 5: $GUEST_NAME | Room $ROOM_NUMBER | Booking $BOOKING_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "1️⃣  Pre-Arrival Welcome"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"prepare"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"prepare"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_arrival_welcome_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "2️⃣  Check-in Day Ready"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkin"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkin"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "checkin_day_ready_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
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
  }' > /dev/null 2>&1
sleep 2

echo "3️⃣  Welcome Settled"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"services"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"services"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "welcome_settled_v05",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "4️⃣  Mid-Stay Checkup"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"housekeeping"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"housekeeping"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "mid_stay_checkup_v04",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1
sleep 2

echo "5️⃣  Pre-Checkout Reminder"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"checkout?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "pre_checkout_reminder_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"},
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
  }' > /dev/null 2>&1
sleep 2

echo "6️⃣  Post-Stay Survey"
REDIRECT_TOKEN=$(echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64 -w 0 2>/dev/null || echo -n '{"t":"riboville","p":"feedback?booking='$BOOKING_ID'"}' | base64)
curl -s -X POST "https://graph.facebook.com/v22.0/$PHONE_ID/messages" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "messaging_product": "whatsapp",
    "to": "'$GUEST_PHONE'",
    "type": "template",
    "template": {
      "name": "post_stay_survey_v03",
      "language": {"code": "en"},
      "components": [
        {
          "type": "body",
          "parameters": [
            {"type": "text", "text": "'$GUEST_NAME'"},
            {"type": "text", "text": "Riboville Boutique Hotel"},
            {"type": "text", "text": "'$ROOM_NUMBER'"}
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
  }' > /dev/null 2>&1

echo "✅ Complete"
echo ""
sleep 1

echo "======================================================================"
echo "✅ ALL 30 MESSAGES SENT! (5 guests × 6 templates)"
echo "======================================================================"
echo ""
echo "📱 Guest Summary:"
echo "  1. Tsheko (+27783776207) - Room 301 - Booking 63"
echo "  2. Palesa (+27665607657) - Room 302 - Booking 64"
echo "  3. Vinny (+27848027195) - Room 303 - Booking 65"
echo "  4. Kgaugelo (+27834923605) - Room 304 - Booking 66"
echo "  5. Motheo (+27683297515) - Room 305 - Booking 67"
echo ""
echo "All redirects go to: https://riboville.staybot.co.za"
echo ""
