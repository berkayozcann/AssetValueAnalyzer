FROM node:24-bookworm-slim AS web-assets

WORKDIR /workspace/src/AssetValueAnalyzer.Web

RUN corepack enable

COPY src/AssetValueAnalyzer.Web/package.json \
    src/AssetValueAnalyzer.Web/pnpm-lock.yaml \
    src/AssetValueAnalyzer.Web/pnpm-workspace.yaml \
    ./

RUN pnpm install --frozen-lockfile

COPY src/AssetValueAnalyzer.Web/ ./

RUN pnpm run assets:build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /workspace

COPY src/AssetValueAnalyzer.Domain/AssetValueAnalyzer.Domain.csproj \
    src/AssetValueAnalyzer.Domain/
COPY src/AssetValueAnalyzer.Application/AssetValueAnalyzer.Application.csproj \
    src/AssetValueAnalyzer.Application/
COPY src/AssetValueAnalyzer.Infrastructure/AssetValueAnalyzer.Infrastructure.csproj \
    src/AssetValueAnalyzer.Infrastructure/
COPY src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj \
    src/AssetValueAnalyzer.Web/
COPY src/AssetValueAnalyzer.Api/AssetValueAnalyzer.Api.csproj \
    src/AssetValueAnalyzer.Api/

RUN dotnet restore src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj \
    && dotnet restore src/AssetValueAnalyzer.Api/AssetValueAnalyzer.Api.csproj

COPY src/ ./src/

FROM dotnet-build AS web-build

COPY --from=web-assets \
    /workspace/src/AssetValueAnalyzer.Web/wwwroot/ \
    ./src/AssetValueAnalyzer.Web/wwwroot/

FROM web-build AS web-publish

RUN dotnet publish \
    src/AssetValueAnalyzer.Web/AssetValueAnalyzer.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

FROM dotnet-build AS api-publish

RUN dotnet publish \
    src/AssetValueAnalyzer.Api/AssetValueAnalyzer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

USER root

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID:$APP_UID" /home/app/.aspnet \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER $APP_UID

FROM runtime AS web

COPY --from=web-publish /app/publish/ ./

ENTRYPOINT ["dotnet", "AssetValueAnalyzer.Web.dll"]

FROM runtime AS api

COPY --from=api-publish /app/publish/ ./

ENTRYPOINT ["dotnet", "AssetValueAnalyzer.Api.dll"]
