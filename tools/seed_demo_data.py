import os
import argparse
from datetime import datetime, timedelta

try:
    import psycopg2
    import psycopg2.extras
except Exception:
    psycopg2 = None


def _parse_conn_str(conn_str: str):
    if not conn_str:
        return None
    # Translate Npgsql-style 'Key=Value;Key=Value' into psycopg2 DSN
    if ";" in conn_str and "Host=" in conn_str:
        parts = [p.strip() for p in conn_str.split(";") if p.strip()]
        kv = {}
        for p in parts:
            if "=" not in p:
                continue
            k, v = p.split("=", 1)
            kv[k.strip().lower()] = v.strip()
        mapping = {
            "host": "host",
            "database": "dbname",
            "username": "user",
            "user id": "user",
            "password": "password",
            "ssl mode": "sslmode",
        }
        out = {}
        for k, v in kv.items():
            mk = mapping.get(k)
            if not mk:
                continue
            if mk == "sslmode":
                v = {"require": "require", "required": "require", "disable": "disable", "prefer": "prefer"}.get(v.lower(), v)
            out[mk] = v
        return " ".join(f"{k}={v}" for k, v in out.items())
    return None

def connect(conn_str):
    if not psycopg2:
        raise RuntimeError("psycopg2 not installed. Run: python -m pip install --user psycopg2-binary")
    try:
        return psycopg2.connect(conn_str, cursor_factory=psycopg2.extras.DictCursor)
    except Exception:
        dsn = _parse_conn_str(conn_str)
        if not dsn:
            raise
        return psycopg2.connect(dsn, cursor_factory=psycopg2.extras.DictCursor)


def fetch_one(conn, sql, params=None):
    with conn.cursor() as cur:
        cur.execute(sql, params or [])
        row = cur.fetchone()
        return row[0] if row else None

def table_has_columns(conn, schema: str, table: str, columns: list[str]) -> bool:
    q = (
        'SELECT COUNT(1) FROM information_schema.columns '
        'WHERE table_schema=%s AND table_name=%s AND column_name = ANY(%s)'
    )
    with conn.cursor() as cur:
        cur.execute(q, [schema, table, columns])
        cnt = cur.fetchone()[0]
        return cnt == len(columns)


