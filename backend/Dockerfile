# syntax=docker/dockerfile:1
# Crm.Api - the modular-monolith host. Build context: backend/.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: shared build settings + every project file only, so a code change does not re-restore.
COPY global.json Crm.sln .editorconfig Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY build/ build/
COPY --parents src/**/*.csproj ./
RUN dotnet restore src/Crm.Api/Crm.Api.csproj

COPY src/ src/
RUN dotnet publish src/Crm.Api/Crm.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Crm.Api.dll"]
