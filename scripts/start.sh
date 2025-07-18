#!/bin/bash

# MCPVault Start Script

set -e

echo "Starting MCPVault Development Environment"
echo "========================================"

# Load environment variables
if [ -f .env.docker ]; then
    export $(cat .env.docker | grep -v '^#' | xargs)
fi

# Start all Docker services
echo "Starting Docker services..."
npm run docker:up

# Wait for services to be healthy
echo "Waiting for services to be ready..."
sleep 5

# Check service status
npm run docker:status

echo ""
echo "MCPVault services are running!"
echo ""
echo "Available services:"
echo "  PostgreSQL:      localhost:${POSTGRES_PORT:-14300}"
echo "  Redis:           localhost:${REDIS_PORT:-14301}"
echo "  pgAdmin:         http://localhost:${PGADMIN_PORT:-14310}"
echo "  Redis Commander: http://localhost:${REDIS_COMMANDER_PORT:-14320}"
echo ""
echo "To start the API server, run: npm start"
echo "To view logs, run: npm run docker:logs"
echo "To stop services, run: npm run docker:down"