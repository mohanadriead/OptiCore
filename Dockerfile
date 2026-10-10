# syntax=docker/dockerfile:1
FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/OptiCore.Api/OptiCore.Api.csproj backend/src/OptiCore.Api/
COPY backend/src/OptiCore.Application/OptiCore.Application.csproj backend/src/OptiCore.Application/
COPY backend/src/OptiCore.Domain/OptiCore.Domain.csproj backend/src/OptiCore.Domain/
COPY backend/src/OptiCore.Infrastructure/OptiCore.Infrastructure.csproj backend/src/OptiCore.Infrastructure/
RUN dotnet restore backend/src/OptiCore.Api/OptiCore.Api.csproj
COPY backend/src/ backend/src/
COPY --from=frontend /src/frontend/dist/ frontend/dist/
RUN dotnet publish backend/src/OptiCore.Api/OptiCore.Api.csproj -c Release --no-restore \
    -p:BuildFrontend=false -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=backend /out/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "OptiCore.Api.dll"]
