# syntax=docker/dockerfile:1
# Storefront.Web - multi-stage build. Build context: repository root (see docker-compose.yml).

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/Shared/Store.Observability/Store.Observability.csproj src/Shared/Store.Observability/
COPY src/Storefront/Storefront.Web/Storefront.Web.csproj src/Storefront/Storefront.Web/
COPY contracts/ contracts/
RUN dotnet restore src/Storefront/Storefront.Web/Storefront.Web.csproj

COPY src/Shared/Store.Observability/ src/Shared/Store.Observability/
COPY src/Storefront/Storefront.Web/ src/Storefront/Storefront.Web/
RUN dotnet publish src/Storefront/Storefront.Web/Storefront.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Data-protection keys (auth and session cookies) live in a volume so sign-ins survive restarts.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID:$APP_UID /home/app/.aspnet

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Storefront.Web.dll"]
