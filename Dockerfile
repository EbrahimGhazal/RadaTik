# --- Stage 1: React SPA (Vite → RadTik/wwwroot/app) ---
FROM node:20-bookworm-slim AS webbuild
WORKDIR /src/radtik-web
COPY radtik-web/package.json radtik-web/package-lock.json ./
RUN npm ci
COPY radtik-web/ ./
# Static wwwroot assets (css, js, lib, …) required beside /app; Vite writes build to ../RadTik/wwwroot/app
COPY RadTik/wwwroot /src/RadTik/wwwroot
RUN npm run build

# --- Stage 2: Publish ASP.NET Core ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY RadTik/RadTik.csproj RadTik/
RUN dotnet restore RadTik/RadTik.csproj
COPY RadTik/ RadTik/
COPY --from=webbuild /src/RadTik/wwwroot RadTik/wwwroot
WORKDIR /src/RadTik
RUN dotnet publish RadTik.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 3: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
COPY docker/wait-for-sql.sh /app/wait-for-sql.sh
RUN chmod +x /app/wait-for-sql.sh

ENTRYPOINT ["/app/wait-for-sql.sh"]
