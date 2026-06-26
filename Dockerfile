# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ResumeAnalyzer.Api.csproj .
RUN dotnet restore -r linux-musl-x64

COPY . .
RUN dotnet publish -c Release -o /app/publish -r linux-musl-x64 --self-contained false --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Reduce GC memory pressure — critical on Render free tier (512 MB RAM)
ENV DOTNET_GCHeapHardLimit=402653184
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_TieredCompilation=0

RUN mkdir -p wwwroot/uploads

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ResumeAnalyzer.Api.dll"]
