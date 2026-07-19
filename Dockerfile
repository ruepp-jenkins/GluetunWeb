# syntax=docker/dockerfile:1

# ---------- Stage 1: build the React SPA ----------
FROM node:24-slim AS web
WORKDIR /src/web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci
COPY src/web/ ./
# vite.config.ts emits the build to ../GluetunWeb.Api/wwwroot (i.e. /src/GluetunWeb.Api/wwwroot)
RUN npm run build

# ---------- Stage 2: publish the ASP.NET Core API ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/GluetunWeb.Api/GluetunWeb.Api.csproj ./GluetunWeb.Api/
RUN dotnet restore ./GluetunWeb.Api/GluetunWeb.Api.csproj
COPY src/GluetunWeb.Api/ ./GluetunWeb.Api/
# Bring in the built SPA so it is published as static content under wwwroot.
COPY --from=web /src/GluetunWeb.Api/wwwroot ./GluetunWeb.Api/wwwroot
RUN dotnet publish ./GluetunWeb.Api/GluetunWeb.Api.csproj -c Release -o /app --no-restore

# ---------- Stage 3: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# git is used to clone/update the provider catalog (qdm12/gluetun-servers).
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./

# SQLite database lives on a mounted volume; secrets are AES-encrypted at rest.
RUN mkdir -p /data
VOLUME /data

ENV ASPNETCORE_URLS=http://+:8080 \
    GLUETUNWEB_DB_PATH=/data/gluetunweb.db \
    DOTNET_gcServer=0

EXPOSE 8080

# NOTE: this tool manages the Docker daemon, so it needs access to the Docker socket
# (mounted at /var/run/docker.sock). It runs as root by default for that reason — see the
# Security section of README.md for hardening options (socket proxy, rootless Docker/Podman).
ENTRYPOINT ["dotnet", "GluetunWeb.Api.dll"]
