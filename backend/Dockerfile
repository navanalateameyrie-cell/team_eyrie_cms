# syntax=docker/dockerfile:1
# -----------------------------------------------------------------------------
# Build stage — restore + publish the CMS.Api project with its project refs.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first so docker caches the restore layer across code-only edits.
COPY backend/src/CMS.Domain/CMS.Domain.csproj              backend/src/CMS.Domain/
COPY backend/src/CMS.Application/CMS.Application.csproj    backend/src/CMS.Application/
COPY backend/src/CMS.Infrastructure/CMS.Infrastructure.csproj backend/src/CMS.Infrastructure/
COPY backend/src/CMS.Api/CMS.Api.csproj                    backend/src/CMS.Api/
RUN dotnet restore backend/src/CMS.Api/CMS.Api.csproj

# Now the sources + publish.
COPY backend/src backend/src
RUN dotnet publish backend/src/CMS.Api/CMS.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Runtime stage — ASP.NET 8 only, non-root, listens on 8080.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# App_Data holds document uploads + the dev mail-drop; mount a volume here in prod.
RUN mkdir -p /app/App_Data/documents /app/App_Data/mail-drop && \
    chown -R app:app /app/App_Data

# Kestrel listens on 8080 (unprivileged) so we can run non-root.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "CMS.Api.dll"]
