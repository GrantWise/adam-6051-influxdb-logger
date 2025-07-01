#!/usr/bin/env python3
"""
ADAM-4571 Weight Scale Protocol Discovery Application
Lightweight Python tool for automatic scale protocol detection and data acquisition
Implements best practices for TCP socket communication and error handling
"""

import json
import time
import socket
import logging
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, Dict, Any, List, Tuple
from dataclasses import dataclass, asdict
import re
import statistics

try:
    # Optional dependencies for advanced features
    import psycopg2  # PostgreSQL connectivity
    import psycopg2.extras
except ImportError as e:
    print(f"Optional package not available: {e}")
    print("For PostgreSQL integration, install with: pip install psycopg2-binary")

# SQLite is built-in to Python
import sqlite3


@dataclass
class WeightReading:
    """Data class for weight readings"""
    weight: Optional[float]
    unit: Optional[str]
    stable: Optional[bool]
    timestamp: datetime
    raw_data: str


@dataclass
class DiscoveryStep:
    """Data class for discovery process steps"""
    step_number: int
    action: str  # 'baseline', 'add_weight', 'remove_weight'
    expected_weight: Optional[float]
    captured_data: List[str]
    timestamp: datetime


@dataclass
class ProtocolField:
    """Data class for protocol field definition"""
    name: str
    start: int
    length: int
    field_type: str  # 'numeric', 'lookup', 'text'
    decimal_places: Optional[int] = None
    values: Optional[Dict[str, str]] = None  # For lookup fields


@dataclass
class ProtocolTemplate:
    """Data class for discovered protocol template"""
    template_id: str
    name: str
    description: str
    delimiter: str
    encoding: str
    fields: List[ProtocolField]
    confidence_score: float
    discovery_date: datetime


class ConfigManager:
    """Manages configuration loading and validation for scale discovery"""
    
    DEFAULT_CONFIG = {
        "adam4571": {
            "host": "192.168.1.101",
            "port": 4001,
            "timeout": 5,
            "reconnect_delay": 2.0,
            "buffer_size": 1024
        },
        "discovery": {
            "confidence_threshold": 85.0,
            "max_iterations": 10,
            "frame_timeout": 2.0,
            "min_samples": 3,
            "stability_window": 5
        },
        "storage": {
            "templates_dir": "protocol_templates",
            "data_log": "weight_readings.csv",
            "backup_count": 10,
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
        },
        "logging": {
            "log_level": "INFO",
            "log_file": "scale_discovery.log",
            "max_log_size_mb": 10,
            "backup_count": 5
        },
        "device": {
            "name": "ADAM-4571-Scale",
            "location": "test_station",
            "description": "Weight scale protocol discovery device"
        }
    }
    
    def __init__(self, config_file: str = "adam_scale_config.json"):
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
    
    def _merge_configs(self, default: dict, user: dict) -> Dict[str, Any]:
        """Recursively merge user config with defaults"""
        result = default.copy()
        for key, value in user.items():
            if key in result and isinstance(result[key], dict) and isinstance(value, dict):
                result[key] = self._merge_configs(result[key], value)
            else:
                result[key] = value
        return result


