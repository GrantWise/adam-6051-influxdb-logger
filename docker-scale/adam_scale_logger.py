#!/usr/bin/env python3
"""
Production-ready ADAM-4571 Scale Logger with Industrial Standards
Implements best practices for TCP socket communication, database management, and service architecture
Based on ADAM-6051 Counter Logger patterns with scale-specific adaptations
"""

import json
import time
import logging
import threading
import signal
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, Dict, Any, List
from dataclasses import dataclass
from abc import ABC, abstractmethod

try:
    import psycopg2
    import psycopg2.extras
    POSTGRES_AVAILABLE = True
except ImportError:
    POSTGRES_AVAILABLE = False
    print("PostgreSQL not available - SQLite will be used")

import sqlite3
import socket


@dataclass
class ScaleReading:
    """Data class for scale readings with industrial metadata"""
    weight: Optional[float]
    unit: Optional[str]
    stable: Optional[bool]
    timestamp: datetime
    raw_data: str
    device_id: str
    quality_flags: Dict[str, Any] = None


class ConfigManager:
    """Manages configuration loading and validation following industrial standards"""
    
    DEFAULT_CONFIG = {
        "adam4571": {
            "host": "192.168.1.101",
            "port": 4001,
            "timeout": 5,
            "reconnect_delay": 2.0,
            "buffer_size": 1024,
            "read_timeout": 1.0
        },
        "database": {
            "type": "sqlite",
            "postgresql": {
                "host": "localhost",
                "port": 5432,
                "database": "adam_industrial",
                "username": "adam_user",
                "password": "adam_password",
                "timeout": 5,
                "pool_size": 5,
                "max_overflow": 10
            },
            "sqlite": {
                "database_file": "adam_scale_data.db",
                "timeout": 30.0,
                "check_same_thread": False
            }
        },
        "monitoring": {
            "poll_interval": 10.0,
            "health_check_interval": 60.0,
            "max_consecutive_failures": 5,
            "data_retention_days": 90
        },
        "logging": {
            "log_level": "INFO",
            "log_file": "adam_scale_logger.log",
            "max_log_size_mb": 10,
            "backup_count": 5,
            "structured_logging": True
        },
        "device": {
            "name": "ADAM-4571-Scale",
            "location": "production_line_1",
            "description": "Industrial weight scale monitoring device"
        },
        "protocols": {
            "auto_discovery": True,
            "template_validation": True,
            "confidence_threshold": 85.0
        }
    }
    
    def __init__(self, config_file: str = None):
        # Load from environment variables first, then file
        self.config = self._load_from_environment()
        
        if config_file and Path(config_file).exists():
            self._merge_from_file(config_file)
        
        self._validate_config()
    
    def _load_from_environment(self) -> Dict[str, Any]:
        """Load configuration from environment variables (12-factor app)"""
        import os
        
        config = self.DEFAULT_CONFIG.copy()
        
        # ADAM device configuration
        if os.getenv('ADAM_HOST'):
            config['adam4571']['host'] = os.getenv('ADAM_HOST')
        if os.getenv('ADAM_PORT'):
            config['adam4571']['port'] = int(os.getenv('ADAM_PORT'))
        
        # Database configuration
        if os.getenv('DATABASE_TYPE'):
            config['database']['type'] = os.getenv('DATABASE_TYPE')
        if os.getenv('DATABASE_HOST'):
            config['database']['postgresql']['host'] = os.getenv('DATABASE_HOST')
        if os.getenv('DATABASE_PASSWORD'):
            config['database']['postgresql']['password'] = os.getenv('DATABASE_PASSWORD')
        
        # Application configuration
        if os.getenv('LOG_LEVEL'):
            config['logging']['log_level'] = os.getenv('LOG_LEVEL')
        if os.getenv('POLL_INTERVAL'):
            config['monitoring']['poll_interval'] = float(os.getenv('POLL_INTERVAL'))
        
        # Device identification
        if os.getenv('DEVICE_NAME'):
            config['device']['name'] = os.getenv('DEVICE_NAME')
        if os.getenv('DEVICE_LOCATION'):
            config['device']['location'] = os.getenv('DEVICE_LOCATION')
        
        return config
    
    def _merge_from_file(self, config_file: str):
        """Merge configuration from JSON file"""
        try:
            with open(config_file, 'r') as f:
                file_config = json.load(f)
            
            self.config = self._merge_configs(self.config, file_config)
            
        except (json.JSONDecodeError, FileNotFoundError) as e:
            logging.warning(f"Error loading config file {config_file}: {e}")
    
    def _merge_configs(self, default: dict, override: dict) -> Dict[str, Any]:
        """Recursively merge configuration dictionaries"""
        result = default.copy()
        for key, value in override.items():
            if key in result and isinstance(result[key], dict) and isinstance(value, dict):
                result[key] = self._merge_configs(result[key], value)
            else:
                result[key] = value
        return result
    
    def _validate_config(self):
        """Validate configuration for industrial requirements"""
        # Validate database type
        db_type = self.config['database']['type']
        if db_type not in ['postgresql', 'sqlite']:
            raise ValueError(f"Unsupported database type: {db_type}")
        
        # Validate PostgreSQL availability
        if db_type == 'postgresql' and not POSTGRES_AVAILABLE:
            logging.warning("PostgreSQL requested but not available, falling back to SQLite")
            self.config['database']['type'] = 'sqlite'
        
        # Validate polling interval
        poll_interval = self.config['monitoring']['poll_interval']
        if poll_interval < 1.0 or poll_interval > 3600.0:
            raise ValueError(f"Poll interval must be between 1.0 and 3600.0 seconds, got {poll_interval}")


