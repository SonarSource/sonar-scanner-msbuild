#!/usr/bin/env bash
# Generates a self-signed certificate for the SslTest integration test, and trusts it in the system store.
set -euo pipefail

CERTS_DIR="$GITHUB_WORKSPACE/certs"
mkdir -p "$CERTS_DIR"
openssl req -newkey rsa:2048 -x509 -sha256 -addext "subjectAltName = DNS:localhost" -nodes \
  -out "$CERTS_DIR/system-trusted.crt" -subj "/C=CS/ST=U/L=U/O=U/OU=U" -keyout "$CERTS_DIR/system-trusted.key"
openssl pkcs12 -export -out "$CERTS_DIR/system-trusted.p12" -inkey "$CERTS_DIR/system-trusted.key" \
  -in "$CERTS_DIR/system-trusted.crt" -passout pass:changeit
keytool -import -storepass changeit -noprompt -cacerts -alias system-trusted -file "$CERTS_DIR/system-trusted.crt"

case "$(uname -s)" in
  Linux)
    sudo cp "$CERTS_DIR/system-trusted.crt" /usr/local/share/ca-certificates/system-trusted.crt
    sudo update-ca-certificates
    ;;
  Darwin)
    sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain "$CERTS_DIR/system-trusted.crt"
    ;;
  *)
    echo "Running on an unsupported or unknown OS: $(uname -s)" >&2
    exit 1
    ;;
esac

echo "SSL_KEYSTORE_PATH=$CERTS_DIR/system-trusted.p12" >> "$GITHUB_ENV"
echo "SSL_KEYSTORE_PASSWORD=changeit" >> "$GITHUB_ENV"
