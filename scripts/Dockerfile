FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get upgrade -y \
    && apt install openjdk-17-jdk openjdk-17-jre maven powershell unzip -y \
    && apt-get clean

# ElasticSearch cannot be run as root, so we have to create a user to run everything
RUN groupadd -r sonar \
    && useradd -ms /bin/bash -r -g sonar sonar \
    && mkdir -p /home/sonar/.sonar/cache \
    && mkdir -p /home/sonar/.sonar/_tmp \
    && chown -R sonar:sonar /home/sonar/.sonar \
    && mkdir -p /app \
    && chown -R sonar:sonar /app

RUN mkdir -p /app/its/target \
    && chown -R sonar:sonar /app/its/target

ENV SSL_KEYSTORE_PASSWORD=changeit
ENV SSL_KEYSTORE_PATH=/certs/system-trusted.p12
    
# Create and trust a self-signed certificate in the system
RUN mkdir /certs \
    && openssl req \
     -newkey rsa:2048 \
     -x509 \
     -sha256 \
     -addext "subjectAltName = DNS:localhost" \
     -nodes \
     -out /certs/system-trusted.crt \
     -subj "/C=SM/ST=U/L=U/O=U/OU=U" \
     -keyout /certs/system-trusted.key \
    && openssl pkcs12 \
     -export \
     -out "$SSL_KEYSTORE_PATH" \
     -inkey "/certs/system-trusted.key" \
     -in "/certs/system-trusted.crt" \
     -passout pass:"$SSL_KEYSTORE_PASSWORD" \
    && cp /certs/system-trusted.crt /usr/local/share/ca-certificates/system-trusted.crt \
    && update-ca-certificates \
    && keytool \
     -import \
     -storepass "changeit" \
     -noprompt \
     -cacerts \
     -alias "system-trusted" \
     -file /usr/local/share/ca-certificates/system-trusted.crt \
    && chown -R sonar:sonar /certs \
    && mkdir -p /home/sonar/.aspnet/https-

USER sonar

WORKDIR /app

ENTRYPOINT ["pwsh"]
