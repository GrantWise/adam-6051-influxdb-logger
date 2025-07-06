# ADAM-6051 Containerized Counter Logger - Functional Specification

## Overview

The ADAM-6051 Containerized Counter Logger is a complete, production-ready monitoring solution that connects to Advantech ADAM-6051 industrial counter devices and automatically logs counter data to a time-series database. It's designed as a plug-and-play solution for OEE (Overall Equipment Effectiveness) and production monitoring systems.

## What It Does

### Core Functionality

**Automated Data Collection**
- Continuously monitors up to 8 counter channels on ADAM-6051 devices
- Polls counter values every 5 seconds (configurable from 1-3600 seconds)
- Handles 32-bit counter values with automatic overflow detection
- Calculates real-time count rates (counts per second/minute/hour)

**Industrial-Grade Reliability**
- Automatic reconnection on network failures
- Comprehensive error handling and retry logic
- 24/7 operation with Docker container restart policies
- Detailed logging for troubleshooting and maintenance

**Time-Series Data Storage**
- Stores all data in InfluxDB 2.x for high-performance queries
- Maintains 365-day data retention (configurable)
- Tags data with device, location, and channel information
- Preserves microsecond timestamp precision for accurate analysis

## Multiple Counter Applications for OEE

### Typical OEE Counter Setup

The ADAM-6051 provides **8 independent counter channels**, commonly used as:

**Production Monitoring**:
- **Channel 0**: Total Production Count (good + bad parts)
- **Channel 1**: Reject/Scrap Count (defective parts)
- **Channel 2**: Machine Cycle Count
- **Channel 3**: Downtime Events
- **Additional channels**: Multiple production lines, quality stations, etc.

**OEE Calculation Benefits**:
```
Good Parts = Total Production - Reject Count
Quality Rate = Good Parts / Total Production
Performance Rate = Actual Cycle Time / Ideal Cycle Time
Availability = (Total Time - Downtime) / Total Time
OEE = Availability × Performance × Quality
```

### Data Structure for OEE

Each counter provides rich data for analysis:

```
Device: ADAM-6051, Location: Line_1, Channel: 0
├── Count: 15,847 (total production)
├── Rate: 12.5 parts/minute (current rate)
└── Timestamp: 2024-07-02T10:30:15.123Z

Device: ADAM-6051, Location: Line_1, Channel: 1  
├── Count: 236 (reject count)
├── Rate: 0.8 rejects/minute (current reject rate)
└── Timestamp: 2024-07-02T10:30:15.123Z
```

## Complete Monitoring Solution

### What's Included

**1. Counter Logger Service**
- Python application in Docker container
- Modbus TCP communication with ADAM-6051
- Automatic data collection and storage

**2. InfluxDB 2.x Database**
- High-performance time-series database
- Web-based data explorer interface
- RESTful API for data retrieval

**3. Grafana Dashboard**
- Pre-built counter monitoring dashboards
- Real-time visualization
- Alerting capabilities

**4. Complete Docker Stack**
- One-command deployment: `docker-compose up -d`
- Automatic service dependencies and health checks
- Persistent data storage

## Deployment and Setup

### Quick Start Process

**1. Deploy the Stack**
```bash
cd docker
docker-compose up -d
```

**2. Configure Your Device**
Edit `config/adam_config.json`:
```json
{
    "modbus": {
        "host": "192.168.1.100",  // ← Your ADAM device IP
        "port": 502,
        "unit_id": 1
    },
    "counters": {
        "channels": [0, 1, 2, 3]  // ← Active counter channels
    },
    "device": {
        "name": "Production_Line_1",
        "location": "Plant_A_Floor_2"
    }
}
```

**3. Restart Logger**
```bash
docker-compose restart adam-logger
```

**4. Access Interfaces**
- **Data Visualization**: http://localhost:3000 (Grafana)
- **Database Interface**: http://localhost:8086 (InfluxDB)
- **Your OEE Software**: Query InfluxDB API

### Multiple Machine Deployment

For multiple production lines, deploy one stack per machine:

```bash
# Machine 1
ADAM_HOST=192.168.1.100 docker-compose up -d

# Machine 2  
ADAM_HOST=192.168.1.101 docker-compose up -d
```

Each deployment creates isolated data collection with unique device tags.

## Data Retrieval for OEE Software

### InfluxDB Query Examples

**Get Latest Counter Values**
```flux
from(bucket: "adam_counters")
  |> range(start: -5m)
  |> filter(fn: (r) => r._measurement == "counter_data")
  |> filter(fn: (r) => r._field == "count")
  |> last()
```

**Production vs Reject Analysis**
```flux
from(bucket: "adam_counters")
  |> range(start: -8h)
  |> filter(fn: (r) => r._measurement == "counter_data")
  |> filter(fn: (r) => r.channel == "0" or r.channel == "1")
  |> pivot(rowKey:["_time"], columnKey: ["channel"], valueColumn: "_value")
```

