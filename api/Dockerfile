# syntax=docker/dockerfile:1.7
#
# Build context is the REPO ROOT (set in docker-compose.yml). Paths below are
# relative to the repo root so the migrations under database/ can be copied in.

# ───── Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore (cached on csproj-only changes)
COPY api/src/ToolboxManager.Api/ToolboxManager.Api.csproj api/src/ToolboxManager.Api/
RUN dotnet restore api/src/ToolboxManager.Api/ToolboxManager.Api.csproj

# Copy the rest. The csproj uses <Content Include="..\..\..\database\migrations\*.sql">
# to pull the SQL files into the build output, so we need the database dir at
# the matching relative path.
COPY api/src/ api/src/
COPY database/ database/

RUN dotnet publish api/src/ToolboxManager.Api/ToolboxManager.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ───── Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN useradd --uid 10001 --no-create-home --shell /sbin/nologin app \
 && mkdir -p /app/logs \
 && chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .
USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ToolboxManager.Api.dll"]