class Adam4571Manager:
    """Manages TCP connection to ADAM-4571 with retry logic and error handling"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['adam4571']
        self.socket = None
        self.connected = False
        self.connection_lock = threading.Lock()
        self.logger = logging.getLogger(__name__)
        self._running = False
        self._data_buffer = []
        self._buffer_lock = threading.Lock()
        
    def connect(self) -> bool:
        """Establish TCP connection to ADAM-4571 device with comprehensive logging
        
        Returns:
            bool: True if connection successful, False otherwise
            
        Note:
            Logs detailed connection attempts for troubleshooting device setup issues
        """
        with self.connection_lock:
            try:
                if self.socket:
                    self.socket.close()
                
                self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.socket.settimeout(self.config['timeout'])
                
                self.logger.info(f"Connecting to ADAM-4571 at {self.config['host']}:{self.config['port']}")
                self.socket.connect((self.config['host'], self.config['port']))
                
                self.connected = True
                self.logger.info(f"✓ Successfully connected to ADAM-4571 at {self.config['host']}:{self.config['port']}")
                # Test basic communication to verify device responsiveness
                self.logger.debug(f"Connection parameters - Timeout: {self.config['timeout']}s, Buffer size: {self.config['buffer_size']}")
                return True
                
            except socket.error as e:
                self.logger.error(f"✗ Failed to connect to ADAM-4571: {e}")
                self.logger.error(f"  Check: 1) Device IP address, 2) Network connectivity, 3) TCP port {self.config['port']} accessibility")
                self.connected = False
                return False
            except Exception as e:
                self.logger.error(f"Unexpected connection error: {e}")
                self.connected = False
                return False
    
    def start_data_capture(self):
        """Start capturing data in background thread"""
        if not self.connected:
            if not self.connect():
                return False
        
        self._running = True
        self._capture_thread = threading.Thread(target=self._capture_loop, daemon=True)
        self._capture_thread.start()
        self.logger.info("Started data capture thread")
        return True
    
    def stop_data_capture(self):
        """Stop data capture"""
        self._running = False
        if hasattr(self, '_capture_thread'):
            self._capture_thread.join(timeout=2)
        self.logger.info("Stopped data capture")
    
    def _capture_loop(self):
        """Background thread for continuous data capture"""
        partial_data = ""
        
        while self._running and self.connected:
            try:
                # Receive data with timeout
                self.socket.settimeout(1.0)  # Non-blocking with timeout
                data = self.socket.recv(self.config['buffer_size'])
                
                if not data:
                    self.logger.warning("No data received, connection may be closed")
                    self.connected = False
                    break
                
                # Decode and process data
                try:
                    decoded_data = data.decode('ascii', errors='replace')
                    partial_data += decoded_data
                    
                    # Split on common delimiters
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
                
                except UnicodeDecodeError as e:
                    self.logger.warning(f"Unicode decode error: {e}")
                    # Try latin-1 as fallback
                    try:
                        decoded_data = data.decode('latin-1')
                        self.logger.debug(f"Fallback decode successful: {repr(decoded_data)}")
                    except Exception:
                        self.logger.error(f"Failed to decode data: {repr(data)}")
                
            except socket.timeout:
                # Expected timeout, continue loop
                continue
            except socket.error as e:
                self.logger.error(f"Socket error in capture loop: {e}")
                self.connected = False
                break
            except Exception as e:
                self.logger.error(f"Unexpected error in capture loop: {e}")
                break
    
    def get_recent_data(self, max_age_seconds: float = 10.0) -> List[Dict]:
        """Get recent captured data with timestamp filtering
        
        Args:
            max_age_seconds: Maximum age of data to return in seconds
            
        Returns:
            List of recent data entries with format:
            [{'data': str, 'timestamp': datetime}, ...]
            
        Note:
            Filters data buffer based on timestamp to return only recent entries
            Used for protocol discovery analysis and real-time monitoring
        """
        cutoff_time = datetime.now(timezone.utc).timestamp() - max_age_seconds
        
        with self._buffer_lock:
            recent_data = [
                entry for entry in self._data_buffer
                if entry['timestamp'].timestamp() > cutoff_time
            ]
            return recent_data.copy()
    
    def clear_buffer(self):
        """Clear the data buffer"""
        with self._buffer_lock:
            self._data_buffer.clear()
    
    def close(self):
        """Close connection safely"""
        self.stop_data_capture()
        
        with self.connection_lock:
            if self.socket:
                self.socket.close()
                self.connected = False
                self.logger.info("TCP connection closed")


class DatabaseManager:
    """Manages database connections for weight data storage with support for PostgreSQL and SQLite"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config['storage']['database']
        self.db_type = self.config['type']
        self.connection = None
        self.logger = logging.getLogger(__name__)
        
    def connect(self) -> bool:
        """Establish database connection based on configured type
        
        Returns:
            bool: True if connection successful, False otherwise
            
        Note:
            Supports both PostgreSQL for production and SQLite for lightweight deployments
        """
        try:
            if self.db_type == "postgresql":
                return self._connect_postgresql()
            elif self.db_type == "sqlite":
                return self._connect_sqlite()
            else:
                self.logger.error(f"Unsupported database type: {self.db_type}")
                return False
        except Exception as e:
            self.logger.error(f"Database connection failed: {e}")
            return False
    
    def _connect_postgresql(self) -> bool:
        """Connect to PostgreSQL database"""
        try:
            pg_config = self.config['postgresql']
            self.connection = psycopg2.connect(
                host=pg_config['host'],
                port=pg_config['port'],
                database=pg_config['database'],
                user=pg_config['username'],
                password=pg_config['password'],
                connect_timeout=pg_config['timeout']
            )
            self.connection.autocommit = True
            self.logger.info(f"✓ Connected to PostgreSQL database: {pg_config['database']}")
            self._create_tables_postgresql()
            return True
        except Exception as e:
            self.logger.error(f"PostgreSQL connection failed: {e}")
            return False
    
    def _connect_sqlite(self) -> bool:
        """Connect to SQLite database"""
        try:
            sqlite_config = self.config['sqlite']
            self.connection = sqlite3.connect(
                sqlite_config['database_file'],
                timeout=30.0,
                check_same_thread=False
            )
            self.connection.row_factory = sqlite3.Row
            self.logger.info(f"✓ Connected to SQLite database: {sqlite_config['database_file']}")
            self._create_tables_sqlite()
            return True
        except Exception as e:
            self.logger.error(f"SQLite connection failed: {e}")
            return False
    
    def _create_tables_postgresql(self):
        """Create PostgreSQL tables if they don't exist"""
        cursor = self.connection.cursor()
        
        # Weight readings table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS weight_readings (
                id SERIAL PRIMARY KEY,
                device_id VARCHAR(50) NOT NULL,
                weight DECIMAL(10,3),
                unit VARCHAR(10),
                stable BOOLEAN,
                raw_data TEXT,
                timestamp TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Protocol templates table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS protocol_templates (
                id SERIAL PRIMARY KEY,
                template_id VARCHAR(50) UNIQUE NOT NULL,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                template_data JSONB NOT NULL,
                confidence_score DECIMAL(5,2),
                discovery_date TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create indexes
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_weight_readings_timestamp ON weight_readings(timestamp)")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_weight_readings_device ON weight_readings(device_id)")
        
        cursor.close()
        self.logger.info("PostgreSQL tables created/verified")
    
    def _create_tables_sqlite(self):
        """Create SQLite tables if they don't exist"""
        cursor = self.connection.cursor()
        
        # Weight readings table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS weight_readings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                weight REAL,
                unit TEXT,
                stable INTEGER,
                raw_data TEXT,
                timestamp TEXT NOT NULL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Protocol templates table
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS protocol_templates (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                template_data TEXT NOT NULL,
                confidence_score REAL,
                discovery_date TEXT NOT NULL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create indexes
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_weight_readings_timestamp ON weight_readings(timestamp)")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_weight_readings_device ON weight_readings(device_id)")
        
        self.connection.commit()
        cursor.close()
        self.logger.info("SQLite tables created/verified")
    
    def save_weight_reading(self, reading: WeightReading, device_id: str) -> bool:
        """Save weight reading to database
        
        Args:
            reading: WeightReading object to save
            device_id: Device identifier
            
        Returns:
            bool: True if successful, False otherwise
        """
        try:
            if not self.connection:
                if not self.connect():
                    return False
            
            if self.db_type == "postgresql":
                return self._save_weight_postgresql(reading, device_id)
            elif self.db_type == "sqlite":
                return self._save_weight_sqlite(reading, device_id)
            
        except Exception as e:
            self.logger.error(f"Failed to save weight reading: {e}")
            return False
    
    def _save_weight_postgresql(self, reading: WeightReading, device_id: str) -> bool:
        """Save weight reading to PostgreSQL"""
        cursor = self.connection.cursor()
        cursor.execute("""
            INSERT INTO weight_readings (device_id, weight, unit, stable, raw_data, timestamp)
            VALUES (%s, %s, %s, %s, %s, %s)
        """, (
            device_id,
            reading.weight,
            reading.unit,
            reading.stable,
            reading.raw_data,
            reading.timestamp.isoformat()
        ))
        cursor.close()
        return True
    
    def _save_weight_sqlite(self, reading: WeightReading, device_id: str) -> bool:
        """Save weight reading to SQLite"""
        cursor = self.connection.cursor()
        cursor.execute("""
            INSERT INTO weight_readings (device_id, weight, unit, stable, raw_data, timestamp)
            VALUES (?, ?, ?, ?, ?, ?)
        """, (
            device_id,
            reading.weight,
            reading.unit,
            1 if reading.stable else 0,
            reading.raw_data,
            reading.timestamp.isoformat()
        ))
        self.connection.commit()
        cursor.close()
        return True
    
    def save_protocol_template(self, template: ProtocolTemplate) -> bool:
        """Save protocol template to database
        
        Args:
            template: ProtocolTemplate object to save
            
        Returns:
            bool: True if successful, False otherwise
        """
        try:
            if not self.connection:
                if not self.connect():
                    return False
            
            template_data = asdict(template)
            template_data['discovery_date'] = template.discovery_date.isoformat()
            
            if self.db_type == "postgresql":
                return self._save_template_postgresql(template, template_data)
            elif self.db_type == "sqlite":
                return self._save_template_sqlite(template, template_data)
            
        except Exception as e:
            self.logger.error(f"Failed to save protocol template: {e}")
            return False
    
    def _save_template_postgresql(self, template: ProtocolTemplate, template_data: dict) -> bool:
        """Save protocol template to PostgreSQL"""
        cursor = self.connection.cursor()
        cursor.execute("""
            INSERT INTO protocol_templates (template_id, name, description, template_data, confidence_score, discovery_date)
            VALUES (%s, %s, %s, %s, %s, %s)
            ON CONFLICT (template_id) DO UPDATE SET
                name = EXCLUDED.name,
                description = EXCLUDED.description,
                template_data = EXCLUDED.template_data,
                confidence_score = EXCLUDED.confidence_score,
                discovery_date = EXCLUDED.discovery_date
        """, (
            template.template_id,
            template.name,
            template.description,
            json.dumps(template_data),
            template.confidence_score,
            template.discovery_date.isoformat()
        ))
        cursor.close()
        return True
    
    def _save_template_sqlite(self, template: ProtocolTemplate, template_data: dict) -> bool:
        """Save protocol template to SQLite"""
        cursor = self.connection.cursor()
        cursor.execute("""
            INSERT OR REPLACE INTO protocol_templates 
            (template_id, name, description, template_data, confidence_score, discovery_date)
            VALUES (?, ?, ?, ?, ?, ?)
        """, (
            template.template_id,
            template.name,
            template.description,
            json.dumps(template_data),
            template.confidence_score,
            template.discovery_date.isoformat()
        ))
        self.connection.commit()
        cursor.close()
        return True
    
    def close(self):
        """Close database connection safely"""
        if self.connection:
            self.connection.close()
            self.connection = None
            self.logger.info(f"{self.db_type.title()} connection closed")


