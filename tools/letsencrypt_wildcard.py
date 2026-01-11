#!/usr/bin/env python3
"""
Let's Encrypt Wildcard Certificate Generator
Uses ACME protocol with DNS-01 challenge for wildcard domains
"""

import json
import os
import time
from datetime import datetime, timedelta
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.backends import default_backend
from cryptography.x509.oid import NameOID
import josepy as jose
from acme import client, messages, challenges
import requests

# Configuration
DOMAIN = "staybot.co.za"
WILDCARD_DOMAIN = f"*.{DOMAIN}"
EMAIL = "admin@staybot.co.za"
OUTPUT_DIR = "C:/certbot/certificates"

# Let's Encrypt ACME URLs
# ACME_DIRECTORY = "https://acme-staging-v02.api.letsencrypt.org/directory"  # Staging
ACME_DIRECTORY = "https://acme-v02.api.letsencrypt.org/directory"  # Production


def generate_private_key():
    """Generate a new RSA private key"""
    return rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048,
        backend=default_backend()
    )


def generate_csr(private_key, domains):
    """Generate a Certificate Signing Request"""
    subject = x509.Name([
        x509.NameAttribute(NameOID.COMMON_NAME, domains[0]),
    ])

    # Add SAN for all domains
    san = x509.SubjectAlternativeName([
        x509.DNSName(domain) for domain in domains
    ])

    csr = (
        x509.CertificateSigningRequestBuilder()
        .subject_name(subject)
        .add_extension(san, critical=False)
        .sign(private_key, hashes.SHA256(), default_backend())
    )

    return csr


def create_acme_client(account_key):
    """Create ACME client"""
    net = client.ClientNetwork(account_key)
    directory = client.ClientV2.get_directory(ACME_DIRECTORY, net)
    return client.ClientV2(directory, net)


def register_account(acme_client, account_key):
    """Register account with Let's Encrypt"""
    registration = messages.NewRegistration.from_data(
        email=EMAIL,
        terms_of_service_agreed=True
    )

    try:
        account = acme_client.new_account(registration)
        print(f"Account registered: {account.uri}")
        return account
    except Exception as e:
        if "already registered" in str(e).lower():
            # Account already exists, retrieve it
            account = acme_client.query_registration(
                messages.NewRegistration.from_data(email=EMAIL)
            )
            print(f"Using existing account: {account.uri}")
            return account
        raise


def get_dns_challenge(acme_client, order, domain):
    """Get DNS-01 challenge for a domain"""
    for authz in order.authorizations:
        authz_resource = acme_client.poll_authorizations(order)
        for auth in authz_resource:
            if auth.body.identifier.value == domain.lstrip("*."):
                for chall in auth.body.challenges:
                    if isinstance(chall.chall, challenges.DNS01):
                        return chall
    return None