class IDatabase(ABC):
    """Interface for database operations following SOLID principles"""
    
    @abstractmethod
    def connect(self) -> bool:
        """Establish database connection"""
        pass
    
    @abstractmethod
    def save_reading(self, reading: ScaleReading) -> bool:
        """Save scale reading to database"""
        pass
    
    @abstractmethod
    def get_recent_readings(self, limit: int = 100) -> List[ScaleReading]:
        """Get recent scale readings"""
        pass
    
    @abstractmethod
    def close(self):
        """Close database connection"""
        pass


class PostgreSQLDatabase(IDatabase):
    """PostgreSQL database implementation for production use"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['database']['postgresql']
        self.connection = None
        self.logger = logging.getLogger(__name__)
    
    def connect(self) -> bool:
        """Establish PostgreSQL connection with industrial reliability"""
        try:
            self.connection = psycopg2.connect(
                host=self.config['host'],
                port=self.config['port'],
                database=self.config['database'],
                user=self.config['username'],
                password=self.config['password'],
                connect_timeout=self.config['timeout']
            )
            self.connection.autocommit = True
            
            self._create_tables()
            self.logger.info(f"✅ Connected to PostgreSQL: {self.config['database']}")
            return True
            
        except Exception as e:
            self.logger.error(f"PostgreSQL connection failed: {e}")
            return False
    
    def _create_tables(self):
        """Create tables with proper schema for scale data"""
        cursor = self.connection.cursor()
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS scale_readings (
                id SERIAL PRIMARY KEY,
                device_id VARCHAR(50) NOT NULL,
                weight DECIMAL(10,3),
                unit VARCHAR(10),
                stable BOOLEAN,
                raw_data TEXT,
                quality_flags JSONB,
                timestamp TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create indexes for performance
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_scale_readings_timestamp ON scale_readings(timestamp)")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_scale_readings_device ON scale_readings(device_id)")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_scale_readings_created ON scale_readings(created_at)")
        
        cursor.close()
        self.logger.info("PostgreSQL tables created/verified")
    
    def save_reading(self, reading: ScaleReading) -> bool:
        """Save scale reading with proper error handling"""
        try:
            cursor = self.connection.cursor()
            cursor.execute("""
                INSERT INTO scale_readings (device_id, weight, unit, stable, raw_data, quality_flags, timestamp)
                VALUES (%s, %s, %s, %s, %s, %s, %s)
            """, (
                reading.device_id,
                reading.weight,
                reading.unit,
                reading.stable,
                reading.raw_data,
                json.dumps(reading.quality_flags) if reading.quality_flags else None,
                reading.timestamp.isoformat()
            ))
            cursor.close()
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to save reading to PostgreSQL: {e}")
            return False
    
    def get_recent_readings(self, limit: int = 100) -> List[ScaleReading]:
        """Get recent readings from PostgreSQL"""
        try:
            cursor = self.connection.cursor(cursor_factory=psycopg2.extras.RealDictCursor)
            cursor.execute("""
                SELECT * FROM scale_readings 
                ORDER BY timestamp DESC 
                LIMIT %s
            """, (limit,))
            
            rows = cursor.fetchall()
            cursor.close()
            
            readings = []
            for row in rows:
                reading = ScaleReading(
                    weight=float(row['weight']) if row['weight'] else None,
                    unit=row['unit'],
                    stable=row['stable'],
                    timestamp=row['timestamp'],
                    raw_data=row['raw_data'],
                    device_id=row['device_id'],
                    quality_flags=json.loads(row['quality_flags']) if row['quality_flags'] else None
                )
                readings.append(reading)
            
            return readings
            
        except Exception as e:
            self.logger.error(f"Failed to fetch readings from PostgreSQL: {e}")
            return []
    
    def close(self):
        """Close PostgreSQL connection safely"""
        if self.connection:
            self.connection.close()
            self.logger.info("PostgreSQL connection closed")


class SQLiteDatabase(IDatabase):
    """SQLite database implementation for lightweight deployments"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['database']['sqlite']
        self.connection = None
        self.logger = logging.getLogger(__name__)
    
    def connect(self) -> bool:
        """Establish SQLite connection"""
        try:
            self.connection = sqlite3.connect(
                self.config['database_file'],
                timeout=self.config['timeout'],
                check_same_thread=self.config['check_same_thread']
            )
            self.connection.row_factory = sqlite3.Row
            
            self._create_tables()
            self.logger.info(f"✅ Connected to SQLite: {self.config['database_file']}")
            return True
            
        except Exception as e:
            self.logger.error(f"SQLite connection failed: {e}")
            return False
    
    def _create_tables(self):
        """Create SQLite tables"""
        cursor = self.connection.cursor()
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS scale_readings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                weight REAL,
                unit TEXT,
                stable INTEGER,
                raw_data TEXT,
                quality_flags TEXT,
                timestamp TEXT NOT NULL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create indexes
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_scale_readings_timestamp ON scale_readings(timestamp)")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_scale_readings_device ON scale_readings(device_id)")
        
        self.connection.commit()
        cursor.close()
        self.logger.info("SQLite tables created/verified")
    
    def save_reading(self, reading: ScaleReading) -> bool:
        """Save reading to SQLite"""
        try:
            cursor = self.connection.cursor()
            cursor.execute("""
                INSERT INTO scale_readings (device_id, weight, unit, stable, raw_data, quality_flags, timestamp)
                VALUES (?, ?, ?, ?, ?, ?, ?)
            """, (
                reading.device_id,
                reading.weight,
                reading.unit,
                1 if reading.stable else 0,
                reading.raw_data,
                json.dumps(reading.quality_flags) if reading.quality_flags else None,
                reading.timestamp.isoformat()
            ))
            self.connection.commit()
            cursor.close()
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to save reading to SQLite: {e}")
            return False
    
    def get_recent_readings(self, limit: int = 100) -> List[ScaleReading]:
        """Get recent readings from SQLite"""
        try:
            cursor = self.connection.cursor()
            cursor.execute("""
                SELECT * FROM scale_readings 
                ORDER BY timestamp DESC 
                LIMIT ?
            """, (limit,))
            
            rows = cursor.fetchall()
            cursor.close()
            
            readings = []
            for row in rows:
                reading = ScaleReading(
                    weight=float(row['weight']) if row['weight'] else None,
                    unit=row['unit'],
                    stable=bool(row['stable']),
                    timestamp=datetime.fromisoformat(row['timestamp']),
                    raw_data=row['raw_data'],
                    device_id=row['device_id'],
                    quality_flags=json.loads(row['quality_flags']) if row['quality_flags'] else None
                )
                readings.append(reading)
            
            return readings
            
        except Exception as e:
            self.logger.error(f"Failed to fetch readings from SQLite: {e}")
            return []
    
    def close(self):
        """Close SQLite connection safely"""
        if self.connection:
            self.connection.close()
            self.logger.info("SQLite connection closed")