class TemplateManager:
    """Manages protocol templates for known scale manufacturers"""
    
    def __init__(self, templates_dir: str = "protocol_templates"):
        self.templates_dir = Path(templates_dir)
        self.logger = logging.getLogger(__name__)
        self.templates = {}
        self.load_templates()
    
    def load_templates(self):
        """Load all available protocol templates"""
        if not self.templates_dir.exists():
            self.logger.warning(f"Templates directory {self.templates_dir} does not exist")
            return
        
        template_files = list(self.templates_dir.glob("*.json"))
        
        for template_file in template_files:
            try:
                with open(template_file, 'r') as f:
                    template_data = json.load(f)
                
                template_id = template_data.get('template_id')
                if template_id:
                    self.templates[template_id] = template_data
                    self.logger.debug(f"Loaded template: {template_id}")
                
            except Exception as e:
                self.logger.warning(f"Failed to load template {template_file}: {e}")
        
        self.logger.info(f"Loaded {len(self.templates)} protocol templates")
    
    def get_all_templates(self) -> Dict[str, Dict]:
        """Get all loaded templates"""
        return self.templates.copy()
    
    def get_template(self, template_id: str) -> Optional[Dict]:
        """Get specific template by ID"""
        return self.templates.get(template_id)
    
    def get_manufacturer_templates(self) -> Dict[str, List[str]]:
        """Get templates organized by manufacturer"""
        manufacturers = {}
        
        for template_id, template_data in self.templates.items():
            manufacturer_info = template_data.get('manufacturer_info', {})
            brand = manufacturer_info.get('brand', 'Unknown')
            
            if brand not in manufacturers:
                manufacturers[brand] = []
            
            manufacturers[brand].append(template_id)
        
        return manufacturers


