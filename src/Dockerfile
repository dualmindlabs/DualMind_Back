# ============================================
# DualMind API — Optimized for CF Containers
# ============================================

# --- Base: Alpine for smallest image size ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DualMind.API/DualMind.API.csproj", "DualMind.API/"]
RUN dotnet restore "DualMind.API/DualMind.API.csproj" --runtime linux-musl-x64
COPY . .
WORKDIR "/src/DualMind.API"
RUN dotnet build "DualMind.API.csproj" -c Release -o /app/build --runtime linux-musl-x64 --no-restore

# --- Publish stage: R2R for faster cold starts ---
FROM build AS publish
RUN dotnet publish "DualMind.API.csproj" -c Release -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained false \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

# --- Final stage ---
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check for CF Containers keep-alive
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --spider -q http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DualMind.API.dll"]
