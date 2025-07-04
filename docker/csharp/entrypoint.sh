#!/bin/bash
# ADAM-6051 C# Logger Entrypoint Script

set -e

echo "Starting ADAM-6051 C# Counter Logger..."
echo "Configuration:"
echo "  ADAM_HOST: ${ADAM_HOST:-192.168.1.100}"
echo "  ADAM_UNIT_ID: ${ADAM_UNIT_ID:-1}"
echo "  POLL_INTERVAL: ${POLL_INTERVAL:-2000}ms"
echo "  LOG_LEVEL: ${LOG_LEVEL:-Information}"

# Wait for InfluxDB to be ready
echo "Waiting for InfluxDB to be ready..."
until curl -f http://influxdb:8086/health > /dev/null 2>&1; do
    echo "InfluxDB is not ready yet, waiting..."
    sleep 5
done
echo "InfluxDB is ready!"

# Set logging configuration
export "Logging__LogLevel__Default"=${LOG_LEVEL:-Information}
export "Logging__LogLevel__Industrial__Adam__Logger"="Debug"

# Set ADAM device configuration via environment variables
export AdamLogger__Devices__0__DeviceId="DOCKER_ADAM_001"
export AdamLogger__Devices__0__IpAddress="modbus-simulator"  # Connect to simulator service
export AdamLogger__Devices__0__Port=502
export AdamLogger__Devices__0__UnitId=${ADAM_UNIT_ID:-1}
export AdamLogger__Devices__0__TimeoutMs=5000
export AdamLogger__Devices__0__MaxRetries=3

# Configure channel 0
export AdamLogger__Devices__0__Channels__0__ChannelNumber=0
export AdamLogger__Devices__0__Channels__0__Name="ProductionCounter"
export AdamLogger__Devices__0__Channels__0__StartRegister=0
export AdamLogger__Devices__0__Channels__0__RegisterCount=2
export AdamLogger__Devices__0__Channels__0__Enabled=true
export AdamLogger__Devices__0__Channels__0__MinValue=0
export AdamLogger__Devices__0__Channels__0__MaxValue=4294967295

# Configure ADAM Logger settings
export AdamLogger__PollIntervalMs=${POLL_INTERVAL:-2000}
export AdamLogger__HealthCheckIntervalMs=30000
export AdamLogger__MaxConcurrentDevices=1

# Configure InfluxDB connection
export AdamLogger__InfluxDb__Url="http://influxdb:8086"
export AdamLogger__InfluxDb__Token="adam-super-secret-token"
export AdamLogger__InfluxDb__Organization="adam_org"
export AdamLogger__InfluxDb__Bucket="adam_counters"
export AdamLogger__InfluxDb__Measurement="counter_data"
export AdamLogger__InfluxDb__WriteBatchSize=50
export AdamLogger__InfluxDb__FlushIntervalMs=5000

echo "Starting application with configuration applied..."

# Debug: Show all environment variables starting with AdamLogger
echo "Environment variables:"
env | grep -E "(AdamLogger|Logging)" | sort

# Start the application
echo "Executing: dotnet Industrial.Adam.Logger.Examples.dll"
exec dotnet Industrial.Adam.Logger.Examples.dll "$@"