class TemplateValidator:
    """Validates protocol templates against live scale data"""
    
    def __init__(self, connection_manager: Adam4571Manager):
        self.connection_manager = connection_manager
        self.logger = logging.getLogger(__name__)
    
    def test_template(self, template_data: Dict, test_duration: float = 10.0) -> Dict[str, Any]:
        """Test a protocol template against live data
        
        Args:
            template_data: Protocol template to test
            test_duration: How long to collect test data (seconds)
            
        Returns:
            Dictionary with test results including confidence score
        """
        self.logger.info(f"Testing template: {template_data.get('name', 'Unknown')}")
        
        # Start data capture
        if not self.connection_manager.connected:
            if not self.connection_manager.connect():
                return {"success": False, "confidence": 0, "error": "Connection failed"}
        
        if not self.connection_manager.start_data_capture():
            return {"success": False, "confidence": 0, "error": "Data capture failed"}
        
        # Clear buffer and collect fresh data
        self.connection_manager.clear_buffer()
        time.sleep(test_duration)
        
        # Get captured data
        test_data = self.connection_manager.get_recent_data(test_duration + 1)
        
        if not test_data:
            return {"success": False, "confidence": 0, "error": "No data received"}
        
        # Analyze data against template
        results = self._analyze_template_match(template_data, test_data)
        
        self.logger.info(f"Template test results: {results['confidence']:.1f}% confidence")
        return results
    
    def _analyze_template_match(self, template_data: Dict, test_data: List[Dict]) -> Dict[str, Any]:
        """Analyze how well template matches the actual data"""
        
        template_fields = template_data.get('fields', [])
        delimiter = template_data.get('delimiter', '\\r\\n').replace('\\r', '\r').replace('\\n', '\n')
        
        successful_parses = 0
        total_attempts = 0
        parsing_errors = []
        
        for data_entry in test_data:
            raw_data = data_entry['data']
            total_attempts += 1
            
            try:
                # Try to parse this data with the template
                parsed_result = self._parse_with_template(raw_data, template_fields)
                
                if parsed_result['success']:
                    successful_parses += 1
                else:
                    parsing_errors.append(parsed_result['error'])
                    
            except Exception as e:
                parsing_errors.append(str(e))
        
        # Calculate confidence score
        if total_attempts == 0:
            confidence = 0
        else:
            parse_success_rate = (successful_parses / total_attempts) * 100
            
            # Additional factors for confidence
            data_consistency = self._check_data_consistency(test_data, template_fields)
            format_match = self._check_format_characteristics(test_data, template_data)
            
            # Weighted confidence score
            confidence = (parse_success_rate * 0.6 + data_consistency * 0.2 + format_match * 0.2)
        
        return {
            "success": True,
            "confidence": confidence,
            "successful_parses": successful_parses,
            "total_attempts": total_attempts,
            "parsing_errors": parsing_errors[:5],  # Limit error samples
            "template_id": template_data.get('template_id'),
            "template_name": template_data.get('name')
        }
    
    def _parse_with_template(self, raw_data: str, template_fields: List[Dict]) -> Dict[str, Any]:
        """Try to parse raw data using template field definitions"""
        
        try:
            parsed_data = {}
            
            for field in template_fields:
                field_name = field['name']
                start_pos = field['start']
                length = field['length']
                field_type = field['field_type']
                
                # Extract field data
                if start_pos + length <= len(raw_data):
                    field_data = raw_data[start_pos:start_pos + length]
                else:
                    return {"success": False, "error": f"Data too short for field {field_name}"}
                
                # Validate field type
                if field_type == "numeric":
                    try:
                        # Try to extract numeric value
                        numeric_value = float(field_data.strip())
                        parsed_data[field_name] = numeric_value
                    except ValueError:
                        return {"success": False, "error": f"Non-numeric data in field {field_name}: '{field_data}'"}
                
                elif field_type == "lookup":
                    field_values = field.get('values', {})
                    if field_data in field_values:
                        parsed_data[field_name] = field_values[field_data]
                    else:
                        # Allow unknown lookup values but note them
                        parsed_data[field_name] = f"unknown_{field_data}"
                
                else:
                    # Text field - accept as-is
                    parsed_data[field_name] = field_data
            
            return {"success": True, "parsed_data": parsed_data}
            
        except Exception as e:
            return {"success": False, "error": str(e)}
    
    def _check_data_consistency(self, test_data: List[Dict], template_fields: List[Dict]) -> float:
        """Check consistency of data format across samples"""
        
        if len(test_data) < 2:
            return 50.0  # Not enough data to check consistency
        
        # Check if all data entries have similar length
        lengths = [len(entry['data']) for entry in test_data]
        avg_length = sum(lengths) / len(lengths)
        length_variance = sum((l - avg_length) ** 2 for l in lengths) / len(lengths)
        
        # Low variance in length indicates consistent format
        if length_variance < 2:
            return 90.0
        elif length_variance < 10:
            return 70.0
        else:
            return 30.0
    
    def _check_format_characteristics(self, test_data: List[Dict], template_data: Dict) -> float:
        """Check if data matches expected format characteristics"""
        
        delimiter = template_data.get('delimiter', '\\r\\n').replace('\\r', '\r').replace('\\n', '\n')
        
        # Check delimiter presence
        delimiter_matches = 0
        for entry in test_data:
            if delimiter.strip() in entry['data'] or len(entry['data'].strip()) > 0:
                delimiter_matches += 1
        
        if len(test_data) == 0:
            return 0.0
        
        delimiter_score = (delimiter_matches / len(test_data)) * 100
        
        # Additional format checks could be added here
        # (specific patterns, expected characters, etc.)
        
        return delimiter_score


