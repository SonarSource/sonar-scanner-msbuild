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

USER sonar

WORKDIR /app

ENTRYPOINT ["pwsh"]
