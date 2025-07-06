# ADAM Industrial Data Acquisition Platform - Python Tools

This directory contains lightweight Python applications for rapid prototyping and testing of ADAM device connectivity and protocol discovery.

## Applications

### 1. ADAM-6051 Counter Logger (`adam_counter_logger.py`)
Production-ready Modbus TCP data acquisition for ADAM-6051 counter modules.

**Features:**
- Modbus TCP communication with comprehensive error handling
- Rate calculation and overflow detection
- InfluxDB time-series data storage
- 24/7 continuous operation capability
- Configurable retry logic and connection management

**Usage:**
```bash
# Run with default configuration
python adam_counter_logger.py

# Use custom configuration file
python adam_counter_logger.py --config custom_config.json

# Test connectivity and single reading
python adam_counter_logger.py --test
```

### 2. ADAM-4571 Scale Discovery (`adam_scale_discovery.py`)
Intelligent protocol discovery for weight scales connected via ADAM-4571 serial-to-Ethernet converters.

**Features:**
- Automatic protocol discovery through guided weight testing
- TCP socket communication to ADAM-4571
- Interactive confidence-based discovery process
- Protocol template generation and storage
- Transport-agnostic design for future extensibility

**Usage:**
```bash
# Test connection to ADAM-4571
python adam_scale_discovery.py --test

# Run interactive protocol discovery
python adam_scale_discovery.py --discover

# Use custom configuration
python adam_scale_discovery.py --config custom_adam_scale_config.json --discover
```

## Installation

1. **Install Python 3.8+ and pip**

2. **Install dependencies:**
```bash
pip install -r requirements.txt
```

3. **For PostgreSQL connectivity (optional):**
```bash
# On Ubuntu/Debian
sudo apt-get install libpq-dev
pip install psycopg2-binary

# On macOS
brew install postgresql
pip install psycopg2-binary

# On Windows
pip install psycopg2-binary
```

4. **SQLite is built-in to Python (no additional installation needed)**

## Configuration

### ADAM-6051 Counter Configuration (`adam_config_json.json`)
```json
{
  "modbus": {
    "host": "192.168.1.100",
    "port": 502,
    "unit_id": 1,
    "timeout": 3,
    "retries": 3
  },
  "influxdb": {
    "url": "http://localhost:8086",
    "token": "your-token",
    "org": "your-org",
    "bucket": "adam_counters"
  },
  "counters": {
    "channels": [0, 1],
    "calculate_rate": true,
    "rate_window": 60
  }
}
```

### ADAM-4571 Scale Configuration (`adam_scale_config.json`)
```json
{
  "adam4571": {
    "host": "192.168.1.101",
    "port": 4001,
    "timeout": 5,
    "buffer_size": 1024
  },
  "discovery": {
    "confidence_threshold": 85.0,
    "max_iterations": 10,
    "frame_timeout": 2.0
  },
  "storage": {
    "database": {
      "type": "sqlite",
      "sqlite": {
        "database_file": "adam_weight_data.db"
      },
      "postgresql": {
        "host": "localhost",
        "port": 5432,
        "database": "adam_industrial",
        "username": "adam_user",
        "password": "adam_password",
        "timeout": 5
      }
    }
  }
}
```

## Database Configuration

The scale discovery application supports both lightweight SQLite (default) and production PostgreSQL databases:

### SQLite (Default - No Setup Required)
- **Pros**: Zero configuration, lightweight, perfect for testing and small deployments
- **Cons**: Single-user, limited concurrent access
- **Usage**: Ideal for proof-of-concept, development, and single-scale deployments

### PostgreSQL (Production Option)
- **Pros**: Multi-user, ACID transactions, excellent for enterprise deployments
- **Cons**: Requires database server setup
- **Usage**: Recommended for production environments with multiple devices

### Setup PostgreSQL (Optional)
```bash
# Install PostgreSQL
sudo apt-get install postgresql postgresql-contrib

# Create database and user
sudo -u postgres psql
CREATE DATABASE adam_industrial;
CREATE USER adam_user WITH PASSWORD 'adam_password';
GRANT ALL PRIVILEGES ON DATABASE adam_industrial TO adam_user;
\q

# Update configuration
# Change "type": "sqlite" to "type": "postgresql" in adam_scale_config.json
```

### Database Schema
Both databases automatically create these tables:
- **weight_readings**: Stores discrete weight measurements with timestamps
- **protocol_templates**: Stores discovered scale protocol definitions

## Protocol Discovery Process

The scale discovery application uses an innovative **ground truth approach**:

1. **Baseline Capture** - Record empty scale data
2. **Interactive Weight Testing** - Add known weights and correlate with data changes
3. **Confidence Scoring** - Real-time analysis of detection accuracy
4. **Template Generation** - Create reusable protocol definitions
5. **Validation** - Test parsing accuracy with generated templates