def seed_business_info(conn, tenant_id, dry_run=False):
    count = fetch_one(conn, 'SELECT COUNT(1) FROM public."BusinessInfo" WHERE "TenantId"=%s', [tenant_id]) or 0
    if count > 0:
        return 0
    rows = [
        (tenant_id, 'Check-in', 'Check-in Policy', 'Standard check-in is from 14:00. Early check-in subject to availability.', ['checkin','policy'], True, 1),
        (tenant_id, 'Wi-Fi', 'Wi‑Fi Details', 'Network: StayBotGuest, Password: stay2025!', ['wifi','internet'], True, 2),
        (tenant_id, 'Parking', 'Parking Information', 'Secure parking available on-site. Please register your vehicle at reception.', ['parking'], True, 3),
        (tenant_id, 'Dining', 'Breakfast Hours', 'Breakfast is served daily 06:30–10:00 at the restaurant.', ['breakfast','dining'], True, 4),
    ]
    base_cols = ['TenantId','Category','Title','Content','Tags','IsActive','DisplayOrder']
    has_ts = table_has_columns(conn, 'public', 'BusinessInfo', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."BusinessInfo" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."BusinessInfo" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def seed_information_items(conn, tenant_id, dry_run=False):
    count = fetch_one(conn, 'SELECT COUNT(1) FROM public."InformationItems" WHERE "TenantId"=%s', [tenant_id]) or 0
    if count > 0:
        return 0
    rows = [
        (tenant_id, 'What time is check-in?', 'Check-in is from 14:00. Early check-in may be arranged subject to availability.', 'Check-in', ['checkin','time'], False, None, None, True, 10),
        (tenant_id, 'Do you offer airport transfers?', 'Yes, airport transfers can be arranged with the concierge at an additional cost.', 'Transport', ['airport','transfer'], False, None, None, True, 8),
        (tenant_id, 'Is breakfast included?', 'Breakfast may be included depending on your rate plan. Please check your booking confirmation.', 'Dining', ['breakfast','dining'], False, None, None, True, 6),
    ]
    base_cols = ['TenantId','Question','Answer','Category','Keywords','IsTimeRelevant','RelevantHourStart','RelevantHourEnd','IsActive','Priority']
    has_ts = table_has_columns(conn, 'public', 'InformationItems', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."InformationItems" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."InformationItems" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def seed_intent_settings(conn, tenant_id, dry_run=False):
    count = fetch_one(conn, 'SELECT COUNT(1) FROM public."IntentSettings" WHERE "TenantId"=%s', [tenant_id]) or 0
    if count > 0:
        return 0
    rows = [
        (tenant_id, 'request_towels', True, True, None, 10, False, True, 'Housekeeping', 'Normal', True, 30, None),
        (tenant_id, 'request_water', True, True, None, 9, False, True, 'Room Service', 'Normal', True, 20, None),
        (tenant_id, 'late_checkout', True, False, 'We will check availability and confirm shortly.', 8, True, True, 'Front Desk', 'High', False, None, None),
    ]
    base_cols = ['TenantId','IntentName','IsEnabled','EnableUpselling','CustomResponse','Priority','RequiresStaffApproval','NotifyStaff','AssignedDepartment','TaskPriority','AutoResolve','AutoResolveDelayMinutes','AdditionalConfig']
    has_ts = table_has_columns(conn, 'public', 'IntentSettings', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."IntentSettings" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."IntentSettings" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def seed_property_directions(conn, tenant_id, dry_run=False):
    count = fetch_one(conn, 'SELECT COUNT(1) FROM public."PropertyDirections" WHERE "TenantId"=%s', [tenant_id]) or 0
    if count > 0:
        return 0
    rows = [
        (tenant_id, 'Gym', 'Facility', 'From lobby, take the elevator to Level 2 and follow signs to Gym.', 'Level 2', 'East Wing', 'Near pool deck', '06:00–22:00', True, 1, None, None),
        (tenant_id, 'Pool', 'Facility', 'Exit to courtyard; pool is on the right-hand side.', 'Ground', 'Courtyard', 'Opposite restaurant', '08:00–20:00', True, 2, None, None),
        (tenant_id, 'Conference Room', 'Facility', 'Level 1, last door on the left after Business Center.', 'Level 1', 'North Wing', 'Next to Business Center', 'On request', True, 3, None, None),
    ]
    base_cols = ['TenantId','FacilityName','Category','Directions','Floor','Wing','Landmarks','Hours','IsActive','Priority','ImageUrl','AdditionalInfo']
    has_ts = table_has_columns(conn, 'public', 'PropertyDirections', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."PropertyDirections" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."PropertyDirections" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def seed_welcome_messages(conn, tenant_id, dry_run=False):
    count = fetch_one(conn, 'SELECT COUNT(1) FROM public."WelcomeMessages" WHERE "TenantId"=%s', [tenant_id]) or 0
    if count > 0:
        return 0
    rows = [
        (tenant_id, 'greeting', 'Hi {{guest_name}}, welcome to {{property_name}}! How can we assist you today?', True, 1),
        (tenant_id, 'assistance', 'We are here to help with any requests — towels, water, directions, and more.', True, 2),
        (tenant_id, 'checkout', 'Hope you enjoyed your stay. Checkout is at 10:00. Need luggage storage?', True, 3),
    ]
    base_cols = ['TenantId','MessageType','Template','IsActive','DisplayOrder']
    has_ts = table_has_columns(conn, 'public', 'WelcomeMessages', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."WelcomeMessages" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."WelcomeMessages" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def seed_bookings(conn, tenant_id, dry_run=False):
    # Optional demo bookings to light up dashboard/analytics; insert only if none exist
    exists = fetch_one(conn, 'SELECT COUNT(1) FROM public."Bookings" WHERE "TenantId"=%s', [tenant_id]) or 0
    if exists > 0:
        return 0
    today = datetime.utcnow().date()
    rows = []
    # 7 departures today
    for i in range(7):
        rows.append((tenant_id, f'GB{i+1001}', 'John Doe', 'john.doe@example.com', today - timedelta(days=2), today, 'CheckedOut', 2, 15000, 'WEB'))
    # 3 active stays
    for i in range(3):
        rows.append((tenant_id, f'GB{i+2001}', 'Jane Guest', 'jane.guest@example.com', today - timedelta(days=1), today + timedelta(days=1), 'CheckedIn', 2, 18000, 'OTA'))
    # upcoming arrivals
    for i in range(5):
        rows.append((tenant_id, f'GB{i+3001}', 'Alex Traveler', 'alex@example.com', today + timedelta(days=1), today + timedelta(days=3), 'Confirmed', 2, 20000, 'DIRECT'))
    base_cols = ['TenantId','ConfirmationNumber','GuestName','Email','CheckinDate','CheckoutDate','Status','TotalNights','RoomRate','Source']
    has_ts = table_has_columns(conn, 'public', 'Bookings', ['CreatedAt','UpdatedAt'])
    if has_ts:
        sql = 'INSERT INTO public."Bookings" ("'+'","'.join(base_cols+['CreatedAt','UpdatedAt'])+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s, NOW(), NOW())'
    else:
        sql = 'INSERT INTO public."Bookings" ("'+'","'.join(base_cols)+'") VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)'
    with conn.cursor() as cur:
        for r in rows:
            if not dry_run:
                cur.execute(sql, r)
    return len(rows)


def main():
    parser = argparse.ArgumentParser(description='Seed demo data for StayBot UI')
    parser.add_argument('--conn', default=os.environ.get('STAYBOT_CONN'), help='PostgreSQL connection string (or set STAYBOT_CONN)')
    parser.add_argument('--tenant-id', type=int, default=1)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()

    if not args.conn:
        raise SystemExit('Missing --conn or STAYBOT_CONN')

    conn = connect(args.conn)
    total = 0
    try:
        with conn:
            total += seed_business_info(conn, args.tenant_id, dry_run=args.dry_run)
            total += seed_information_items(conn, args.tenant_id, dry_run=args.dry_run)
            total += seed_intent_settings(conn, args.tenant_id, dry_run=args.dry_run)
            total += seed_property_directions(conn, args.tenant_id, dry_run=args.dry_run)
            total += seed_welcome_messages(conn, args.tenant_id, dry_run=args.dry_run)
            total += seed_bookings(conn, args.tenant_id, dry_run=args.dry_run)
    finally:
        conn.close()

    print(f"Seed completed. Inserted rows: {total} (dry_run={args.dry_run})")


if __name__ == '__main__':
    main()
