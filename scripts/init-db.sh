#!/bin/bash

# MCPVault Database Initialization Script

set -e

echo "MCPVault Database Initialization"
echo "================================"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker and try again."
    exit 1
fi

# Load environment variables
if [ -f .env.docker ]; then
    export $(cat .env.docker | grep -v '^#' | xargs)
fi

# Start PostgreSQL container
echo "Starting PostgreSQL container..."
docker-compose --env-file .env.docker up -d postgres

# Wait for PostgreSQL to be ready
echo "Waiting for PostgreSQL to be ready..."
until docker exec mcpvault_postgres pg_isready -U mcpvault -d mcpvault > /dev/null 2>&1; do
    echo -n "."
    sleep 1
done
echo " Ready!"

# Run database initialization scripts
echo "Running database initialization scripts..."

# Execute schema files
for schema_file in dbbuilder/mcpvault/schemas/*.sql; do
    if [ -f "$schema_file" ]; then
        echo "Executing: $schema_file"
        docker exec -i mcpvault_postgres psql -U mcpvault -d mcpvault < "$schema_file"
    fi
done

# Execute procedure files
for proc_file in dbbuilder/mcpvault/procedures/*.sql; do
    if [ -f "$proc_file" ]; then
        echo "Executing: $proc_file"
        docker exec -i mcpvault_postgres psql -U mcpvault -d mcpvault < "$proc_file"
    fi
done

echo "Database initialization complete!"
echo ""
echo "Connection details:"
echo "  Host: localhost"
echo "  Port: ${POSTGRES_PORT:-14300}"
echo "  Database: mcpvault"
echo "  Username: mcpvault"
echo "  Password: ${POSTGRES_PASSWORD:-changeme}"
echo ""
echo "pgAdmin available at: http://localhost:${PGADMIN_PORT:-14310}"