**Hourly Production Rates**
```flux
from(bucket: "adam_counters")
  |> range(start: -24h)
  |> filter(fn: (r) => r._field == "rate")
  |> aggregateWindow(every: 1h, fn: mean)
```

### REST API Access

Your OEE software can retrieve data via HTTP API:

```bash
curl -X POST http://localhost:8086/api/v2/query \
  -H "Authorization: Token adam-super-secret-token" \
  -H "Content-Type: application/vnd.flux" \
  -d 'from(bucket:"adam_counters") |> range(start: -1h)'
```

**Response Format**:
```json
{
  "device": "Production_Line_1",
  "location": "Plant_A_Floor_2", 
  "channel": "0",
  "count": 15847,
  "rate": 12.5,
  "timestamp": "2024-07-02T10:30:15.123Z"
}
```

## OEE Software Integration

### Simple Integration Pattern

Your OEE software needs minimal code to access the data:

```python
from influxdb_client import InfluxDBClient

class ProductionDataService:
    def __init__(self):
        self.client = InfluxDBClient(
            url="http://localhost:8086",
            token="adam-super-secret-token",
            org="adam_org"
        )
    
    def get_production_counts(self, start_time, end_time):
        """Get production and reject counts for OEE calculation"""
        query = f'''
        from(bucket: "adam_counters")
          |> range(start: {start_time}, stop: {end_time})
          |> filter(fn: (r) => r._measurement == "counter_data")
          |> filter(fn: (r) => r._field == "count")
          |> last()
        '''
        return self.client.query(query)
    
    def get_current_rates(self):
        """Get real-time production rates"""
        query = '''
        from(bucket: "adam_counters")
          |> range(start: -5m)
          |> filter(fn: (r) => r._field == "rate")
          |> last()
        '''
        return self.client.query(query)
```

### Configuration Management Interface

Your OEE software can provide a simple web form to update device settings:

```python
import json

def update_adam_config(device_ip, channels, device_name, location):
    """Update ADAM device configuration via your OEE interface"""
    config = {
        "modbus": {"host": device_ip},
        "counters": {"channels": channels},
        "device": {
            "name": device_name,
            "location": location
        }
    }
    
    with open('/docker/config/adam_config.json', 'w') as f:
        json.dump(config, f, indent=4)
    
    # Restart logger container to apply changes
    os.system('docker-compose restart adam-logger')
```

## Operational Benefits

### For Production Managers
- **Real-time Production Monitoring**: Live counter values and rates
- **Historical Analysis**: Trend analysis over days, weeks, months
- **Quality Tracking**: Automatic reject rate calculations
- **Downtime Detection**: Gap analysis in counter data

### For Maintenance Teams
- **Device Health Monitoring**: Connection status and communication errors
- **Predictive Maintenance**: Counter rate degradation analysis
- **Troubleshooting**: Detailed logs for device connectivity issues

### For OEE Analysis
- **Accurate Data**: Microsecond timestamp precision
- **Multiple Metrics**: Count, rate, and derived calculations
- **Flexible Queries**: SQL-like queries for complex analysis
- **Data Export**: CSV export for external analysis tools

## Scalability and Performance

### Production Scale
- **Single Device**: Up to 8 simultaneous counters
- **Multiple Devices**: One container stack per ADAM-6051 device
- **Data Volume**: ~1KB per reading × channels × poll frequency
- **Retention**: 365 days of data (configurable)

### Performance Characteristics
- **Poll Frequency**: 1-3600 seconds (typically 5 seconds)
- **Response Time**: <100ms for data queries
- **Throughput**: Handles high-speed counters (>10,000 counts/minute)
- **Reliability**: 99.9%+ uptime with proper network infrastructure

## Monitoring and Maintenance

### Health Monitoring
- Docker health checks ensure service availability
- Automatic restart on failures
- Connection status logging
- Data quality validation

### Maintenance Tasks
- **Monthly**: Review logs for communication issues
- **Quarterly**: Backup InfluxDB data
- **Annually**: Update container images

### Troubleshooting Support
- Detailed error logging with timestamps
- Network connectivity diagnostics
- Modbus communication debugging
- Data validation and consistency checks

## Summary

The ADAM-6051 Containerized Counter Logger provides a complete, industrial-grade solution for production counter monitoring. It eliminates the complexity of Modbus communication, counter overflow handling, and data storage, allowing your OEE software to focus on analysis and reporting rather than data collection infrastructure.

**Key Benefits**:
- **Plug-and-play deployment** with Docker
- **Multiple counter support** for production/reject tracking
- **Industrial reliability** with comprehensive error handling
- **Simple data access** via InfluxDB API
- **Scalable architecture** for multiple production lines
- **Rich data format** optimized for OEE calculations

Your OEE software simply needs to query InfluxDB for the counter data and perform OEE calculations, while the containerized logger handles all the complex industrial communication and data storage requirements.