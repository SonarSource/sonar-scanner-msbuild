function gen-cert {
    openssl req -newkey rsa:2048 -x509 -sha256 -addext "subjectAltName = DNS:localhost" -nodes -out $1.crt -subj "/C=SM/ST=U/L=U/O=U/OU=U" -keyout $1.key 2> /dev/null
}

echo -n "Generating certificates for manual tests..."
gen-cert /manual-tests/system-trusted
cp /manual-tests/system-trusted.crt /usr/local/share/ca-certificates/system-trusted.crt
update-ca-certificates
keytool -import -storepass "changeit" -noprompt -cacerts -alias "system-trusted" -file /usr/local/share/ca-certificates/system-trusted.crt

gen-cert /manual-tests/untrusted

gen-cert /manual-tests/truststore
openssl pkcs12 -export -out /manual-tests/truststore.p12 -inkey "/manual-tests/truststore.key" -in "/manual-tests/truststore.crt" -passout pass:itchange

gen-cert /manual-tests/defaultpass
openssl pkcs12 -export -out /manual-tests/defaultpass.p12 -inkey "/manual-tests/defaultpass.key" -in "/manual-tests/defaultpass.crt" -passout pass:changeit

echo " Done"

echo "Starting nginx..."

nginx

echo "Running manual tests..."
timeout $TIMEOUT bats --print-output-on-failure /manual-tests/tests.sh
