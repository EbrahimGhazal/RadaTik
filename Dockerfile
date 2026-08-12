# =========================================================
# 1) Build React / Vite
# =========================================================
FROM node:22-bookworm-slim AS frontend

WORKDIR /src

COPY radatik-web/package.json radatik-web/package-lock.json ./radatik-web/

WORKDIR /src/radatik-web

RUN npm ci

COPY radatik-web/ ./

RUN npm run build


# =========================================================
# 2) Build ASP.NET Core
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY RadaTik/RadaTik.csproj RadaTik/

RUN dotnet restore RadaTik/RadaTik.csproj

COPY RadaTik/ RadaTik/

# Copy React production build into ASP.NET wwwroot/app
COPY --from=frontend /src/RadaTik/wwwroot/app/ RadaTik/wwwroot/app/

# Disable the Windows-specific frontend MSBuild target.
RUN dotnet publish RadaTik/RadaTik.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:RunFrontendBuild=false


# =========================================================
# 3) Production runtime
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

COPY docker-entrypoint.sh /docker-entrypoint.sh

RUN chmod +x /docker-entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/docker-entrypoint.sh"]
