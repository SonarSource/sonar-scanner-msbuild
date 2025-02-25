FROM alpine:latest

ENV TIMEOUT=60

# Install dependencies
RUN apk update && apk upgrade && \
    apk add --no-cache \
    bash \
    curl \
    icu-libs \
    libintl \
    libssl3 \
    libstdc++ \
    nginx \
    openjdk17-jdk \
    openjdk17-jre \
    openssl \
    unzip \
    zlib

ADD --chmod=755 "https://dot.net/v1/dotnet-install.sh" /tmp/dotnet-install.sh

RUN /tmp/dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && mkdir -p /manual-tests/project

COPY nginx.conf /etc/nginx/nginx.conf

COPY tests.sh /manual-tests/tests.sh

COPY run.sh /manual-tests/run.sh

WORKDIR /manual-tests/project

RUN dotnet new sln -n ManualTests \
    && dotnet new classlib -n ManualTests \
    && dotnet sln add ManualTests/ManualTests.csproj

ADD https://github.com/bats-core/bats-core/archive/refs/tags/v1.11.1.tar.gz /tmp/bats.tar.gz
RUN mkdir -p /bats \
    && tar -xzf /tmp/bats.tar.gz -C /bats --strip-components=1 \
    && ln -s /bats/bin/bats /usr/local/bin/bats \
    && rm /tmp/bats.tar.gz

# Set the entrypoint
ENTRYPOINT ["bash"]
CMD ["/manual-tests/run.sh"]
