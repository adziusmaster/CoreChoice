# CoreChoice backend — multi-stage build for a Linux VPS (Hetzner).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/CoreChoice.Core/ ./CoreChoice.Core/
COPY src/CoreChoice.Server/ ./CoreChoice.Server/
# nuget.org explicitly: the default feed on the build machine is an unreachable mirror.
RUN dotnet restore CoreChoice.Server/CoreChoice.Server.csproj \
      --source https://api.nuget.org/v3/index.json \
 && dotnet publish CoreChoice.Server/CoreChoice.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Install operational tools the aspnet base image ships neither of: curl for the compose
# healthcheck, and sqlite3 for DEPLOY.md's prompt-change procedure (edits the live database
# in-container, without a redeploy). Neither is unused even though nothing in the app itself
# calls them.
RUN apt-get update && apt-get install -y --no-install-recommends curl sqlite3 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
# SQLite database persists on a mounted volume.
VOLUME /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Db="Data Source=/data/corechoice.server.db"
EXPOSE 8080
# Secrets (Gemini__ApiKey, Dev__Secret, Security__IpHashSalt) are supplied at runtime.
ENTRYPOINT ["dotnet", "CoreChoice.Server.dll"]
