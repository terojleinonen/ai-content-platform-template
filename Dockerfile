# syntax=docker/dockerfile:1

# 1. Frontend: Vite builds straight into the API's wwwroot.
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2. API: restore (cached unless the project file changes), then publish with the built frontend.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src/AiContentPlatform.Api/AiContentPlatform.Api.csproj backend/src/AiContentPlatform.Api/
RUN dotnet restore backend/src/AiContentPlatform.Api/AiContentPlatform.Api.csproj
COPY backend/src/ backend/src/
COPY --from=frontend /src/backend/src/AiContentPlatform.Api/wwwroot backend/src/AiContentPlatform.Api/wwwroot
RUN dotnet publish backend/src/AiContentPlatform.Api/AiContentPlatform.Api.csproj -c Release -o /app --no-restore

# 3. Runtime: ASP.NET Core only, running as the image's non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p /data/keys && chown "$APP_UID" /data/keys
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeysPath=/data/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AiContentPlatform.Api.dll"]