class DatabaseFactory:
    """Factory for creating database instances following SOLID principles"""
    
    @staticmethod
    def create_database(config: Dict[str, Any]) -> IDatabase:
        """Create appropriate database instance based on configuration"""
        db_type = config['database']['type']
        
        if db_type == 'postgresql':
            return PostgreSQLDatabase(config)
        elif db_type == 'sqlite':
            return SQLiteDatabase(config)
        else:
            raise ValueError(f"Unsupported database type: {db_type}")


class Adam4571Manager:
    """Manages TCP connection to ADAM-4571 following industrial communication patterns"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['adam4571']
        self.socket = None
        self.connected = False
        self.connection_lock = threading.Lock()
        self.logger = logging.getLogger(__name__)
        self._running = False
        self._data_buffer = []
        self._buffer_lock = threading.Lock()
        self._consecutive_failures = 0
        self._last_successful_read = None
    
    def connect(self) -> bool:
        """Establish TCP connection with comprehensive error handling"""
        with self.connection_lock:
            try:
                if self.socket:
                    self.socket.close()
                
                self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.socket.settimeout(self.config['timeout'])
                
                self.logger.info(f"Connecting to ADAM-4571 at {self.config['host']}:{self.config['port']}")
                self.socket.connect((self.config['host'], self.config['port']))
                
                self.connected = True
                self._consecutive_failures = 0
                self._last_successful_read = datetime.now(timezone.utc)
                
                self.logger.info(f"✅ Successfully connected to ADAM-4571 at {self.config['host']}:{self.config['port']}")
                return True
                
            except socket.error as e:
                self.logger.error(f"✗ Failed to connect to ADAM-4571: {e}")
                self.logger.error(f"  Check: 1) Device IP address, 2) Network connectivity, 3) TCP port {self.config['port']} accessibility")
                self.connected = False
                self._consecutive_failures += 1
                return False
            except Exception as e:
                self.logger.error(f"Unexpected connection error: {e}")
                self.connected = False
                self._consecutive_failures += 1
                return False
    
    def start_monitoring(self):
        """Start continuous data monitoring"""
        if not self.connected:
            if not self.connect():
                return False
        
        self._running = True
        self._monitor_thread = threading.Thread(target=self._monitor_loop, daemon=True)
        self._monitor_thread.start()
        self.logger.info("Started data monitoring thread")
        return True
    
    def stop_monitoring(self):
        """Stop data monitoring"""
        self._running = False
        if hasattr(self, '_monitor_thread'):
            self._monitor_thread.join(timeout=2)
        self.logger.info("Stopped data monitoring")
    
    def _monitor_loop(self):
        """Background monitoring loop with error recovery"""
        partial_data = ""
        
        while self._running:
            try:
                if not self.connected:
                    if not self.connect():
                        time.sleep(self.config['reconnect_delay'])
                        continue
                
                # Receive data with timeout
                self.socket.settimeout(self.config['read_timeout'])
                data = self.socket.recv(self.config['buffer_size'])
                
                if not data:
                    self.logger.warning("No data received, connection may be closed")
                    self.connected = False
                    self._consecutive_failures += 1
                    continue
                
                # Process received data
                self._process_received_data(data, partial_data)
                self._consecutive_failures = 0
                self._last_successful_read = datetime.now(timezone.utc)
                
            except socket.timeout:
                # Expected timeout, continue monitoring
                continue
            except socket.error as e:
                self.logger.error(f"Socket error in monitoring loop: {e}")
                self.connected = False
                self._consecutive_failures += 1
                time.sleep(self.config['reconnect_delay'])
            except Exception as e:
                self.logger.error(f"Unexpected error in monitoring loop: {e}")
                self._consecutive_failures += 1
                time.sleep(self.config['reconnect_delay'])
    
    def _process_received_data(self, data: bytes, partial_data: str):
        """Process received data with proper encoding handling"""
        try:
            decoded_data = data.decode('ascii', errors='replace')
            partial_data += decoded_data
            
            # Split on common delimiters
            import re
            lines = re.split(r'[\r\n]+', partial_data)
            
            # Keep last partial line
            partial_data = lines[-1] if not partial_data.endswith(('\r', '\n')) else ""
            
            # Process complete lines
            for line in lines[:-1]:
                if line.strip():
                    with self._buffer_lock:
                        self._data_buffer.append({
                            'data': line.strip(),
                            'timestamp': datetime.now(timezone.utc)
                        })
                        
                        # Limit buffer size
                        if len(self._data_buffer) > 1000:
                            self._data_buffer = self._data_buffer[-500:]
        
        except UnicodeDecodeError as e:
            self.logger.warning(f"Unicode decode error: {e}")
            # Try alternative encoding
            try:
                decoded_data = data.decode('latin-1')
                self.logger.debug(f"Fallback decode successful: {repr(decoded_data)}")
            except Exception:
                self.logger.error(f"Failed to decode data: {repr(data)}")
    
    def get_recent_data(self, max_age_seconds: float = 10.0) -> List[Dict]:
        """Get recent captured data"""
        cutoff_time = datetime.now(timezone.utc).timestamp() - max_age_seconds
        
        with self._buffer_lock:
            recent_data = [
                entry for entry in self._data_buffer
                if entry['timestamp'].timestamp() > cutoff_time
            ]
            return recent_data.copy()
    
    def get_health_status(self) -> Dict[str, Any]:
        """Get connection health status"""
        now = datetime.now(timezone.utc)
        
        status = {
            'connected': self.connected,
            'consecutive_failures': self._consecutive_failures,
            'last_successful_read': self._last_successful_read.isoformat() if self._last_successful_read else None,
            'buffer_size': len(self._data_buffer),
            'uptime_seconds': None
        }
        
        if self._last_successful_read:
            status['seconds_since_last_read'] = (now - self._last_successful_read).total_seconds()
        
        return status
    
    def close(self):
        """Close connection safely"""
        self.stop_monitoring()
        
        with self.connection_lock:
            if self.socket:
                self.socket.close()
                self.connected = False
                self.logger.info("TCP connection closed")


class ScaleDataProcessor:
    """Processes raw scale data following industrial data processing patterns"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
    
    def process_raw_data(self, raw_data: str, device_id: str) -> Optional[ScaleReading]:
        """Process raw scale data into structured reading"""
        try:
            # Basic data processing - in production this would use protocol templates
            # For now, implement simple parsing logic
            
            timestamp = datetime.now(timezone.utc)
            
            # Simple pattern matching for common scale formats
            import re
            
            # Look for weight patterns
            weight_match = re.search(r'[\+\-]?\d+\.?\d*', raw_data)
            weight = float(weight_match.group()) if weight_match else None
            
            # Look for unit patterns
            unit_match = re.search(r'(kg|lb|g|oz)', raw_data.lower())
            unit = unit_match.group() if unit_match else 'kg'
            
            # Check for stability indicators
            stable = 'ST' in raw_data.upper() or 'STABLE' in raw_data.upper()
            
            # Quality assessment
            quality_flags = {
                'raw_length': len(raw_data),
                'has_numeric': bool(weight_match),
                'has_unit': bool(unit_match),
                'processing_timestamp': timestamp.isoformat()
            }
            
            reading = ScaleReading(
                weight=weight,
                unit=unit,
                stable=stable,
                timestamp=timestamp,
                raw_data=raw_data,
                device_id=device_id,
                quality_flags=quality_flags
            )
            
            self.logger.debug(f"Processed reading: weight={weight} {unit}, stable={stable}")
            return reading
            
        except Exception as e:
            self.logger.error(f"Error processing raw data '{raw_data}': {e}")
            return None


