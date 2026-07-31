# syntax=docker/dockerfile:1

FROM node:22-alpine AS assets-build
WORKDIR /src
COPY src/GlimpsesOfGlory.Web/package.json src/GlimpsesOfGlory.Web/package-lock.json ./
RUN npm ci
COPY src/GlimpsesOfGlory.Web/scripts ./scripts
COPY src/GlimpsesOfGlory.Web/assets ./assets
COPY src/GlimpsesOfGlory.Web/Pages ./Pages
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY GlimpsesOfGloryEshop.slnx ./
COPY src/GlimpsesOfGlory.Domain/GlimpsesOfGlory.Domain.csproj src/GlimpsesOfGlory.Domain/
COPY src/GlimpsesOfGlory.Application/GlimpsesOfGlory.Application.csproj src/GlimpsesOfGlory.Application/
COPY src/GlimpsesOfGlory.Infrastructure/GlimpsesOfGlory.Infrastructure.csproj src/GlimpsesOfGlory.Infrastructure/
COPY src/GlimpsesOfGlory.Web/GlimpsesOfGlory.Web.csproj src/GlimpsesOfGlory.Web/
RUN dotnet restore

COPY src/ src/
COPY --from=assets-build /src/wwwroot/css/site.css src/GlimpsesOfGlory.Web/wwwroot/css/site.css
COPY --from=assets-build /src/wwwroot/js/alpine.min.js src/GlimpsesOfGlory.Web/wwwroot/js/alpine.min.js

# Pre-deploy gate: image build fails here if compilation fails.
RUN dotnet build -c Release --no-restore

RUN dotnet publish src/GlimpsesOfGlory.Web/GlimpsesOfGlory.Web.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GlimpsesOfGlory.Web.dll"]
