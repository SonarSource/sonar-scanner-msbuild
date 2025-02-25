FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN wget -q https://packages.microsoft.com/config/debian/$VERSION_ID/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb

RUN apt-get update \
    && apt-get upgrade -y \
    && apt install openjdk-17-jdk openjdk-17-jre maven powershell -y

