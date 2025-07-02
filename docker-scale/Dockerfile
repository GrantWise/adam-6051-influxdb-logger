# Production-ready ADAM-4571 Scale Logger Container
# Based on industrial software development standards

FROM python:3.11-slim

# Create application user for security
RUN groupadd -r adamscale && useradd -r -g adamscale adamscale

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for better caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY adam_scale_logger.py .
COPY adam_scale_discovery.py .
COPY entrypoint.sh .

# Copy protocol templates
COPY protocol_templates/ ./protocol_templates/

# Create necessary directories
RUN mkdir -p /app/logs /app/data /app/config /app/templates

# Set proper permissions
RUN chown -R adamscale:adamscale /app
RUN chmod +x entrypoint.sh

# Switch to non-root user
USER adamscale

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD python -c "import socket; socket.create_connection(('${ADAM_HOST:-192.168.1.101}', ${ADAM_PORT:-4001}), timeout=5).close()" || exit 1

# Expose port for health monitoring
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["./entrypoint.sh"]
CMD ["--monitor"]