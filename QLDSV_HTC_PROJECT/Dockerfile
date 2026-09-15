# Multi-stage Dockerfile for QLDSV_HTC_PROJECT (.NET 8.0 Windows Forms)
# Implements multi-stage build to eliminate source code, PDB files, and temp artifacts
# from the final Windows container image deployed to EKS.
#
# Environment variables required (inject via Kubernetes Secrets / ConfigMaps):
#   DB_CONNECTION_STRING       - Full SQL Server connection string (from AWS Secrets Manager via IRSA)
#   HEALTH_CHECK_PORT          - Port for health check HTTP server (default: 8080)

# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build

WORKDIR /src

# Copy project file first for dependency caching
COPY ["QLDSV_HTC_PROJECT/QLDSV_HTC_PROJECT.csproj", "QLDSV_HTC_PROJECT/"]
COPY ["QLDSV_HTC_PROJECT/App.config", "QLDSV_HTC_PROJECT/"]

# Restore NuGet packages (cached layer)
RUN dotnet restore "QLDSV_HTC_PROJECT/QLDSV_HTC_PROJECT.csproj"

# Copy remaining source files
COPY . .

# Build and publish in Release mode (no debug symbols)
RUN dotnet publish "QLDSV_HTC_PROJECT/QLDSV_HTC_PROJECT.csproj" \
    -c Release \
    -o C:\app\publish \
    --no-restore \
    /p:DebugType=None \
    /p:DebugSymbols=false

# ─── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2 AS runtime

WORKDIR /app

# Copy only the compiled output from the build stage (no source, no PDB files)
COPY --from=build C:\app\publish\ .

# Environment variables are injected at runtime via Kubernetes Secrets / ConfigMaps
# DB_CONNECTION_STRING and HEALTH_CHECK_PORT must be set in the pod spec
ENV HEALTH_CHECK_PORT=8080

# Expose health check port
EXPOSE 8080

ENTRYPOINT ["QLDSV_HTC_PROJECT.exe"]
