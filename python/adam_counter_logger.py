#!/usr/bin/env python3
"""
Production-ready ADAM-6051 Counter Logger with InfluxDB
Implements best practices for Modbus TCP communication and error handling
"""

import json
import time
import logging
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, Tuple, Dict, Any
from dataclasses import dataclass

try:
    from pymodbus.client.sync import ModbusTcpClient
    from pymodbus.exceptions import ModbusException, ConnectionException
    from influxdb import InfluxDBClient
    from influxdb.exceptions import InfluxDBError
except ImportError as e:
    print(f"Missing required package: {e}")
    print("Install with: pip install pymodbus influxdb")
    exit(1)


@dataclass
class CounterReading:
    """Data class for counter readings"""
    channel: int
    count: int
    timestamp: datetime
    rate: Optional[float] = None


class ConfigManager:
    """Manages configuration loading and validation"""
    
    DEFAULT_CONFIG = {
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
            "host": "localhost",
            "port": 8086,
            "username": "",
            "password": "",
            "database": "adam_counters",
            "ssl": False,
            "verify_ssl": False,
            "timeout": 5,
            "retries": 3
        },
        "logging": {
            "poll_interval": 5.0,
            "log_level": "INFO",
            "log_file": "adam_logger.log",
            "max_log_size": "10MB",
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
    
    def __init__(self, config_file: str = "adam_config.json"):
        self.config_file = Path(config_file)
        self.config = self.load_config()
    
    def load_config(self) -> Dict[str, Any]:
        """Load configuration from file or create default"""
        if not self.config_file.exists():
            self.create_default_config()
            print(f"Created default config file: {self.config_file}")
            print("Please review and modify the configuration as needed.")
        
        try:
            with open(self.config_file, 'r') as f:
                config = json.load(f)
            
            # Merge with defaults to ensure all keys exist
            return self._merge_configs(self.DEFAULT_CONFIG, config)
            
        except (json.JSONDecodeError, FileNotFoundError) as e:
            print(f"Error loading config: {e}")
            print("Using default configuration")
            return self.DEFAULT_CONFIG.copy()
    
    def create_default_config(self):
        """Create default configuration file"""
        with open(self.config_file, 'w') as f:
            json.dump(self.DEFAULT_CONFIG, f, indent=4)
    
    def _merge_configs(self, default: dict, user: dict) -> dict:
        """Recursively merge user config with defaults"""
        result = default.copy()
        for key, value in user.items():
            if key in result and isinstance(result[key], dict) and isinstance(value, dict):
                result[key] = self._merge_configs(result[key], value)
            else:
                result[key] = value
        return result


class ModbusManager:
    """Manages Modbus TCP connection with retry logic and error handling"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['modbus']
        self.client = None
        self.connected = False
        self.connection_lock = threading.Lock()
        self.logger = logging.getLogger(__name__)
        
    def connect(self) -> bool:
        """Establish connection with retry logic"""
        with self.connection_lock:
            try:
                if self.client:
                    self.client.close()
                
                self.client = ModbusTcpClient(
                    host=self.config['host'],
                    port=self.config['port'],
                    timeout=self.config['timeout']
                )
                
                self.connected = self.client.connect()
                if self.connected:
                    self.logger.info(f"Connected to ADAM-6051 at {self.config['host']}:{self.config['port']}")
                else:
                    self.logger.error(f"Failed to connect to {self.config['host']}:{self.config['port']}")
                
                return self.connected
                
            except Exception as e:
                self.logger.error(f"Connection error: {e}")
                self.connected = False
                return False
    
    def read_counters(self, channels: list) -> Dict[int, Optional[int]]:
        """Read counter values with retry logic"""
        if not self.connected:
            if not self.connect():
                return {ch: None for ch in channels}
        
        for attempt in range(self.config['retries'] + 1):
            try:
                # Read holding registers for counter values
                # Each counter uses 2 registers (32-bit values)
                max_channel = max(channels)
                num_registers = (max_channel + 1) * 2
                
                result = self.client.read_holding_registers(
                    address=40001,  # Starting address for counters
                    count=num_registers,
                    unit=self.config['unit_id']
                )
                
                if result.isError():
                    raise ModbusException(f"Modbus error: {result}")
                
                # Parse 32-bit counter values
                counter_values = {}
                for channel in channels:
                    reg_index = channel * 2
                    if reg_index + 1 < len(result.registers):
                        # Combine low and high words
                        low_word = result.registers[reg_index]
                        high_word = result.registers[reg_index + 1]
                        counter_values[channel] = high_word * 65536 + low_word
                    else:
                        counter_values[channel] = None
                
                return counter_values
                
            except (ModbusException, ConnectionException) as e:
                self.logger.warning(f"Modbus read attempt {attempt + 1} failed: {e}")
                
                if attempt < self.config['retries']:
                    # Reconnect and retry
                    self.connected = False
                    time.sleep(self.config['retry_delay'])
                    self.connect()
                else:
                    self.logger.error(f"Failed to read counters after {self.config['retries']} retries")
                    return {ch: None for ch in channels}
            
            except Exception as e:
                self.logger.error(f"Unexpected error reading counters: {e}")
                return {ch: None for ch in channels}
        
        return {ch: None for ch in channels}
    
    def close(self):
        """Close connection safely"""
        with self.connection_lock:
            if self.client:
                self.client.close()
                self.connected = False
                self.logger.info("Modbus connection closed")


class InfluxDBManager:
    """Manages InfluxDB connection and data writing"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['influxdb']
        self.client = None
        self.logger = logging.getLogger(__name__)
        self._connect()
    
    def _connect(self):
        """Connect to InfluxDB"""
        try:
            self.client = InfluxDBClient(
                host=self.config['host'],
                port=self.config['port'],
                username=self.config['username'] or None,
                password=self.config['password'] or None,
                database=self.config['database'],
                ssl=self.config['ssl'],
                verify_ssl=self.config['verify_ssl'],
                timeout=self.config['timeout']
            )
            
            # Create database if it doesn't exist
            self.client.create_database(self.config['database'])
            self.logger.info(f"Connected to InfluxDB database: {self.config['database']}")
            
        except Exception as e:
            self.logger.error(f"Failed to connect to InfluxDB: {e}")
            self.client = None
    
    def write_counter_data(self, readings: list, device_config: Dict[str, Any]) -> bool:
        """Write counter readings to InfluxDB with retry logic"""
        if not self.client:
            self._connect()
            if not self.client:
                return False
        
        try:
            data_points = []
            for reading in readings:
                point = {
                    "measurement": "counter_data",
                    "tags": {
                        "device": device_config['name'],
                        "location": device_config['location'],
                        "channel": str(reading.channel)
                    },
                    "time": reading.timestamp.isoformat(),
                    "fields": {
                        "count": reading.count
                    }
                }
                
                # Add rate if available
                if reading.rate is not None:
                    point["fields"]["rate"] = reading.rate
                
                data_points.append(point)
            
            for attempt in range(self.config['retries'] + 1):
                try:
                    success = self.client.write_points(data_points)
                    if success:
                        self.logger.debug(f"Successfully wrote {len(data_points)} data points")
                        return True
                    else:
                        raise InfluxDBError("Write operation returned False")
                        
                except InfluxDBError as e:
                    self.logger.warning(f"InfluxDB write attempt {attempt + 1} failed: {e}")
                    if attempt < self.config['retries']:
                        time.sleep(1)
                        self._connect()  # Reconnect
                    else:
                        self.logger.error(f"Failed to write to InfluxDB after {self.config['retries']} retries")
                        return False
            
        except Exception as e:
            self.logger.error(f"Unexpected error writing to InfluxDB: {e}")
            return False
        
        return False


class RateCalculator:
    """Calculates count rates over time"""
    
    def __init__(self, window_size: int = 60):
        self.window_size = window_size
        self.history = {}  # {channel: [(timestamp, count), ...]}
    
    def add_reading(self, channel: int, count: int, timestamp: datetime) -> Optional[float]:
        """Add reading and calculate rate"""
        if channel not in self.history:
            self.history[channel] = []
        
        self.history[channel].append((timestamp, count))
        
        # Clean old entries
        cutoff_time = timestamp.timestamp() - self.window_size
        self.history[channel] = [
            (ts, cnt) for ts, cnt in self.history[channel]
            if ts.timestamp() > cutoff_time
        ]
        
        # Calculate rate if we have enough data
        if len(self.history[channel]) >= 2:
            oldest_time, oldest_count = self.history[channel][0]
            latest_time, latest_count = self.history[channel][-1]
            
            time_diff = (latest_time - oldest_time).total_seconds()
            if time_diff > 0:
                count_diff = latest_count - oldest_count
                return count_diff / time_diff
        
        return None


class ADAM6051Logger:
    """Main logger class that coordinates all components"""
    
    def __init__(self, config_file: str = "adam_config.json"):
        # Load configuration
        self.config_manager = ConfigManager(config_file)
        self.config = self.config_manager.config
        
        # Setup logging
        self._setup_logging()
        self.logger = logging.getLogger(__name__)
        
        # Initialize components
        self.modbus_manager = ModbusManager(self.config)
        self.influx_manager = InfluxDBManager(self.config)
        self.rate_calculator = RateCalculator(self.config['counters']['rate_window'])
        
        # Runtime state
        self.running = False
        self.last_readings = {}
        
        self.logger.info("ADAM-6051 Logger initialized")
    
    def _setup_logging(self):
        """Configure logging"""
        log_config = self.config['logging']
        
        # Create formatter
        formatter = logging.Formatter(
            '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
        )
        
        # Setup file handler with rotation
        from logging.handlers import RotatingFileHandler
        file_handler = RotatingFileHandler(
            log_config['log_file'],
            maxBytes=10*1024*1024,  # 10MB
            backupCount=log_config['backup_count']
        )
        file_handler.setFormatter(formatter)
        
        # Setup console handler
        console_handler = logging.StreamHandler()
        console_handler.setFormatter(formatter)
        
        # Configure root logger
        logging.basicConfig(
            level=getattr(logging, log_config['log_level']),
            handlers=[file_handler, console_handler]
        )
    
    def read_and_log_counters(self):
        """Read counter values and log to InfluxDB"""
        channels = self.config['counters']['channels']
        timestamp = datetime.now(timezone.utc)
        
        # Read counter values
        counter_values = self.modbus_manager.read_counters(channels)
        
        # Process readings
        readings = []
        for channel in channels:
            count = counter_values.get(channel)
            if count is not None:
                # Calculate rate if enabled
                rate = None
                if self.config['counters']['calculate_rate']:
                    rate = self.rate_calculator.add_reading(channel, count, timestamp)
                
                # Check for overflow
                if count > self.config['counters']['overflow_threshold']:
                    self.logger.warning(f"Channel {channel} approaching overflow: {count}")
                
                reading = CounterReading(
                    channel=channel,
                    count=count,
                    timestamp=timestamp,
                    rate=rate
                )
                readings.append(reading)
                
                self.logger.info(f"Channel {channel}: count={count}" + 
                               (f", rate={rate:.2f}/s" if rate else ""))
            else:
                self.logger.error(f"Failed to read channel {channel}")
        
        # Write to InfluxDB
        if readings:
            success = self.influx_manager.write_counter_data(readings, self.config['device'])
            if not success:
                self.logger.error("Failed to write data to InfluxDB")
        
        return len(readings) > 0
    
    def start_logging(self):
        """Start the logging loop"""
        self.running = True
        poll_interval = self.config['logging']['poll_interval']
        
        self.logger.info(f"Starting continuous logging every {poll_interval} seconds")
        self.logger.info(f"Monitoring channels: {self.config['counters']['channels']}")
        
        try:
            while self.running:
                start_time = time.time()
                
                success = self.read_and_log_counters()
                
                if not success:
                    self.logger.warning("No data logged this cycle")
                
                # Sleep for remaining time to maintain interval
                elapsed = time.time() - start_time
                sleep_time = max(0, poll_interval - elapsed)
                
                if sleep_time > 0:
                    time.sleep(sleep_time)
                else:
                    self.logger.warning(f"Polling took {elapsed:.2f}s, longer than interval {poll_interval}s")
        
        except KeyboardInterrupt:
            self.logger.info("Logging stopped by user")
        except Exception as e:
            self.logger.error(f"Unexpected error in logging loop: {e}")
        finally:
            self.stop_logging()
    
    def stop_logging(self):
        """Stop logging and cleanup"""
        self.running = False
        self.modbus_manager.close()
        self.logger.info("Logging stopped and connections closed")


def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description="ADAM-6051 Counter Logger")
    parser.add_argument(
        "--config", 
        default="adam_config.json",
        help="Configuration file path (default: adam_config.json)"
    )
    parser.add_argument(
        "--test", 
        action="store_true",
        help="Run a single test reading and exit"
    )
    
    args = parser.parse_args()
    
    logger = ADAM6051Logger(args.config)
    
    if args.test:
        print("Running test reading...")
        success = logger.read_and_log_counters()
        print(f"Test {'successful' if success else 'failed'}")
    else:
        logger.start_logging()


if __name__ == "__main__":
    main()
