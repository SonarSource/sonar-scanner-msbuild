FROM mcr.microsoft.com/dotnet/sdk:9.0:latest AS build
WORKDIR /App

# Copy everything
COPY src/ ./
# Restore as distinct layers
run dotnet restore
# Build and publish a release
RUN dotnet publish -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0:latest
WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "MultiLanguageSupport.dll"]
