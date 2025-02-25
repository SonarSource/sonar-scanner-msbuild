FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get upgrade -y \
    && apt install openjdk-17-jdk openjdk-17-jre maven powershell unzip -y \
    && apt-get clean \
    && useradd -ms /bin/bash sonar

COPY --chown=sonar:sonar . /app

USER sonar

ENTRYPOINT ["bash"]
