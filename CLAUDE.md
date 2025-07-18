# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCPVault is an enterprise-grade MCP (Model Context Protocol) access management platform that provides secure, compliant, and role-based access to MCP servers through an intelligent intermediary layer. The project aims to help organizations maintain their security certifications (SOC2, HIPAA, PCI, GDPR, FedRAMP) while safely leveraging MCP capabilities.

**Current Status**: The project is in the planning and architecture phase (Week 1 of a 28-week roadmap). Database schemas have been designed, but no application code has been implemented yet.

## Repository Structure

```
/mnt/d/dev2/mcpvault/
├── Documentation files (README.md, REQUIREMENTS.md, TODO.md, FUTURE.md, PROJECT_OVERVIEW.md)
├── site/
│   └── index.html           # SaaS landing page
└── dbbuilder/
    └── mcpvault/
        ├── schemas/         # PostgreSQL database schemas
        │   ├── 001_core_schema.sql      # Core auth and MCP tables
        │   └── 002_vault_jobs_schema.sql # Key vault and job orchestration
        └── procedures/      # Stored procedures
            └── 001_auth_procedures.sql   # Authentication procedures
```

## Database Architecture

The PostgreSQL database uses five main schemas:
- **auth**: Authentication, users, organizations, roles, sessions
- **mcp**: MCP server configurations, connections, veneering
- **vault**: Key vault provider integrations (no secrets stored)
- **jobs**: Asynchronous job orchestration and tracking
- **audit**: Comprehensive audit logging for compliance

Key design principles:
- Multi-tenant with organization-based isolation
- Row-level security using organization_id
- Comprehensive audit trails on all operations
- No secrets stored in database (uses external key vaults)

## Development Commands

Since the project has no code implementation yet, here are the planned commands based on the documentation:

### Database Setup
```bash
# Initialize database (when implemented)
./scripts/init-db.sh

# Apply database migrations
# TODO: Implement migration system
```

### Technology Stack (Planned)
The project recommends .NET Core 8.0 as the primary implementation. When implemented:
```bash
# .NET Core commands (future)
dotnet restore
dotnet build
dotnet test
dotnet run

# Alternative implementations mentioned:
# Rust: cargo build && cargo test
# Go: go mod download && go build
```

## Key Technical Decisions

1. **Zero-Trust Security Architecture**: Every request must be authenticated and authorized
2. **MCP Veneering**: Customizes how MCP servers are presented to different users/roles
3. **Job Orchestration**: All long-running operations are async with progress tracking
4. **Compliance-First Design**: Built to maintain existing enterprise certifications
5. **Multi-Cloud Support**: Abstracts key vault providers (Azure, AWS, GCP, HashiCorp)

## Important Implementation Notes

1. **WSL to SQL Server Connections**: Use IP address 172.31.208.1,14333 instead of localhost (see global CLAUDE.md)
2. **Password Handling**: Never quote passwords in SQL commands, even with special characters
3. **Multi-Tenancy**: Always filter by organization_id in all queries
4. **Audit Everything**: Every operation must create audit trail entries
5. **No Direct MCP Access**: All MCP server access must go through the proxy layer

## Current Development Priorities (from TODO.md)

Week 1-2 focus:
1. Finalize technology stack decision
2. Set up development environment
3. Implement database connection and test schemas
4. Create authentication prototype
5. Set up CI/CD pipeline

## Testing Strategy

When implemented, the project requires:
- Minimum 80% unit test coverage
- Integration tests for all API endpoints
- Security test automation
- Performance testing for scale
- Compliance validation tests

## References

- See README.md for project overview and vision
- See REQUIREMENTS.md for detailed functional/non-functional requirements
- See TODO.md for the 28-week development roadmap
- See PROJECT_OVERVIEW.md for technical architecture details
- See FUTURE.md for long-term vision and AI integration plans