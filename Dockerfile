FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet restore backend/SmartGuard.API/SmartGuard.API.csproj
RUN dotnet publish backend/SmartGuard.API/SmartGuard.API.csproj --no-restore --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
COPY --from=api-build /app/publish/ ./
COPY --from=frontend-build /src/frontend/dist/ ./wwwroot/
ENTRYPOINT ["dotnet", "SmartGuard.API.dll"]
