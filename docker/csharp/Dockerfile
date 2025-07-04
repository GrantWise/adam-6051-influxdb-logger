# ADAM-6051 C# Logger Dockerfile
# Multi-stage build for optimized production image

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Industrial.Adam.Logger.sln ./
COPY src/Industrial.Adam.Logger/Industrial.Adam.Logger.csproj ./src/Industrial.Adam.Logger/
COPY src/Industrial.Adam.Logger.Examples/Industrial.Adam.Logger.Examples.csproj ./src/Industrial.Adam.Logger.Examples/
COPY src/Industrial.Adam.Logger.Tests/Industrial.Adam.Logger.Tests.csproj ./src/Industrial.Adam.Logger.Tests/
COPY src/Industrial.Adam.Logger.IntegrationTests/Industrial.Adam.Logger.IntegrationTests.csproj ./src/Industrial.Adam.Logger.IntegrationTests/

# Restore dependencies for the solution
RUN dotnet restore Industrial.Adam.Logger.sln

# Copy source code
COPY src/ ./src/

# Build and publish the application
RUN dotnet publish ./src/Industrial.Adam.Logger.Examples/Industrial.Adam.Logger.Examples.csproj \
    -c Release -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish ./

# Copy entrypoint script
COPY docker/csharp/entrypoint.sh ./entrypoint.sh
RUN chmod +x ./entrypoint.sh

# Create directories for configuration and logs
RUN mkdir -p /app/config /app/logs

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production

# Health check - verify the dotnet process is running
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD pgrep -f dotnet || exit 1

# Run the application through entrypoint script
ENTRYPOINT ["./entrypoint.sh"]