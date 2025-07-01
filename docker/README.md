# ADAM-6051 Counter Logger - Docker Setup

Complete monitoring solution for ADAM-6051 industrial counter devices with InfluxDB 2.x and Grafana.

## Quick Start

### Prerequisites
- Docker and Docker Compose installed
- ADAM-6051 device accessible on your network
- Basic knowledge of your device's IP address

### 1. Start the Stack
```bash
cd docker
docker-compose up -d
```

### 2. Configure Your Device
Edit `config/adam_config.json` and update the ADAM device IP:
```json
{
    "modbus": {
        "host": "192.168.1.100",  ← Change this to your ADAM device IP
        ...
    }
}
```

### 3. Restart the Logger
```bash
docker-compose restart adam-logger
```

### 4. Access Dashboard
- **Grafana Dashboard**: http://localhost:3000 (admin/admin)
- **InfluxDB Web UI**: http://localhost:8086 (admin/admin123)

## Services

| Service | Port | Purpose | Credentials |
|---------|------|---------|-------------|
| Grafana | 3000 | Visualization dashboard | admin/admin |
| InfluxDB 2.x | 8086 | Time-series database | admin/admin123 |
| Logger | - | Background data collection | - |

## What's New in Version 2.x

### InfluxDB 2.7.12
- **Modern Architecture**: Latest stable InfluxDB with improved performance
- **Flux Query Language**: More powerful queries and data transformations  
- **Organization/Bucket Model**: Better data organization than old database model
- **Built-in Web UI**: Modern interface for data exploration at :8086

### Grafana 12.0.2
- **Latest Features**: Most recent stable Grafana with enhanced visualization
- **Better InfluxDB 2.x Support**: Native Flux query support
- **Improved Performance**: Faster dashboard loading and query execution
- **Enhanced Security**: Latest security features and fixes

## Configuration Options

### Environment Variables
Create a `.env` file from the template for quick configuration:
```bash
cp .env.template .env
# Edit .env with your settings
```

Key variables:
- `ADAM_HOST`: IP address of your ADAM-6051 device
- `ADAM_UNIT_ID`: Modbus unit ID (usually 1)
- `LOG_LEVEL`: DEBUG, INFO, WARNING, ERROR
- `POLL_INTERVAL`: Seconds between readings (default: 5.0)

### Configuration File
For advanced settings, edit `config/adam_config.json`:

```json
{
    "modbus": {
        "host": "192.168.1.100",     // Device IP address
        "port": 502,                 // Modbus TCP port
        "unit_id": 1,                // Device unit ID
        "timeout": 3,                // Connection timeout
        "retries": 3                 // Retry attempts
    },
    "influxdb": {
        "url": "http://influxdb:8086",      // InfluxDB 2.x URL
        "token": "adam-super-secret-token", // Access token
        "org": "adam_org",                  // Organization
        "bucket": "adam_counters",          // Data bucket
        "timeout": 5,                       // Connection timeout
        "retries": 3                        // Retry attempts
    },
    "counters": {
        "channels": [0, 1, 2, 3],    // Active counter channels
        "calculate_rate": true,       // Enable rate calculation
        "rate_window": 60            // Rate calculation window (seconds)
    },
    "device": {
        "name": "ADAM-6051",         // Device identifier
        "location": "Line_1",        // Location tag
        "description": "Counter 1"   // Description
    }
}
```

## InfluxDB 2.x Data Model

### Organization Structure
- **Organization**: `adam_org`
- **Bucket**: `adam_counters` (365-day retention)
- **Measurement**: `counter_data`

### Data Schema
```
counter_data,device=ADAM-6051,location=Line_1,channel=0 count=1234,rate=5.2 1640995200000000000
```

**Tags** (indexed for fast queries):
- `device`: Device name (e.g., "ADAM-6051")
- `location`: Physical location (e.g., "Line_1")  
- `channel`: Counter channel number (e.g., "0", "1")

**Fields** (actual data values):
- `count`: Current counter value (integer)
- `rate`: Counts per second (float, optional)

## Dashboard Features

The pre-built Grafana dashboard includes:

- **Real-time Counter Values**: Current count for each channel
- **Counter Trends**: Historical count data over time  
- **Count Rates**: Counts per second for each channel
- **Device Status**: Online/offline monitoring
- **Data Quality**: Freshness and missing data indicators
- **System Health**: Connection status and error monitoring

### Creating Custom Dashboards

InfluxDB 2.x uses Flux query language. Example queries:

```flux
// Get latest counter values
from(bucket: "adam_counters")
  |> range(start: -1h)
  |> filter(fn: (r) => r._measurement == "counter_data")
  |> filter(fn: (r) => r._field == "count")
  |> last()

// Calculate hourly averages
from(bucket: "adam_counters")
  |> range(start: -24h)
  |> filter(fn: (r) => r._measurement == "counter_data")
  |> filter(fn: (r) => r._field == "count")
  |> aggregateWindow(every: 1h, fn: mean)
```

