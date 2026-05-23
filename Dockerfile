# syntax=docker/dockerfile:1.7

FROM node:22-bookworm-slim AS client-build
WORKDIR /src

COPY client-app/package*.json ./client-app/
WORKDIR /src/client-app
RUN npm ci

COPY client-app/ ./
RUN mkdir -p ../LittlePublisher.Web/wwwroot \
    && rm -rf ../LittlePublisher.Web/wwwroot/assets ../LittlePublisher.Web/wwwroot/index.html \
    && npm run type-check \
    && npm run build-only

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src

COPY global.json LittlePublisher.sln ./
COPY LittlePublisher.Web/LittlePublisher.Web.csproj LittlePublisher.Web/
COPY LittlePublisher.Web.Tests/LittlePublisher.Web.Tests.csproj LittlePublisher.Web.Tests/
COPY lib/IndieAuth/AspNet.Security.IndieAuth/AspNet.Security.IndieAuth.csproj lib/IndieAuth/AspNet.Security.IndieAuth/

RUN dotnet restore LittlePublisher.Web/LittlePublisher.Web.csproj

COPY LittlePublisher.Web/ LittlePublisher.Web/
COPY lib/IndieAuth/ lib/IndieAuth/
COPY --from=client-build /src/LittlePublisher.Web/wwwroot/ LittlePublisher.Web/wwwroot/

RUN dotnet publish LittlePublisher.Web/LittlePublisher.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.source="https://github.com/myquay/LittlePublisher" \
      org.opencontainers.image.description="LittlePublisher web application" \
      org.opencontainers.image.licenses="MIT"

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "LittlePublisher.Web.dll"]
