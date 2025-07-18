# MCPVault Docker Configuration

This document describes the Docker isolation strategy for MCPVault development environment to ensure complete isolation from other Docker projects.

## Project Configuration

- **Project Prefix**: `mcpvault_`
- **Port Range**: 14300-14399
- **Network Subnet**: 172.30.0.0/16

## Port Allocations

- **14300**: PostgreSQL Database
- **14301**: Redis Cache
- **14302**: MCPVault API (HTTP)
- **14303**: MCPVault API (HTTPS)
- **14310**: pgAdmin Web Interface
- **14320**: Redis Commander

## Docker Resources Naming

All Docker resources use the project prefix to ensure isolation:

- **Containers**: `mcpvault_postgres`, `mcpvault_redis`, `mcpvault_api`
- **Networks**: `mcpvault_network`
- **Volumes**: `mcpvault_postgres_data`, `mcpvault_redis_data`

## Environment Variables

The `.env.docker` file centralizes Docker-specific configuration:

```env
COMPOSE_PROJECT_NAME=mcpvault
POSTGRES_PORT=14300
REDIS_PORT=14301
API_HTTP_PORT=14302
API_HTTPS_PORT=14303
PGADMIN_PORT=14310
REDIS_COMMANDER_PORT=14320
NETWORK_SUBNET=172.30.0.0/16
```

## NPM Scripts

Common Docker operations are available via npm scripts:

```bash
npm run docker:up       # Start all services
npm run docker:down     # Stop all services
npm run docker:clean    # Remove containers and volumes
npm run docker:logs     # View logs for all services
npm run docker:status   # Check status of all services
```

## Troubleshooting

### Port Conflicts
If you encounter port conflicts, check if ports in the 14300-14399 range are already in use:
```bash
netstat -an | grep 143[0-9][0-9]
```

### Network Conflicts
If the subnet 172.30.0.0/16 is already in use, update the NETWORK_SUBNET in `.env.docker`.

### Container Name Conflicts
All containers are prefixed with `mcpvault_`. If conflicts occur, ensure no other project uses this prefix.