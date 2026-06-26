# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cached unless .csproj changes)
COPY ResumeAnalyzer.Api.csproj .
RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create the uploads directory (mounted as a volume in production)
RUN mkdir -p wwwroot/uploads

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose HTTP port (HTTPS is terminated at the reverse-proxy / load-balancer)
EXPOSE 8080

# ASP.NET Core listens on 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ResumeAnalyzer.Api.dll"]