class ProtocolDiscoveryEngine:
    """Intelligent protocol discovery engine for weight scales"""
    
    def __init__(self, config: Dict[str, Any], connection_manager: Adam4571Manager):
        self.config = config['discovery']
        self.connection_manager = connection_manager
        self.logger = logging.getLogger(__name__)
        self.discovery_steps = []
        self.confidence_scores = {}
        
        # Initialize template management
        self.template_manager = TemplateManager()
        self.template_validator = TemplateValidator(connection_manager)
        
    def start_discovery_session(self) -> bool:
        """Start a new protocol discovery session with template matching first"""
        self.logger.info("🔍 Starting protocol discovery session")
        self.discovery_steps = []
        self.confidence_scores = {}
        
        # Start data capture
        if not self.connection_manager.start_data_capture():
            self.logger.error("Failed to start data capture")
            return False
        
        print("\n" + "="*60)
        print("📊 WEIGHT SCALE PROTOCOL DISCOVERY")
        print("="*60)
        print("\nThis process will automatically detect your scale's data format.")
        print("First, we'll try known manufacturer templates, then discovery if needed.")
        
        # Phase 1: Try known templates
        print("\n🔍 Phase 1: Testing Known Manufacturer Templates")
        print("="*50)
        
        template_result = self._try_known_templates()
        
        if template_result['success'] and template_result['confidence'] >= self.config['confidence_threshold']:
            print(f"\n🎯 SUCCESS! Found matching template: {template_result['template_name']}")
            print(f"   Confidence: {template_result['confidence']:.1f}%")
            print(f"   Template ID: {template_result['template_id']}")
            
            # Save the successful template
            template_data = self.template_manager.get_template(template_result['template_id'])
            if template_data:
                template_file = self._save_template_as_discovered(template_data)
                print(f"   Saved as: {template_file}")
            
            return True
        
        # Phase 2: Interactive discovery if templates failed
        print(f"\n⚠️  No suitable template found (best: {template_result.get('confidence', 0):.1f}%)")
        print("\n🔍 Phase 2: Interactive Protocol Discovery")
        print("="*50)
        print("Please follow the prompts to add/remove weights as requested.")
        print("\nStep 1: Baseline Reading (Empty Scale)")
        print("Please ensure the scale is empty and stable.")
        input("Press Enter when ready to capture baseline...")
        
        return self._capture_baseline()
    
    def _try_known_templates(self) -> Dict[str, Any]:
        """Try all known templates against live data"""
        
        templates = self.template_manager.get_all_templates()
        
        if not templates:
            self.logger.warning("No templates available for testing")
            return {"success": False, "confidence": 0, "error": "No templates available"}
        
        print(f"📋 Testing {len(templates)} known templates...")
        
        best_result = {"success": False, "confidence": 0}
        test_results = []
        
        # Test each template
        for template_id, template_data in templates.items():
            manufacturer_info = template_data.get('manufacturer_info', {})
            brand = manufacturer_info.get('brand', 'Unknown')
            
            print(f"   🔧 Testing {brand}: {template_data.get('name', template_id)[:40]}...")
            
            try:
                result = self.template_validator.test_template(template_data, test_duration=5.0)
                result['template_id'] = template_id
                result['template_name'] = template_data.get('name', template_id)
                result['brand'] = brand
                
                test_results.append(result)
                
                if result.get('success', False) and result.get('confidence', 0) > best_result.get('confidence', 0):
                    best_result = result
                
                print(f"      Confidence: {result.get('confidence', 0):.1f}%")
                
                # Early exit if we find a very high confidence match
                if result.get('confidence', 0) >= 95:
                    print(f"      🎯 Excellent match found! Stopping tests.")
                    break
                    
            except Exception as e:
                self.logger.warning(f"Error testing template {template_id}: {e}")
                print(f"      ❌ Test failed: {e}")
        
        # Show summary of results
        self._show_template_test_summary(test_results)
        
        return best_result
    
    def _show_template_test_summary(self, test_results: List[Dict]):
        """Show summary of template test results"""
        
        print(f"\n📊 Template Test Results:")
        print("-" * 60)
        
        # Sort by confidence score
        sorted_results = sorted(test_results, key=lambda x: x.get('confidence', 0), reverse=True)
        
        for result in sorted_results[:5]:  # Show top 5 results
            confidence = result.get('confidence', 0)
            brand = result.get('brand', 'Unknown')
            name = result.get('template_name', 'Unknown')[:30]
            
            status = "✅" if confidence >= 85 else "⚠️" if confidence >= 50 else "❌"
            print(f"{status} {confidence:5.1f}% - {brand:15} - {name}")
        
        if len(sorted_results) > 5:
            print(f"    ... and {len(sorted_results) - 5} more templates tested")
    
    def _save_template_as_discovered(self, template_data: Dict) -> str:
        """Save a known template as a discovered template for consistency"""
        
        # Create a copy with discovery metadata
        discovered_template = template_data.copy()
        discovered_template['template_id'] = f"matched_{template_data['template_id']}_{int(time.time())}"
        discovered_template['discovery_date'] = datetime.now(timezone.utc).isoformat()
        discovered_template['discovery_method'] = "template_matching"
        discovered_template['original_template'] = template_data['template_id']
        
        # Save to file
        templates_dir = Path("protocol_templates")
        templates_dir.mkdir(exist_ok=True)
        
        template_file = templates_dir / f"{discovered_template['template_id']}.json"
        
        with open(template_file, 'w') as f:
            json.dump(discovered_template, f, indent=2)
        
        return str(template_file)
    
    def _capture_baseline(self) -> bool:
        """Capture baseline data from empty scale"""
        self.connection_manager.clear_buffer()
        
        print("📋 Capturing baseline data for 5 seconds...")
        time.sleep(5)
        
        baseline_data = self.connection_manager.get_recent_data(6.0)
        
        if not baseline_data:
            print("❌ No data received from scale. Check connection.")
            return False
        
        # Store baseline step
        step = DiscoveryStep(
            step_number=1,
            action='baseline',
            expected_weight=0.0,
            captured_data=[entry['data'] for entry in baseline_data],
            timestamp=datetime.now(timezone.utc)
        )
        self.discovery_steps.append(step)
        
        print(f"✅ Captured {len(baseline_data)} baseline data points")
        self._show_sample_data(baseline_data[:3])
        
        return self._run_weight_tests()
    
    def _run_weight_tests(self) -> bool:
        """Run interactive weight testing sequence"""
        test_weights = [1.0, 5.0, 10.0, 2.5]  # Suggested test weights
        
        for i, weight in enumerate(test_weights, 2):
            print(f"\nStep {i}: Weight Test - {weight} kg")
            print(f"Please place a {weight} kg weight on the scale.")
            input("Press Enter when scale is stable...")
            
            if not self._capture_weight_step(i, 'add_weight', weight):
                continue
            
            # Calculate confidence after each step
            self._calculate_confidence()
            
            if self.confidence_scores.get('overall', 0) > self.config['confidence_threshold']:
                print(f"🎯 High confidence achieved ({self.confidence_scores['overall']:.1f}%)")
                break
        
        return self._finalize_discovery()
    
    def _capture_weight_step(self, step_number: int, action: str, expected_weight: float) -> bool:
        """Capture data for a weight test step"""
        self.connection_manager.clear_buffer()
        
        print(f"📊 Capturing data for {expected_weight} kg...")
        time.sleep(3)
        
        step_data = self.connection_manager.get_recent_data(4.0)
        
        if not step_data:
            print("⚠️  No data captured for this step")
            return False
        
        step = DiscoveryStep(
            step_number=step_number,
            action=action,
            expected_weight=expected_weight,
            captured_data=[entry['data'] for entry in step_data],
            timestamp=datetime.now(timezone.utc)
        )
        self.discovery_steps.append(step)
        
        print(f"✅ Captured {len(step_data)} data points")
        self._show_sample_data(step_data[:2])
        
        return True
    
    def _show_sample_data(self, data_entries: List[Dict]):
        """Show sample data to user"""
        print("📄 Sample data:")
        for entry in data_entries:
            print(f"   {repr(entry['data'])}")
    
    def _calculate_confidence(self):
        """Calculate confidence scores for protocol detection"""
        if len(self.discovery_steps) < 2:
            return
        
        # Simple confidence calculation based on data consistency
        all_data = []
        for step in self.discovery_steps:
            all_data.extend(step.captured_data)
        
        # Check for consistent frame format
        if all_data:
            # Look for common delimiters
            has_crlf = sum(1 for d in all_data if '\r' in d or '\n' in d)
            format_consistency = (has_crlf / len(all_data)) * 100 if all_data else 0
            
            # Look for numeric patterns
            numeric_patterns = sum(1 for d in all_data if re.search(r'\d+\.?\d*', d))
            numeric_confidence = (numeric_patterns / len(all_data)) * 100 if all_data else 0
            
            # Overall confidence
            overall = (format_consistency + numeric_confidence) / 2
            
            self.confidence_scores = {
                'format': format_consistency,
                'numeric': numeric_confidence,
                'overall': overall
            }
            
            print(f"🔍 Confidence: Format={format_consistency:.1f}%, Numeric={numeric_confidence:.1f}%, Overall={overall:.1f}%")
    
    def _finalize_discovery(self) -> bool:
        """Finalize discovery and create protocol template"""
        print("\n" + "="*60)
        print("🎯 DISCOVERY COMPLETE")
        print("="*60)
        
        if not self.discovery_steps:
            print("❌ No discovery data captured")
            return False
        
        # Analyze captured data to create template
        template = self._analyze_and_create_template()
        
        if template:
            # Save template
            template_path = self._save_template(template)
            print(f"✅ Protocol template saved: {template_path}")
            
            # Show template summary
            self._show_template_summary(template)
            
            return True
        else:
            print("❌ Failed to create protocol template")
            return False
    
    def _analyze_and_create_template(self) -> Optional[ProtocolTemplate]:
        """Analyze captured data and create protocol template"""
        # Simple analysis - look for numeric patterns
        sample_data = self.discovery_steps[0].captured_data
        if not sample_data:
            return None
        
        # Basic template creation
        template = ProtocolTemplate(
            template_id=f"discovered_{int(time.time())}",
            name="Auto-Discovered Scale Protocol",
            description=f"Discovered on {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            delimiter=r"\r\n",  # Most common
            encoding="ASCII",
            fields=[
                ProtocolField(
                    name="weight",
                    start=0,
                    length=len(sample_data[0]) if sample_data else 10,
                    field_type="numeric",
                    decimal_places=2
                )
            ],
            confidence_score=self.confidence_scores.get('overall', 0),
            discovery_date=datetime.now(timezone.utc)
        )
        
        return template
    
    def _save_template(self, template: ProtocolTemplate) -> str:
        """Save protocol template to file and database"""
        # Save to file (for backward compatibility)
        templates_dir = Path("protocol_templates")
        templates_dir.mkdir(exist_ok=True)
        
        template_file = templates_dir / f"{template.template_id}.json"
        
        # Convert to dict for JSON serialization
        template_dict = asdict(template)
        template_dict['discovery_date'] = template.discovery_date.isoformat()
        
        with open(template_file, 'w') as f:
            json.dump(template_dict, f, indent=2)
        
        return str(template_file)
    
    def _show_template_summary(self, template: ProtocolTemplate):
        """Show template summary to user"""
        print(f"\n📋 Template Summary:")
        print(f"   ID: {template.template_id}")
        print(f"   Name: {template.name}")
        print(f"   Confidence: {template.confidence_score:.1f}%")
        print(f"   Fields: {len(template.fields)}")


