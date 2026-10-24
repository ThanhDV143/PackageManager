# syntax=docker/dockerfile:1
# Builds on the runner's own architecture and cross-publishes for TARGETARCH (amd64 / arm64),
# so no emulation is needed for the NAS image.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY src/PackageManager.Shared/PackageManager.Shared.csproj src/PackageManager.Shared/
COPY src/PackageManager.Client/PackageManager.Client.csproj src/PackageManager.Client/
COPY src/PackageManager.Server/PackageManager.Server.csproj src/PackageManager.Server/
RUN dotnet restore src/PackageManager.Server -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/PackageManager.Server -c Release -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__DatabasePath=/data/app.db \
    Storage__PackagesPath=/data/unitypkgs
VOLUME /data
EXPOSE 8080

# Non-root by default; compose can override with the UID that owns the NAS folders.
USER $APP_UID
ENTRYPOINT ["dotnet", "PackageManager.Server.dll"]
