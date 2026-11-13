# syntax=docker/dockerfile:1
# Payment.Api - multi-stage build. Build context: repository root (see docker-compose.yml).

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json ./
COPY src/Shared/Store.Contracts.Events/Store.Contracts.Events.csproj src/Shared/Store.Contracts.Events/
COPY src/Shared/Store.Messaging/Store.Messaging.csproj src/Shared/Store.Messaging/
COPY src/Shared/Store.Observability/Store.Observability.csproj src/Shared/Store.Observability/
COPY src/Services/Payment/Payment.Api/Payment.Api.csproj src/Services/Payment/Payment.Api/
RUN dotnet restore src/Services/Payment/Payment.Api/Payment.Api.csproj

COPY src/Shared/Store.Contracts.Events/ src/Shared/Store.Contracts.Events/
COPY src/Shared/Store.Messaging/ src/Shared/Store.Messaging/
COPY src/Shared/Store.Observability/ src/Shared/Store.Observability/
COPY src/Services/Payment/Payment.Api/ src/Services/Payment/Payment.Api/
RUN dotnet publish src/Services/Payment/Payment.Api/Payment.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Run as the image's built-in non-root user (uid 1654).
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Payment.Api.dll"]
