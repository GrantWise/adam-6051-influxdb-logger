#!/bin/bash
set -e

# ADAM Scale Logger Entrypoint Script
# Implements industrial software standards for service startup

echo "🏭 ADAM Scale Logger - Industrial Grade Service"
echo "=============================================="

# Function to wait for service dependencies
wait_for_service() {
    local host=$1
    local port=$2
    local service_name=$3
    local timeout=${4:-30}
    
    echo "⏳ Waiting for $service_name at $host:$port..."
    
    for i in $(seq 1 $timeout); do
        if timeout 1 bash -c "echo >/dev/tcp/$host/$port" 2>/dev/null; then
            echo "✅ $service_name is ready"
            return 0
        fi
        echo "   Attempt $i/$timeout - $service_name not ready, waiting..."
        sleep 1
    done
    
    echo "❌ $service_name failed to become ready within $timeout seconds"
    return 1
}

# Function to validate configuration
validate_config() {
    echo "🔍 Validating configuration..."
    
    # Check required environment variables
    if [[ -z "$ADAM_HOST" ]]; then
        echo "⚠️  ADAM_HOST not set, using default: 192.168.1.101"
        export ADAM_HOST="192.168.1.101"
    fi
    
    if [[ -z "$DATABASE_TYPE" ]]; then
        echo "⚠️  DATABASE_TYPE not set, using default: sqlite"
        export DATABASE_TYPE="sqlite"
    fi
    
    echo "📋 Configuration:"
    echo "   ADAM Device: $ADAM_HOST:${ADAM_PORT:-4001}"
    echo "   Database: $DATABASE_TYPE"
    echo "   Log Level: ${LOG_LEVEL:-INFO}"
    echo "   Service Mode: ${SERVICE_MODE:-true}"
}

# Function to test ADAM device connectivity
test_adam_connectivity() {
    echo "🔗 Testing ADAM device connectivity..."
    
    local adam_host=${ADAM_HOST:-192.168.1.101}
    local adam_port=${ADAM_PORT:-4001}
    
    if timeout 5 bash -c "echo >/dev/tcp/$adam_host/$adam_port" 2>/dev/null; then
        echo "✅ ADAM device is reachable at $adam_host:$adam_port"
        return 0
    else
        echo "⚠️  ADAM device not reachable at $adam_host:$adam_port"
        echo "   This may be normal if the device is not yet connected"
        return 1
    fi
}

# Function to start health monitoring server
start_health_server() {
    echo "🏥 Starting health monitoring server..."
    
    cat > /tmp/health_server.py << 'EOF'
#!/usr/bin/env python3
import http.server
import socketserver
import json
import socket
import os
from datetime import datetime

class HealthHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/health':
            health_status = self.check_health()
            self.send_response(200 if health_status['healthy'] else 503)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps(health_status, indent=2).encode())
        else:
            self.send_response(404)
            self.end_headers()
    
    def check_health(self):
        checks = {}
        overall_healthy = True
        
        # Check ADAM device connectivity
        adam_host = os.getenv('ADAM_HOST', '192.168.1.101')
        adam_port = int(os.getenv('ADAM_PORT', '4001'))
        
        try:
            sock = socket.create_connection((adam_host, adam_port), timeout=5)
            sock.close()
            checks['adam_device'] = {'status': 'healthy', 'message': f'Connected to {adam_host}:{adam_port}'}
        except Exception as e:
            checks['adam_device'] = {'status': 'unhealthy', 'message': f'Cannot connect to {adam_host}:{adam_port}: {str(e)}'}
            overall_healthy = False
        
        # Check database connectivity
        db_type = os.getenv('DATABASE_TYPE', 'sqlite')
        if db_type == 'postgresql':
            db_host = os.getenv('DATABASE_HOST', 'postgres')
            db_port = int(os.getenv('DATABASE_PORT', '5432'))
            
            try:
                sock = socket.create_connection((db_host, db_port), timeout=3)
                sock.close()
                checks['database'] = {'status': 'healthy', 'message': f'PostgreSQL reachable at {db_host}:{db_port}'}
            except Exception as e:
                checks['database'] = {'status': 'unhealthy', 'message': f'PostgreSQL unreachable: {str(e)}'}
                overall_healthy = False
        else:
            checks['database'] = {'status': 'healthy', 'message': 'SQLite (local file)'}
        
        return {
            'healthy': overall_healthy,
            'timestamp': datetime.utcnow().isoformat() + 'Z',
            'service': 'adam-scale-logger',
            'version': '1.0.0',
            'checks': checks
        }

if __name__ == '__main__':
    port = int(os.getenv('HEALTH_CHECK_PORT', '8080'))
    with socketserver.TCPServer(("", port), HealthHandler) as httpd:
        print(f"Health server running on port {port}")
        httpd.serve_forever()
EOF
    
    python3 /tmp/health_server.py &
    HEALTH_PID=$!
    echo "✅ Health server started (PID: $HEALTH_PID)"
}

# Function to handle graceful shutdown
cleanup() {
    echo "🛑 Shutting down services gracefully..."
    
    # Kill health server
    if [[ -n "$HEALTH_PID" ]]; then
        kill $HEALTH_PID 2>/dev/null || true
    fi
    
    # Kill main application
    if [[ -n "$MAIN_PID" ]]; then
        kill $MAIN_PID 2>/dev/null || true
        wait $MAIN_PID 2>/dev/null || true
    fi
    
    echo "✅ Shutdown complete"
    exit 0
}

# Set up signal handlers
trap cleanup SIGTERM SIGINT

# Main execution
main() {
    echo "🚀 Starting ADAM Scale Logger service..."
    
    # Validate configuration
    validate_config
    
    # Wait for database if using PostgreSQL
    if [[ "$DATABASE_TYPE" == "postgresql" ]]; then
        wait_for_service "${DATABASE_HOST:-postgres}" "${DATABASE_PORT:-5432}" "PostgreSQL" 60
    fi
    
    # Test ADAM device connectivity (non-blocking)
    test_adam_connectivity || echo "   Continuing startup - device may connect later"
    
    # Start health monitoring server
    start_health_server
    
    # Start main application based on command
    case "$1" in
        "--discover")
            echo "🔍 Starting discovery mode..."
            python3 adam_scale_discovery.py --discover &
            MAIN_PID=$!
            ;;
        "--test")
            echo "🧪 Running connectivity test..."
            python3 adam_scale_discovery.py --test
            exit $?
            ;;
        "--monitor"|"")
            echo "📊 Starting continuous monitoring mode..."
            python3 adam_scale_logger.py --monitor &
            MAIN_PID=$!
            ;;
        *)
            echo "❌ Unknown command: $1"
            echo "Valid commands: --discover, --test, --monitor"
            exit 1
            ;;
    esac
    
    echo "✅ Service startup complete"
    echo "📡 Health endpoint: http://localhost:${HEALTH_CHECK_PORT:-8080}/health"
    
    # Wait for main process
    wait $MAIN_PID
}

# Execute main function with all arguments
main "$@"