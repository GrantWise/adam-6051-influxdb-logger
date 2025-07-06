#!/bin/bash
set -e

# Function to log messages
log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1"
}

log "Starting ADAM-6051 Counter Logger..."

# Wait for InfluxDB 2.x to be ready
log "Waiting for InfluxDB 2.x to be ready..."
for i in {1..30}; do
    if curl -f http://influxdb:8086/health >/dev/null 2>&1; then
        log "InfluxDB 2.x is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        log "ERROR: InfluxDB 2.x is not responding after 30 attempts"
        exit 1
    fi
    log "InfluxDB 2.x not ready, waiting... (attempt $i/30)"
    sleep 2
done

# Check if config file exists, if not use default path
CONFIG_FILE="/app/config/adam_config.json"
if [ ! -f "$CONFIG_FILE" ]; then
    log "Config file not found at $CONFIG_FILE, creating default..."
    CONFIG_FILE="adam_config.json"
fi

# Update configuration with environment variables if provided
if [ ! -z "$ADAM_HOST" ] || [ ! -z "$ADAM_UNIT_ID" ] || [ ! -z "$LOG_LEVEL" ] || [ ! -z "$POLL_INTERVAL" ]; then
    log "Updating configuration with environment variables..."
    
    # Create a temporary Python script to update config
    cat > update_config.py << 'EOF'
import json
import os
import sys

config_file = sys.argv[1] if len(sys.argv) > 1 else "adam_config.json"

# Try to load existing config
try:
    with open(config_file, 'r') as f:
        config = json.load(f)
except FileNotFoundError:
    # Use default config if file doesn't exist
    config = {
        "modbus": {
            "host": "192.168.1.100",
            "port": 502,
            "unit_id": 1,
            "timeout": 3,
            "retry_on_empty": True,
            "retry_on_invalid": True,
            "retries": 3,
            "retry_delay": 1.0
        },
        "influxdb": {
            "url": "http://influxdb:8086",
            "token": "adam-super-secret-token",
            "org": "adam_org",
            "bucket": "adam_counters",
            "timeout": 5,
            "retries": 3
        },
        "logging": {
            "poll_interval": 5.0,
            "log_level": "INFO",
            "log_file": "/app/logs/adam_logger.log",
            "max_log_size_mb": 10,
            "backup_count": 5
        },
        "counters": {
            "channels": [0, 1],
            "calculate_rate": True,
            "rate_window": 60,
            "overflow_threshold": 4294967000
        },
        "device": {
            "name": "ADAM-6051",
            "location": "default",
            "description": "Counter monitoring device"
        }
    }

# Update with environment variables
if os.getenv('ADAM_HOST'):
    config['modbus']['host'] = os.getenv('ADAM_HOST')
    print(f"Updated ADAM host to: {config['modbus']['host']}")

if os.getenv('ADAM_UNIT_ID'):
    config['modbus']['unit_id'] = int(os.getenv('ADAM_UNIT_ID'))
    print(f"Updated ADAM unit ID to: {config['modbus']['unit_id']}")

if os.getenv('LOG_LEVEL'):
    config['logging']['log_level'] = os.getenv('LOG_LEVEL')
    print(f"Updated log level to: {config['logging']['log_level']}")

if os.getenv('POLL_INTERVAL'):
    config['logging']['poll_interval'] = float(os.getenv('POLL_INTERVAL'))
    print(f"Updated poll interval to: {config['logging']['poll_interval']}s")

# Ensure InfluxDB host points to container
config['influxdb']['host'] = 'influxdb'

# Write updated config
with open(config_file, 'w') as f:
    json.dump(config, f, indent=4)

print(f"Configuration updated and saved to: {config_file}")
EOF

    python update_config.py "$CONFIG_FILE"
fi

log "Starting ADAM Counter Logger with config: $CONFIG_FILE"

# Run the logger application
exec python adam_counter_logger.py --config "$CONFIG_FILE"