## Data Management

### Data Retention
- **Default**: Data kept for 365 days in `adam_counters` bucket
- **Configurable**: Modify retention via InfluxDB 2.x UI or API
- **Automatic**: Old data automatically deleted when retention period expires

### Data Access
Query data using Flux in InfluxDB UI or API:

```bash
# Query via API
curl -X POST http://localhost:8086/api/v2/query \
  -H "Authorization: Token adam-super-secret-token" \
  -H "Content-Type: application/vnd.flux" \
  -d 'from(bucket:"adam_counters") |> range(start: -1h)'
```

## Troubleshooting

### Check Service Status
```bash
docker-compose ps
docker-compose logs adam-logger
docker-compose logs influxdb
docker-compose logs grafana
```

### Common Issues

**Logger not connecting to ADAM device:**
1. Verify device IP in `config/adam_config.json`
2. Check network connectivity: `ping <device_ip>`
3. Verify Modbus TCP port 502 is accessible
4. Check device unit ID configuration

**No data in InfluxDB:**
1. Check logger logs: `docker-compose logs adam-logger`
2. Verify InfluxDB 2.x connection: http://localhost:8086
3. Check bucket exists in InfluxDB UI
4. Verify token and organization configuration

**Dashboard not loading:**
1. Wait 60 seconds for initial provisioning
2. Check Grafana logs: `docker-compose logs grafana`
3. Verify datasource configuration in Grafana UI
4. Manually create dashboards using Flux queries

### Health Checks
```bash
# Check if services are healthy
docker-compose ps

# Test individual components
curl http://localhost:8086/health      # InfluxDB 2.x
curl http://localhost:3000/api/health  # Grafana
```

## Advanced Usage

### Multiple ADAM Devices
To monitor multiple devices:

1. Copy configuration file:
```bash
cp config/adam_config.json config/adam_device2.json
```

2. Update the second config with different IP and device name

3. Add second logger service to `docker-compose.yml`:
```yaml
adam-logger-2:
  build:
    context: ./python
  volumes:
    - ./config:/app/config
  environment:
    - CONFIG_FILE=/app/config/adam_device2.json
```

### Data Export
Export data using InfluxDB 2.x CLI:

```bash
# Export to CSV
docker exec adam-influxdb influx query \
  'from(bucket:"adam_counters") |> range(start: -24h)' \
  --format csv > data.csv

# Export specific time range
docker exec adam-influxdb influx query \
  'from(bucket:"adam_counters") |> range(start: 2024-01-01T00:00:00Z, stop: 2024-12-31T23:59:59Z)' \
  --format csv > data_2024.csv
```

## Security Considerations

### Production Deployment
For production use:

1. **Change default tokens** in `docker-compose.yml`
2. **Enable HTTPS** for Grafana (add reverse proxy)
3. **Restrict network access** using Docker networks
4. **Use proper authentication** for InfluxDB 2.x
5. **Regular backups** of InfluxDB 2.x data

### Secure Token Management
```yaml
# Use Docker secrets for production
secrets:
  influx_token:
    file: ./secrets/influx_token.txt
```

## Migration from InfluxDB 1.x

If upgrading from the previous version:

1. **Backup existing data** before migration
2. **Update queries** from InfluxQL to Flux syntax
3. **Recreate dashboards** with new datasource configuration
4. **Update application** to use InfluxDB 2.x client library

### Data Migration
```bash
# Export from InfluxDB 1.x
influx_inspect export -datadir /var/lib/influxdb/data -waldir /var/lib/influxdb/wal -out export.txt

# Import to InfluxDB 2.x (requires transformation)
# See InfluxDB documentation for migration tools
```

## Maintenance

### Backup Data
```bash
# Backup InfluxDB 2.x
docker exec adam-influxdb influx backup /backup --bucket adam_counters
docker cp adam-influxdb:/backup ./backup_$(date +%Y%m%d)

# Backup Grafana
docker exec adam-grafana tar -czf /tmp/grafana-backup.tar.gz /var/lib/grafana
docker cp adam-grafana:/tmp/grafana-backup.tar.gz ./grafana-backup_$(date +%Y%m%d).tar.gz
```

### Update Services
```bash
# Update to latest versions
docker-compose pull
docker-compose up -d
```

### Clean Up
```bash
# Remove everything (WARNING: Deletes all data)
docker-compose down -v

# Remove just containers (keeps data)
docker-compose down
```

## Support

### Logs Location
- Logger: `logs/adam_logger.log` (mounted from container)
- System: `docker-compose logs <service>`

### Performance Tuning
- Increase `poll_interval` for less frequent data collection
- Adjust InfluxDB 2.x retention policy for storage optimization
- Use Grafana query caching for large datasets

### Getting Help
1. Check service logs for error messages
2. Verify network connectivity to ADAM device
3. Test Modbus communication with diagnostic tools
4. Review configuration file syntax
5. Check InfluxDB 2.x UI for data presence

For additional support, refer to the main project documentation.