class ScaleDiscoveryApplication:
    """Main application class for scale discovery"""
    
    def __init__(self, config_file: str = "adam_scale_config.json"):
        # Load configuration
        self.config_manager = ConfigManager(config_file)
        self.config = self.config_manager.config
        
        # Setup logging
        self._setup_logging()
        self.logger = logging.getLogger(__name__)
        
        # Initialize components
        self.connection_manager = Adam4571Manager(self.config)
        self.database_manager = DatabaseManager(self.config)
        self.discovery_engine = ProtocolDiscoveryEngine(self.config, self.connection_manager)
        
        self.logger.info("Scale Discovery Application initialized")
    
    def _setup_logging(self):
        """Configure comprehensive logging for troubleshooting ADAM device setup and operation"""
        log_config = self.config['logging']
        
        # Create detailed formatter for troubleshooting
        formatter = logging.Formatter(
            '%(asctime)s.%(msecs)03d - %(name)s - %(levelname)s - [%(funcName)s:%(lineno)d] - %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'
        )
        
        # Setup file handler with rotation using configured size
        from logging.handlers import RotatingFileHandler
        max_bytes = log_config['max_log_size_mb'] * 1024 * 1024  # Convert MB to bytes
        file_handler = RotatingFileHandler(
            log_config['log_file'],
            maxBytes=max_bytes,
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
    
    def run_discovery(self):
        """Run the interactive discovery process"""
        try:
            self.logger.info("Starting scale discovery application")
            
            # Test connection first
            if not self.connection_manager.connect():
                print("❌ Failed to connect to ADAM-4571. Please check configuration.")
                return False
            
            # Run discovery
            success = self.discovery_engine.start_discovery_session()
            
            if success:
                print("\n🎉 Discovery completed successfully!")
                print("You can now use the generated template for weight monitoring.")
            else:
                print("\n⚠️  Discovery failed. Please check connection and try again.")
            
            return success
            
        except KeyboardInterrupt:
            print("\n\n⏹️  Discovery cancelled by user")
            return False
        except Exception as e:
            self.logger.error(f"Unexpected error in discovery: {e}")
            print(f"\n❌ Error: {e}")
            return False
        finally:
            self.connection_manager.close()
            self.database_manager.close()
    
    def test_connection(self):
        """Test connection to ADAM-4571"""
        print("🔗 Testing connection to ADAM-4571...")
        
        if self.connection_manager.connect():
            print("✅ Connection successful!")
            
            # Test data capture
            print("📡 Testing data capture for 5 seconds...")
            self.connection_manager.start_data_capture()
            time.sleep(5)
            
            data = self.connection_manager.get_recent_data()
            print(f"📊 Captured {len(data)} data points")
            
            if data:
                print("📄 Sample data:")
                for entry in data[:3]:
                    print(f"   {repr(entry['data'])}")
            else:
                print("⚠️  No data received")
            
            self.connection_manager.close()
            return True
        else:
            print("❌ Connection failed!")
            return False
    
    def test_specific_template(self, template_id: str):
        """Test a specific template against the scale"""
        print(f"🔧 Testing specific template: {template_id}")
        
        if not self.connection_manager.connect():
            print("❌ Failed to connect to ADAM-4571. Please check configuration.")
            return False
        
        template_manager = TemplateManager()
        template_data = template_manager.get_template(template_id)
        
        if not template_data:
            print(f"❌ Template '{template_id}' not found.")
            print(f"Available templates: {list(template_manager.get_all_templates().keys())}")
            return False
        
        template_validator = TemplateValidator(self.connection_manager)
        
        print(f"📋 Template: {template_data.get('name', 'Unknown')}")
        print(f"🏭 Manufacturer: {template_data.get('manufacturer_info', {}).get('brand', 'Unknown')}")
        print("🔍 Testing against live scale data...")
        
        try:
            result = template_validator.test_template(template_data, test_duration=10.0)
            
            if result['success']:
                confidence = result['confidence']
                print(f"\n📊 Test Results:")
                print(f"   Confidence Score: {confidence:.1f}%")
                print(f"   Successful Parses: {result['successful_parses']}/{result['total_attempts']}")
                
                if confidence >= 85:
                    print("✅ EXCELLENT match - This template should work well!")
                elif confidence >= 70:
                    print("⚠️  GOOD match - Template may work with minor issues")
                elif confidence >= 50:
                    print("⚠️  FAIR match - Template partially compatible") 
                else:
                    print("❌ POOR match - Template not suitable for this scale")
                
                if result.get('parsing_errors'):
                    print(f"\n⚠️  Sample parsing errors:")
                    for error in result['parsing_errors'][:3]:
                        print(f"   - {error}")
                
                return confidence >= 70
            else:
                print(f"❌ Template test failed: {result.get('error', 'Unknown error')}")
                return False
                
        except Exception as e:
            print(f"❌ Error during template test: {e}")
            return False
        finally:
            self.connection_manager.close()
    
    def list_available_templates(self):
        """List all available protocol templates"""
        template_manager = TemplateManager()
        templates = template_manager.get_all_templates()
        manufacturers = template_manager.get_manufacturer_templates()
        
        if not templates:
            print("❌ No protocol templates found.")
            print("   Check that the 'protocol_templates' directory exists and contains template files.")
            return
        
        print(f"📋 Available Protocol Templates ({len(templates)} total)")
        print("="*60)
        
        for manufacturer, template_ids in manufacturers.items():
            print(f"\n🏭 {manufacturer}:")
            for template_id in template_ids:
                template_data = templates[template_id]
                name = template_data.get('name', 'Unknown')
                confidence = template_data.get('confidence_score', 0)
                print(f"   📄 {template_id}")
                print(f"      Name: {name}")
                print(f"      Confidence: {confidence:.1f}%")
                
                models = template_data.get('manufacturer_info', {}).get('common_models', [])
                if models:
                    print(f"      Models: {', '.join(models[:3])}")
        
        print(f"\nUsage:")
        print(f"  python adam_scale_discovery.py --template <template_id>")
        print(f"  python adam_scale_discovery.py --discover  # Try all templates automatically")


def main():
    """Main entry point for ADAM-4571 Scale Discovery Application
    
    Provides command-line interface for:
    - Connection testing to ADAM-4571 device
    - Interactive protocol discovery process
    - Custom configuration file specification
    
    Usage:
        python adam_scale_discovery.py                              # Show usage help
        python adam_scale_discovery.py --test                      # Test connection only
        python adam_scale_discovery.py --discover                  # Run discovery process
        python adam_scale_discovery.py --config custom.json --test # Use custom config
    
    Example:
        # Test connectivity and configuration
        python adam_scale_discovery.py --test
        
        # Start interactive protocol discovery
        python adam_scale_discovery.py --discover
    """
    import argparse
    
    parser = argparse.ArgumentParser(description="ADAM-4571 Scale Protocol Discovery")
    parser.add_argument(
        "--config", 
        default="adam_scale_config.json",
        help="Configuration file path (default: adam_scale_config.json)"
    )
    parser.add_argument(
        "--test", 
        action="store_true",
        help="Test connection only"
    )
    parser.add_argument(
        "--discover", 
        action="store_true",
        help="Run protocol discovery (templates first, then interactive)"
    )
    parser.add_argument(
        "--template", 
        type=str,
        help="Test a specific template against the scale"
    )
    parser.add_argument(
        "--list-templates", 
        action="store_true",
        help="List all available protocol templates"
    )
    
    args = parser.parse_args()
    
    app = ScaleDiscoveryApplication(args.config)
    
    if args.test:
        app.test_connection()
    elif args.discover:
        app.run_discovery()
    elif args.template:
        app.test_specific_template(args.template)
    elif args.list_templates:
        app.list_available_templates()
    else:
        print("ADAM-4571 Scale Discovery Application")
        print("Usage:")
        print("  --test              Test connection to ADAM-4571")
        print("  --discover          Run protocol discovery (try templates first)")
        print("  --template <id>     Test specific template against scale")
        print("  --list-templates    Show all available templates")


if __name__ == "__main__":
    main()