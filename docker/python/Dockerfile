# Use official Python runtime as base image
FROM python:3.9-slim

# Set working directory in container
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application files
COPY adam_counter_logger.py .
COPY entrypoint.sh .

# Make entrypoint script executable
RUN chmod +x entrypoint.sh

# Create directories for config and logs
RUN mkdir -p /app/config /app/logs

# Create non-root user for security
RUN useradd --create-home --shell /bin/bash adam && \
    chown -R adam:adam /app
USER adam

# Health check to verify application is running
HEALTHCHECK --interval=60s --timeout=30s --start-period=30s --retries=3 \
    CMD python -c "import sys; sys.exit(0)"

# Set default environment variables
ENV PYTHONUNBUFFERED=1
ENV LOG_LEVEL=INFO

# Expose no ports (this is a background service)

# Use entrypoint script to handle configuration
ENTRYPOINT ["./entrypoint.sh"]