class ScaleLoggerService:
    """Main service class implementing industrial software standards"""
    
    def __init__(self, config_file: str = None):
        # Load configuration
        self.config_manager = ConfigManager(config_file)
        self.config = self.config_manager.config
        
        # Setup logging
        self._setup_logging()
        self.logger = logging.getLogger(__name__)
        
        # Initialize components
        self.database = DatabaseFactory.create_database(self.config)
        self.adam_manager = Adam4571Manager(self.config)
        self.data_processor = ScaleDataProcessor(self.config)
        
        # Service state
        self.running = False
        self.health_status = {}
        
        # Signal handling for graceful shutdown
        signal.signal(signal.SIGTERM, self._signal_handler)
        signal.signal(signal.SIGINT, self._signal_handler)
        
        self.logger.info("ADAM Scale Logger Service initialized")
    
    def _setup_logging(self):
        """Configure industrial-grade logging"""
        log_config = self.config['logging']
        
        formatter = logging.Formatter(
            '%(asctime)s.%(msecs)03d - %(name)s - %(levelname)s - [%(funcName)s:%(lineno)d] - %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'
        )
        
        # File handler with rotation
        from logging.handlers import RotatingFileHandler
        max_bytes = log_config['max_log_size_mb'] * 1024 * 1024
        file_handler = RotatingFileHandler(
            log_config['log_file'],
            maxBytes=max_bytes,
            backupCount=log_config['backup_count']
        )
        file_handler.setFormatter(formatter)
        
        # Console handler
        console_handler = logging.StreamHandler()
        console_handler.setFormatter(formatter)
        
        # Configure logger
        logging.basicConfig(
            level=getattr(logging, log_config['log_level']),
            handlers=[file_handler, console_handler]
        )
    
    def _signal_handler(self, signum, frame):
        """Handle shutdown signals gracefully"""
        self.logger.info(f"Received signal {signum}, initiating graceful shutdown...")
        self.stop()
    
    def start_monitoring(self):
        """Start the monitoring service"""
        try:
            self.logger.info("🚀 Starting ADAM Scale Logger Service")
            
            # Initialize database connection
            if not self.database.connect():
                self.logger.error("Failed to connect to database")
                return False
            
            # Start ADAM device monitoring
            if not self.adam_manager.start_monitoring():
                self.logger.error("Failed to start ADAM device monitoring")
                return False
            
            # Start main service loop
            self.running = True
            self._service_loop()
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to start monitoring service: {e}")
            return False
    
    def _service_loop(self):
        """Main service monitoring loop"""
        self.logger.info("📊 Starting continuous scale monitoring")
        poll_interval = self.config['monitoring']['poll_interval']
        
        try:
            while self.running:
                start_time = time.time()
                
                # Process recent scale data
                self._process_recent_data()
                
                # Update health status
                self._update_health_status()
                
                # Sleep for remaining time
                elapsed = time.time() - start_time
                sleep_time = max(0, poll_interval - elapsed)
                
                if sleep_time > 0:
                    time.sleep(sleep_time)
                else:
                    self.logger.warning(f"Processing took {elapsed:.2f}s, longer than interval {poll_interval}s")
        
        except Exception as e:
            self.logger.error(f"Unexpected error in service loop: {e}")
        finally:
            self.stop()
    
    def _process_recent_data(self):
        """Process recent data from ADAM device"""
        try:
            recent_data = self.adam_manager.get_recent_data(
                self.config['monitoring']['poll_interval'] + 1
            )
            
            processed_count = 0
            device_id = self.config['device']['name']
            
            for data_entry in recent_data:
                reading = self.data_processor.process_raw_data(
                    data_entry['data'], 
                    device_id
                )
                
                if reading:
                    if self.database.save_reading(reading):
                        processed_count += 1
                        if reading.weight is not None:
                            self.logger.info(f"📏 Weight: {reading.weight} {reading.unit} ({'stable' if reading.stable else 'unstable'})")
            
            if processed_count > 0:
                self.logger.debug(f"Processed {processed_count} readings")
            
        except Exception as e:
            self.logger.error(f"Error processing recent data: {e}")
    
    def _update_health_status(self):
        """Update service health status"""
        self.health_status = {
            'service_running': self.running,
            'adam_device': self.adam_manager.get_health_status(),
            'database_connected': self.database.connection is not None,
            'last_update': datetime.now(timezone.utc).isoformat()
        }
    
    def get_health_status(self) -> Dict[str, Any]:
        """Get current health status"""
        return self.health_status.copy()
    
    def stop(self):
        """Stop the monitoring service gracefully"""
        self.logger.info("🛑 Stopping ADAM Scale Logger Service")
        
        self.running = False
        
        # Stop components
        self.adam_manager.close()
        self.database.close()
        
        self.logger.info("✅ Service stopped gracefully")


def main():
    """Main entry point for ADAM-4571 Scale Logger Service"""
    import argparse
    
    parser = argparse.ArgumentParser(description="ADAM-4571 Scale Logger Service")
    parser.add_argument(
        "--config",
        default=None,
        help="Configuration file path"
    )
    parser.add_argument(
        "--monitor",
        action="store_true",
        help="Start monitoring service"
    )
    parser.add_argument(
        "--test",
        action="store_true", 
        help="Test connectivity only"
    )
    
    args = parser.parse_args()
    
    service = ScaleLoggerService(args.config)
    
    if args.test:
        # Test mode - verify connectivity
        print("🧪 Testing ADAM device connectivity...")
        if service.adam_manager.connect():
            print("✅ Connection successful")
            service.adam_manager.close()
            sys.exit(0)
        else:
            print("❌ Connection failed")
            sys.exit(1)
    elif args.monitor:
        # Monitoring mode - start service
        success = service.start_monitoring()
        sys.exit(0 if success else 1)
    else:
        # Default - start monitoring
        success = service.start_monitoring()
        sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()