# Midnight Panel: api server with the built web frontend in one image.
# Build: docker build -t midnight/panel .

# 1. Web frontend (Vite writes into src/Midnight.ApiServer/wwwroot)
FROM node:22-alpine AS web
WORKDIR /src/src/Midnight.Web
COPY src/Midnight.Web/package.json src/Midnight.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/Midnight.Web/ ./
RUN npm run build

# 2. Api server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Midnight.Shared/Midnight.Shared.csproj src/Midnight.Shared/
COPY src/Midnight.Plugins.Abstractions/Midnight.Plugins.Abstractions.csproj src/Midnight.Plugins.Abstractions/
COPY src/Midnight.ApiServer/Midnight.ApiServer.csproj src/Midnight.ApiServer/
RUN dotnet restore src/Midnight.ApiServer
COPY src/Midnight.Shared/ src/Midnight.Shared/
COPY src/Midnight.Plugins.Abstractions/ src/Midnight.Plugins.Abstractions/
COPY src/Midnight.ApiServer/ src/Midnight.ApiServer/
COPY --from=web /src/src/Midnight.ApiServer/wwwroot src/Midnight.ApiServer/wwwroot
RUN dotnet publish src/Midnight.ApiServer -c Release -o /app --no-restore -p:SkipWebBuild=true

# 3. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# Named volumes copy this ownership on first use, so the non root user can write
RUN mkdir -p /app/storage && chown app:app /app/storage

ENV ASPNETCORE_URLS=http://+:8080 \
    MIDNIGHT_STORAGE=/app/storage

USER app
EXPOSE 8080
VOLUME /app/storage

ENTRYPOINT ["dotnet", "Midnight.ApiServer.dll"]
