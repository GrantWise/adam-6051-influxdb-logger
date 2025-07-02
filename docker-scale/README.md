# ADAM Scale Logger - Industrial Grade Container Solution

Production-ready monitoring solution for ADAM-4571 weight scale devices with PostgreSQL storage and Grafana visualization. Built following industrial software development standards.

## Quick Start

### Prerequisites
- Docker and Docker Compose installed
- ADAM-4571 device accessible on your network
- Basic knowledge of your device's IP address

### 1. Start the Complete Stack
```bash
cd docker-scale
docker-compose up -d
```

### 2. Configure Your Device
Create configuration file from template:
```bash
cp .env.template .env
```

Edit `.env` and update the ADAM device IP:
```bash
ADAM_HOST=192.168.1.101  # ← Change to your ADAM device IP
ADAM_PORT=4001           # ← TCP port (usually 4001)
DEVICE_NAME=Scale_Line_1 # ← Descriptive name
DEVICE_LOCATION=Plant_A  # ← Location identifier
```

### 3. Restart the Logger
```bash
docker-compose restart adam-scale-logger
```

### 4. Access Interfaces
- **Grafana Dashboard**: http://localhost:3000 (admin/admin)
- **Health Monitoring**: http://localhost:8080/health
- **PostgreSQL**: localhost:5432 (adam_user/adam_secure_password)

## Architecture Overview

### Services

| Service | Port | Purpose | Technology |
|---------|------|---------|------------|
| PostgreSQL | 5432 | Primary database | PostgreSQL 15.4 |
| Grafana | 3000 | Visualization | Grafana 10.1.5 |
| Scale Logger | 8080 | Data collection | Python 3.11 |
| Discovery | - | Protocol discovery | Python 3.11 |

### Key Features

**Industrial Grade Reliability**:
- Automatic reconnection on network failures
- Comprehensive error handling and retry logic
- Health monitoring with REST API endpoints
- Graceful shutdown handling

**Production Architecture**:
- SOLID principles implementation
- Separate database implementations (PostgreSQL/SQLite)
- Factory pattern for database selection
- Dependency injection via configuration

**Container-Native Design**:
- Docker health checks for all services
- Environment variable configuration
- Persistent data volumes
- Service dependency management

## Configuration Options

### Environment Variables

Key variables for quick configuration:

```bash
# Device Connection
ADAM_HOST=192.168.1.101          # Device IP address
ADAM_PORT=4001                   # TCP port (default: 4001)

# Database Selection
DATABASE_TYPE=postgresql         # postgresql or sqlite
DATABASE_PASSWORD=secure_pass    # PostgreSQL password

# Monitoring
LOG_LEVEL=INFO                   # DEBUG, INFO, WARNING, ERROR
POLL_INTERVAL=10.0              # Seconds between readings
DEVICE_NAME=Production_Scale     # Device identifier
DEVICE_LOCATION=Line_1          # Location tag
```

### Advanced Configuration

For advanced settings, create `config/scale_config.json`:

```json
{
    "adam4571": {
        "host": "192.168.1.101",
        "port": 4001,
        "timeout": 5,
        "reconnect_delay": 2.0,
        "buffer_size": 1024
    },
    "database": {
        "type": "postgresql",
        "postgresql": {
            "host": "postgres",
            "database": "adam_industrial",
            "pool_size": 5
        }
    },
    "monitoring": {
        "poll_interval": 10.0,
        "health_check_interval": 60.0,
        "max_consecutive_failures": 5
    },
    "device": {
        "name": "ADAM-4571-Scale",
        "location": "production_line_1",
        "description": "Industrial weight scale"
    }
}
```

## Database Schema

### PostgreSQL Tables

**scale_readings** - Primary data table:
```sql
CREATE TABLE scale_readings (
    id SERIAL PRIMARY KEY,
    device_id VARCHAR(50) NOT NULL,
    weight DECIMAL(10,3),
    unit VARCHAR(10),
    stable BOOLEAN,
    raw_data TEXT,
    quality_flags JSONB,
    timestamp TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
```

**Indexes** for performance:
- `idx_scale_readings_timestamp` - Time-based queries
- `idx_scale_readings_device` - Device filtering
- `idx_scale_readings_created` - Data management

### Data Access Examples

**Latest readings**:
```sql
SELECT * FROM scale_readings 
WHERE device_id = 'ADAM-4571-Scale' 
ORDER BY timestamp DESC 
LIMIT 10;
```

**Hourly averages**:
```sql
SELECT 
    DATE_TRUNC('hour', timestamp) as hour,
    AVG(weight) as avg_weight,
    COUNT(*) as reading_count
FROM scale_readings 
WHERE timestamp >= NOW() - INTERVAL '24 hours'
    AND weight IS NOT NULL
GROUP BY hour
ORDER BY hour;
```

## Protocol Discovery

### Automatic Discovery Process

Run protocol discovery for unknown scales:

```bash
# Interactive discovery mode
docker-compose --profile discovery run --rm adam-scale-discovery

# Test specific device connectivity
docker-compose run --rm adam-scale-discovery --test
```

### Discovery Process

1. **Template Matching**: Tests known manufacturer templates first
2. **Interactive Discovery**: Guided process if templates fail
3. **Template Generation**: Creates custom protocol templates
4. **Validation**: Tests generated templates against live data

