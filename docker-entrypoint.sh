# --- Stage 1: React SPA (Vite → RadaTik/wwwroot/app) ---
FROM node:20-bookworm-slim AS webbuild
WORKDIR /src/radatik-web
COPY radatik-web/package.json radatik-web/package-lock.json ./
RUN npm ci
COPY radatik-web/ ./
# Static wwwroot assets (css, js, lib, …) required beside /app; Vite writes build to ../RadaTik/wwwroot/app
COPY RadaTik/wwwroot /src/RadaTik/wwwroot
RUN npm run build

# --- Stage 2: Publish ASP.NET Core ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY RadaTik/RadaTik.csproj RadaTik/
RUN dotnet restore RadaTik/RadaTik.csproj
COPY RadaTik/ RadaTik/
COPY --from=webbuild /src/RadaTik/wwwroot RadaTik/wwwroot
WORKDIR /src/RadaTik
RUN dotnet publish RadaTik.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 3: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
COPY docker/wait-for-sql.sh /app/wait-for-sql.sh
RUN chmod +x /app/wait-for-sql.sh

ENTRYPOINT ["/app/wait-for-sql.sh"]
