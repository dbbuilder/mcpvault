# Multi-stage build for MCPVault API

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/MCPVault.API/MCPVault.API.csproj", "src/MCPVault.API/"]
COPY ["src/MCPVault.Core/MCPVault.Core.csproj", "src/MCPVault.Core/"]
COPY ["src/MCPVault.Infrastructure/MCPVault.Infrastructure.csproj", "src/MCPVault.Infrastructure/"]
COPY ["src/MCPVault.Domain/MCPVault.Domain.csproj", "src/MCPVault.Domain/"]
RUN dotnet restore "src/MCPVault.API/MCPVault.API.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/MCPVault.API"
RUN dotnet build "MCPVault.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MCPVault.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# Install required runtime dependencies
RUN apk add --no-cache \
    icu-libs \
    krb5-libs \
    libgcc \
    libintl \
    libssl3 \
    libstdc++ \
    zlib

# Create non-root user
RUN addgroup -g 1000 -S mcpvault && \
    adduser -u 1000 -S mcpvault -G mcpvault

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R mcpvault:mcpvault /app

# Switch to non-root user
USER mcpvault

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose ports
EXPOSE 8080
EXPOSE 8443

# Set environment variables
ENV ASPNETCORE_URLS="http://+:8080;https://+:8443" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Entry point
ENTRYPOINT ["dotnet", "MCPVault.API.dll"]