## Operational Procedures

### Health Monitoring

**Service Health Check**:
```bash
curl http://localhost:8080/health
```

Response format:
```json
{
  "healthy": true,
  "timestamp": "2024-07-02T10:30:15.123Z",
  "service": "adam-scale-logger",
  "version": "1.0.0",
  "checks": {
    "adam_device": {
      "status": "healthy",
      "message": "Connected to 192.168.1.101:4001"
    },
    "database": {
      "status": "healthy", 
      "message": "PostgreSQL reachable"
    }
  }
}
```

**Service Status**:
```bash
docker-compose ps
docker-compose logs adam-scale-logger
```

### Data Management

**Backup Database**:
```bash
# PostgreSQL backup
docker exec adam-scale-postgres pg_dump -U adam_user adam_industrial > backup.sql

# Restore from backup
docker exec -i adam-scale-postgres psql -U adam_user adam_industrial < backup.sql
```

**View Recent Data**:
```bash
# Connect to database
docker exec -it adam-scale-postgres psql -U adam_user adam_industrial

# Query recent readings
SELECT timestamp, weight, unit, stable FROM scale_readings ORDER BY timestamp DESC LIMIT 10;
```

### Troubleshooting

**Common Issues**:

1. **Scale not connecting**:
   ```bash
   # Check device connectivity
   docker-compose run --rm adam-scale-logger --test
   
   # Check network connectivity
   docker exec adam-scale-logger ping 192.168.1.101
   ```

2. **No data in database**:
   ```bash
   # Check logger status
   docker-compose logs adam-scale-logger
   
   # Check database connection
   docker-compose logs postgres
   ```

3. **Dashboard not loading**:
   ```bash
   # Check Grafana logs
   docker-compose logs grafana
   
   # Verify datasource connection in Grafana UI
   ```

### Performance Tuning

**For High-Frequency Scales**:
- Reduce `POLL_INTERVAL` to 1-5 seconds
- Increase PostgreSQL `shared_buffers`
- Consider database partitioning for large datasets

**For Multiple Devices**:
- Deploy separate stacks per device
- Use unique `DEVICE_NAME` for each deployment
- Aggregate data in central dashboard

## Security Considerations

### Production Deployment

**Essential Security Steps**:

1. **Change Default Passwords**:
   ```bash
   # Update all passwords in .env file
   DATABASE_PASSWORD=your_secure_password
   # Update Grafana admin password in docker-compose.yml
   ```

2. **Network Security**:
   ```bash
   # Restrict network access
   # Use Docker secrets for sensitive data
   # Enable SSL/TLS for external connections
   ```

3. **Data Protection**:
   ```bash
   # Regular backups
   # Data retention policies
   # Access logging and monitoring
   ```

## Scaling and Extensions

### Multiple Production Lines

Deploy one stack per production line:

```bash
# Line 1
ADAM_HOST=192.168.1.101 DEVICE_NAME=Scale_Line_1 docker-compose up -d

# Line 2  
ADAM_HOST=192.168.1.102 DEVICE_NAME=Scale_Line_2 docker-compose -f docker-compose.yml -p line2 up -d
```

### Integration with OEE Systems

Query scale data via PostgreSQL:

```python
import psycopg2

def get_production_weights(start_time, end_time):
    conn = psycopg2.connect(
        host="localhost",
        database="adam_industrial", 
        user="adam_user",
        password="adam_secure_password"
    )
    
    cursor = conn.cursor()
    cursor.execute("""
        SELECT timestamp, weight, stable 
        FROM scale_readings 
        WHERE timestamp BETWEEN %s AND %s
        AND weight IS NOT NULL
        ORDER BY timestamp
    """, (start_time, end_time))
    
    return cursor.fetchall()
```

## Support and Maintenance

### Regular Maintenance

**Monthly Tasks**:
- Review logs for communication issues
- Check database size and performance
- Verify backup procedures
- Update Docker images

**Quarterly Tasks**:
- Full backup and restore test
- Security review and password updates
- Performance optimization review

### Getting Help

1. **Check Service Logs**:
   ```bash
   docker-compose logs adam-scale-logger
   ```

2. **Verify Configuration**:
   ```bash
   # Test device connectivity
   docker-compose run --rm adam-scale-logger --test
   ```

3. **Check Health Status**:
   ```bash
   curl http://localhost:8080/health
   ```

4. **Review Documentation**:
   - Industrial Software Development Standards
   - ADAM-4571 device manual
   - Docker and PostgreSQL documentation

---

## Industrial Standards Compliance

This implementation follows our **Industrial Software Development Standards** including:

- ✅ **Pragmatic SOLID Principles** - Clean architecture with proper separation
- ✅ **Industrial Communication Patterns** - Robust connection and retry handling  
- ✅ **Error Handling Excellence** - Comprehensive error recovery and logging
- ✅ **Configuration Management** - Environment-aware configuration with validation
- ✅ **Container-Native Architecture** - Production-ready Docker deployment
- ✅ **Observable Operations** - Health checks, monitoring, and structured logging
- ✅ **Production Readiness** - Graceful shutdown, signal handling, and service management

For detailed standards documentation, see: `docs/Industrial-Software-Development-Standards.md`