def main():
    print("=" * 60)
    print("Let's Encrypt Wildcard Certificate Generator")
    print("=" * 60)
    print(f"\nDomains: {WILDCARD_DOMAIN}, {DOMAIN}")
    print(f"Email: {EMAIL}")
    print(f"ACME Directory: {ACME_DIRECTORY}")
    print()

    # Create output directory
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    # Generate or load account key
    account_key_path = os.path.join(OUTPUT_DIR, "account_key.pem")
    if os.path.exists(account_key_path):
        print("Loading existing account key...")
        with open(account_key_path, "rb") as f:
            account_key = jose.JWKRSA(
                key=serialization.load_pem_private_key(
                    f.read(), password=None, backend=default_backend()
                )
            )
    else:
        print("Generating new account key...")
        account_key_raw = generate_private_key()
        account_key = jose.JWKRSA(key=account_key_raw)
        with open(account_key_path, "wb") as f:
            f.write(account_key_raw.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.PKCS8,
                encryption_algorithm=serialization.NoEncryption()
            ))

    # Generate domain private key
    print("Generating domain private key...")
    domain_key = generate_private_key()

    # Generate CSR
    print("Generating CSR...")
    domains = [WILDCARD_DOMAIN, DOMAIN]
    csr = generate_csr(domain_key, domains)

    # Create ACME client
    print("Connecting to Let's Encrypt...")
    acme_client = create_acme_client(account_key)

    # Register account
    print("Registering account...")
    register_account(acme_client, account_key)

    # Create order
    print("Creating certificate order...")
    order = acme_client.new_order(
        jose.ComparableX509(csr).wrapped
    )

    # Process authorizations
    print("\n" + "=" * 60)
    print("DNS CHALLENGE REQUIRED")
    print("=" * 60)

    dns_challenges = []
    for authz in order.authorizations:
        domain = authz.body.identifier.value
        for chall in authz.body.challenges:
            if isinstance(chall.chall, challenges.DNS01):
                validation = chall.chall.validation(account_key)
                record_name = f"_acme-challenge.{domain}"

                dns_challenges.append({
                    "domain": domain,
                    "challenge": chall,
                    "record_name": record_name,
                    "record_value": validation
                })

                print(f"\nDomain: {domain}")
                print(f"  TXT Record Name:  {record_name}")
                print(f"  TXT Record Value: {validation}")

    print("\n" + "=" * 60)
    print("ACTION REQUIRED")
    print("=" * 60)
    print("""
Please add the above TXT record(s) to your DNS in Afrihost:

1. Log into Afrihost DNS management
2. Add a TXT record for each challenge shown above
3. Wait 2-5 minutes for DNS propagation
4. You can verify with: nslookup -type=TXT _acme-challenge.staybot.co.za

Press ENTER when you have added the DNS record(s)...
""")

    input()

    # Verify DNS propagation
    print("Checking DNS propagation...")
    for challenge_info in dns_challenges:
        record_name = challenge_info["record_name"]
        expected_value = challenge_info["record_value"]

        print(f"  Checking {record_name}...")
        # Simple check using requests to a DNS API
        try:
            response = requests.get(
                f"https://dns.google/resolve?name={record_name}&type=TXT",
                timeout=10
            )
            data = response.json()
            if "Answer" in data:
                found_values = [a.get("data", "").strip('"') for a in data["Answer"]]
                if expected_value in found_values:
                    print(f"    Found correct TXT record!")
                else:
                    print(f"    WARNING: Expected '{expected_value}' but found {found_values}")
            else:
                print(f"    WARNING: No TXT record found yet. This may still work if DNS is propagating.")
        except Exception as e:
            print(f"    Could not verify: {e}")

    print("\nSubmitting challenges to Let's Encrypt...")

    # Answer challenges
    for challenge_info in dns_challenges:
        chall = challenge_info["challenge"]
        response = chall.chall.response(account_key)
        acme_client.answer_challenge(chall, response)

    # Poll for order completion
    print("Waiting for validation...")
    max_attempts = 30
    for attempt in range(max_attempts):
        order = acme_client.poll_and_finalize(order)
        if order.body.status == messages.STATUS_VALID:
            print("Validation successful!")
            break
        elif order.body.status == messages.STATUS_INVALID:
            print("Validation failed!")
            for authz in order.authorizations:
                for chall in authz.body.challenges:
                    if chall.status == messages.STATUS_INVALID:
                        print(f"  Challenge failed: {chall.error}")
            return
        time.sleep(2)
    else:
        print("Timeout waiting for validation")
        return

    # Save certificate
    cert_path = os.path.join(OUTPUT_DIR, "certificate.pem")
    key_path = os.path.join(OUTPUT_DIR, "private_key.pem")
    fullchain_path = os.path.join(OUTPUT_DIR, "fullchain.pem")

    # Get the certificate
    cert = order.fullchain_pem

    with open(cert_path, "w") as f:
        # Write just the first certificate (the domain cert)
        certs = cert.split("-----END CERTIFICATE-----")
        f.write(certs[0] + "-----END CERTIFICATE-----\n")

    with open(fullchain_path, "w") as f:
        f.write(cert)

    with open(key_path, "wb") as f:
        f.write(domain_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()
        ))

    print("\n" + "=" * 60)
    print("CERTIFICATE GENERATED SUCCESSFULLY!")
    print("=" * 60)
    print(f"\nFiles saved to: {OUTPUT_DIR}")
    print(f"  - certificate.pem   (domain certificate)")
    print(f"  - fullchain.pem     (certificate + chain)")
    print(f"  - private_key.pem   (private key)")

    # Create PFX for Azure
    print("\nCreating PFX file for Azure...")
    pfx_path = os.path.join(OUTPUT_DIR, "certificate.pfx")
    pfx_password = "StayBot2025!"

    # Use openssl command to create PFX
    import subprocess
    subprocess.run([
        "openssl", "pkcs12", "-export",
        "-out", pfx_path,
        "-inkey", key_path,
        "-in", cert_path,
        "-certfile", fullchain_path,
        "-passout", f"pass:{pfx_password}"
    ], check=True)

    print(f"  - certificate.pfx   (for Azure, password: {pfx_password})")

    print(f"""
Next steps to upload to Azure:

az webapp config ssl upload \\
  --resource-group staybot-prod-rg \\
  --name staybot-guest \\
  --certificate-file "{pfx_path}" \\
  --certificate-password "{pfx_password}"
""")


if __name__ == "__main__":
    main()
