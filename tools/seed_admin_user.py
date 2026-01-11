import os
import argparse
import uuid

try:
    import psycopg2
    import psycopg2.extras
except Exception:
    psycopg2 = None


def connect(conn_str: str):
    if not psycopg2:
        raise RuntimeError("psycopg2 not installed. Run: python -m pip install --user psycopg2-binary")
    # Allow Npgsql-style strings
    def to_dsn(s: str):
        if ";" in s and "Host=" in s:
            parts = [p for p in s.split(";") if p.strip()]
            mapping = {
                "host": "host",
                "database": "dbname",
                "username": "user",
                "user id": "user",
                "password": "password",
                "ssl mode": "sslmode",
            }
            kv = {}
            for p in parts:
                if "=" not in p:
                    continue
                k, v = p.split("=", 1)
                k = k.strip().lower()
                v = v.strip()
                if k in mapping:
                    if mapping[k] == "sslmode":
                        v = {"require": "require", "required": "require", "disable": "disable", "prefer": "prefer"}.get(v.lower(), v)
                    kv[mapping[k]] = v
            return " ".join(f"{k}={v}" for k, v in kv.items())
        return s
    try:
        return psycopg2.connect(conn_str, cursor_factory=psycopg2.extras.DictCursor)
    except Exception:
        return psycopg2.connect(to_dsn(conn_str), cursor_factory=psycopg2.extras.DictCursor)


def table_exists(conn, schema: str, table: str) -> bool:
    with conn.cursor() as cur:
        cur.execute(
            "SELECT 1 FROM information_schema.tables WHERE table_schema=%s AND table_name=%s",
            [schema, table],
        )
        return cur.fetchone() is not None


def cols_exist(conn, schema: str, table: str, cols: list[str]) -> bool:
    with conn.cursor() as cur:
        cur.execute(
            "SELECT column_name FROM information_schema.columns WHERE table_schema=%s AND table_name=%s",
            [schema, table],
        )
        have = {r[0] for r in cur.fetchall()}
        return all(c in have for c in cols)


def get_role_id(conn, role_name: str):
    with conn.cursor() as cur:
        cur.execute('SELECT "Id" FROM "AspNetRoles" WHERE "Name"=%s', [role_name])
        row = cur.fetchone()
        return row[0] if row else None


def get_user_id(conn, normalized_email: str):
    with conn.cursor() as cur:
        cur.execute('SELECT "Id" FROM "AspNetUsers" WHERE "NormalizedEmail"=%s', [normalized_email])
        row = cur.fetchone()
        return row[0] if row else None


def seed_user(conn, email: str, tenant_id: int, link_role: bool = True, link_tenant: bool = True):
    normalized = email.upper()
    user_id = get_user_id(conn, normalized)
    if user_id:
        return user_id, False

    user_guid = str(uuid.uuid4())
    # Required columns baseline per ASP.NET Identity
    base_cols = [
        'Id','UserName','NormalizedUserName','Email','NormalizedEmail','EmailConfirmed',
        'PasswordHash','SecurityStamp','ConcurrencyStamp','PhoneNumberConfirmed','TwoFactorEnabled','LockoutEnabled','AccessFailedCount'
    ]
    timestamps = cols_exist(conn, 'public', 'AspNetUsers', ['CreatedAt','UpdatedAt'])

    cols = base_cols + (['CreatedAt','UpdatedAt'] if timestamps else [])
    placeholders = ",".join(["%s"] * len(base_cols)) + (", NOW(), NOW()" if timestamps else "")

    with conn.cursor() as cur:
        cur.execute(
            f'INSERT INTO "AspNetUsers" ("' + '","'.join(cols) + f'") VALUES ({placeholders})',
            [
                user_guid,
                email,
                normalized,
                email,
                normalized,
                True,            # EmailConfirmed
                None,            # PasswordHash (Option 1: left NULL)
                str(uuid.uuid4()),   # SecurityStamp
                str(uuid.uuid4()),   # ConcurrencyStamp
                False,
                False,
                False,
                0,
            ],
        )

    # Optional links
    if link_tenant and table_exists(conn, 'public', 'TenantUsers'):
        with conn.cursor() as cur:
            cur.execute('SELECT 1 FROM "TenantUsers" WHERE "TenantId"=%s AND "UserId"=%s', [tenant_id, user_guid])
            if not cur.fetchone():
                cur.execute('INSERT INTO "TenantUsers" ("TenantId","UserId") VALUES (%s,%s)', [tenant_id, user_guid])

    if link_role and table_exists(conn, 'public', 'AspNetUserRoles') and table_exists(conn, 'public', 'AspNetRoles'):
        admin_role_id = get_role_id(conn, 'Admin') or get_role_id(conn, 'Administrator')
        if admin_role_id:
            with conn.cursor() as cur:
                cur.execute('SELECT 1 FROM "AspNetUserRoles" WHERE "UserId"=%s AND "RoleId"=%s', [user_guid, admin_role_id])
                if not cur.fetchone():
                    cur.execute('INSERT INTO "AspNetUserRoles" ("UserId","RoleId") VALUES (%s,%s)', [user_guid, admin_role_id])

    return user_guid, True


def main():
    parser = argparse.ArgumentParser(description='Insert test admin user into AspNetUsers (Option 1: no password hash)')
    parser.add_argument('--conn', default=os.environ.get('STAYBOT_CONN'), help='PostgreSQL connection string (or set STAYBOT_CONN)')
    parser.add_argument('--tenant-id', type=int, default=1)
    parser.add_argument('--email', default='test@admin.com')
    parser.add_argument('--no-role', action='store_true', help='Do not link to Admin role')
    parser.add_argument('--no-tenant-link', action='store_true', help='Do not link to TenantUsers')
    parser.add_argument('--password-hash', help='If provided, update AspNetUsers.PasswordHash for the user (use Identity hasher output)')
    args = parser.parse_args()

    if not args.conn:
        raise SystemExit('Missing --conn or STAYBOT_CONN')

    conn = connect(args.conn)
    try:
        with conn:
            user_id, created = seed_user(conn, args.email, args.tenant_id, link_role=not args.no_role, link_tenant=not args.no_tenant_link)
            if args.password_hash:
                with conn.cursor() as cur:
                    cur.execute('UPDATE "AspNetUsers" SET "PasswordHash"=%s WHERE "NormalizedEmail"=%s', [args.password_hash, args.email.upper()])
    finally:
        conn.close()

    print(f"UserId: {user_id} | created={created} | email={args.email} | tenant_id={args.tenant_id}")
    if args.password_hash:
        print("PasswordHash updated.")
    else:
        print("NOTE: PasswordHash is NULL. The user cannot log in until we set a valid ASP.NET Identity hash (Option 2).")


if __name__ == '__main__':
    main()