### Example Discovery Session
```
📊 WEIGHT SCALE PROTOCOL DISCOVERY
=====================================

Step 1: Baseline Reading (Empty Scale)
Please ensure the scale is empty and stable.
Press Enter when ready to capture baseline...

📋 Capturing baseline data for 5 seconds...
✅ Captured 12 baseline data points
📄 Sample data:
   'US    0.00 kg'
   'US    0.00 kg'

Step 2: Weight Test - 1.0 kg
Please place a 1.0 kg weight on the scale.
Press Enter when scale is stable...

📊 Capturing data for 1.0 kg...
✅ Captured 8 data points
📄 Sample data:
   'ST    1.00 kg'
   'ST    1.00 kg'

🔍 Confidence: Format=100.0%, Numeric=100.0%, Overall=100.0%
🎯 High confidence achieved (100.0%)

🎯 DISCOVERY COMPLETE
=====================
✅ Protocol template saved: protocol_templates/discovered_1656789123.json

📋 Template Summary:
   ID: discovered_1656789123
   Name: Auto-Discovered Scale Protocol
   Confidence: 100.0%
   Fields: 1
```

## Protocol Templates

### Known Manufacturer Templates

The system includes pre-built templates for major scale manufacturers:

- **`mettler_toledo_mt_standard.json`** - Mettler Toledo MT-SICS protocol with STX/ETX framing
- **`and_weighing_fx_series.json`** - A&D Weighing FX series CSV-style format  
- **`ohaus_defender_series.json`** - Ohaus Defender/Ranger simple space-padded format
- **`sartorius_standard.json`** - Sartorius laboratory scales with high precision
- **`avery_weigh_tronix_standard.json`** - Avery Weigh-Tronix industrial scales
- **`generic_chinese_scales.json`** - Common format for generic Chinese scales

### Using Known Templates

Instead of running discovery, you can directly apply a known template:

```bash
# Test with known Mettler Toledo protocol
python adam_scale_discovery.py --template mettler_toledo_mt_standard

# Verify A&D Weighing template against your scale
python adam_scale_discovery.py --verify and_weighing_fx_series
```

See `docs/Known Scale Protocols.md` for detailed information about each manufacturer's protocol.

### Discovered Protocol Templates

Auto-discovered protocols are saved as JSON templates in the `protocol_templates/` directory:

```json
{
  "template_id": "discovered_1656789123",
  "name": "Auto-Discovered Scale Protocol",
  "delimiter": "\\r\\n",
  "encoding": "ASCII",
  "fields": [
    {
      "name": "stability",
      "start": 0,
      "length": 2,
      "field_type": "lookup",
      "values": {
        "ST": "stable",
        "US": "unstable"
      }
    },
    {
      "name": "weight",
      "start": 3,
      "length": 8,
      "field_type": "numeric",
      "decimal_places": 2
    }
  ],
  "confidence_score": 100.0
}
```

## Troubleshooting

### Connection Issues
1. **Verify network connectivity** to ADAM device
2. **Check IP address and port** in configuration
3. **Ensure ADAM device is configured properly**:
   - ADAM-6051: Modbus TCP enabled
   - ADAM-4571: TCP Server mode, Raw Socket

### Discovery Issues
1. **No data received**: Check ADAM-4571 TCP configuration
2. **Low confidence scores**: Ensure scale readings are stable and consistent
3. **Garbled data**: Check serial port settings (baud rate, parity, data bits)

### Performance Issues
1. **High CPU usage**: Reduce polling frequency in configuration
2. **Memory growth**: Check for connection leaks, restart application periodically
3. **Network timeouts**: Increase timeout values in configuration

## Development Notes

### Architecture Patterns
- **Configuration Management**: JSON-based with defaults and validation
- **Error Handling**: Comprehensive logging with retry logic
- **Connection Management**: Thread-safe with automatic reconnection
- **Data Processing**: Pipeline-based with configurable processors

### Extending for New Devices
1. Create new `DeviceProvider` class implementing connection interface
2. Add protocol-specific `ProtocolProvider` for communication
3. Implement `DataProcessor` for device-specific data handling
4. Update configuration schema for new device parameters

### Migration to C# Platform
These Python tools serve as proof-of-concept implementations. Key patterns for C# migration:
- Interface-driven architecture (IDeviceProvider, IProtocolProvider)
- Configuration-based device registration
- Plugin architecture for extensibility
- Protocol template compatibility (JSON format)

## Support

For issues and questions:
1. Check logs in application directory
2. Verify ADAM device configuration
3. Test network connectivity
4. Review configuration files for syntax errors

## License

Internal development tools for industrial data acquisition platform.