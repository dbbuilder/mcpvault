-- MCPVault Database Schema - Core Tables
-- PostgreSQL Implementation for Enterprise MCP Compliance Platform
-- Author: MCPVault Development Team
-- Version: 1.0.0

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schemas for organization
CREATE SCHEMA IF NOT EXISTS auth;
CREATE SCHEMA IF NOT EXISTS mcp;
CREATE SCHEMA IF NOT EXISTS vault;
CREATE SCHEMA IF NOT EXISTS audit;
CREATE SCHEMA IF NOT EXISTS jobs;

-- Set search path
SET search_path = public, auth, mcp, vault, audit, jobs;

-- ==================================================
-- AUTHENTICATION AND AUTHORIZATION SCHEMA
-- ==================================================

-- Organizations table - Multi-tenant root entity
CREATE TABLE auth.organizations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    domain VARCHAR(255) UNIQUE NOT NULL,
    settings JSONB DEFAULT '{}',
    compliance_frameworks TEXT[] DEFAULT ARRAY[]::TEXT[],
    subscription_tier VARCHAR(50) DEFAULT 'professional',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID,
    updated_by UUID
);

-- Add indexes for organizations
CREATE INDEX idx_organizations_domain ON auth.organizations(domain);
CREATE INDEX idx_organizations_active ON auth.organizations(is_active);
CREATE INDEX idx_organizations_tier ON auth.organizations(subscription_tier);

-- Roles table - System and custom roles
CREATE TABLE auth.roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    description TEXT,
    permissions JSONB DEFAULT '{}',
    is_system_role BOOLEAN DEFAULT false,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID,
    updated_by UUID,
    UNIQUE(organization_id, name)
);

-- Add indexes for roles
CREATE INDEX idx_roles_org_id ON auth.roles(organization_id);
CREATE INDEX idx_roles_active ON auth.roles(is_active);
CREATE INDEX idx_roles_system ON auth.roles(is_system_role);

-- Teams table - Organizational hierarchy
CREATE TABLE auth.teams (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    parent_team_id UUID REFERENCES auth.teams(id) ON DELETE SET NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    settings JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID,
    updated_by UUID,
    UNIQUE(organization_id, name)
);

-- Add indexes for teams
CREATE INDEX idx_teams_org_id ON auth.teams(organization_id);
CREATE INDEX idx_teams_parent ON auth.teams(parent_team_id);
CREATE INDEX idx_teams_active ON auth.teams(is_active);

-- Users table - System users
CREATE TABLE auth.users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    email VARCHAR(255) UNIQUE NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    password_hash VARCHAR(255),
    phone VARCHAR(50),
    timezone VARCHAR(50) DEFAULT 'UTC',
    locale VARCHAR(10) DEFAULT 'en_US',
    preferences JSONB DEFAULT '{}',
    mfa_enabled BOOLEAN DEFAULT false,
    mfa_secret VARCHAR(255),
    email_verified BOOLEAN DEFAULT false,
    email_verified_at TIMESTAMPTZ,
    last_login_at TIMESTAMPTZ,
    last_login_ip INET,
    failed_login_attempts INTEGER DEFAULT 0,
    locked_until TIMESTAMPTZ,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID,
    updated_by UUID
);

-- Add indexes for users
CREATE INDEX idx_users_email ON auth.users(email);
CREATE INDEX idx_users_org_id ON auth.users(organization_id);
CREATE INDEX idx_users_active ON auth.users(is_active);
CREATE INDEX idx_users_last_login ON auth.users(last_login_at);

-- User roles assignment table
CREATE TABLE auth.user_roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES auth.roles(id) ON DELETE CASCADE,
    granted_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    granted_by UUID REFERENCES auth.users(id),
    expires_at TIMESTAMPTZ,
    is_active BOOLEAN DEFAULT true,
    UNIQUE(user_id, role_id)
);

-- Add indexes for user_roles
CREATE INDEX idx_user_roles_user_id ON auth.user_roles(user_id);
CREATE INDEX idx_user_roles_role_id ON auth.user_roles(role_id);
CREATE INDEX idx_user_roles_active ON auth.user_roles(is_active);
CREATE INDEX idx_user_roles_expires ON auth.user_roles(expires_at);

-- User teams assignment table
CREATE TABLE auth.user_teams (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    team_id UUID NOT NULL REFERENCES auth.teams(id) ON DELETE CASCADE,
    role_in_team VARCHAR(50) DEFAULT 'member',
    is_supervisor BOOLEAN DEFAULT false,
    joined_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    joined_by UUID REFERENCES auth.users(id),
    is_active BOOLEAN DEFAULT true,
    UNIQUE(user_id, team_id)
);

-- Add indexes for user_teams
CREATE INDEX idx_user_teams_user_id ON auth.user_teams(user_id);
CREATE INDEX idx_user_teams_team_id ON auth.user_teams(team_id);
CREATE INDEX idx_user_teams_supervisor ON auth.user_teams(is_supervisor);
CREATE INDEX idx_user_teams_active ON auth.user_teams(is_active);

-- Sessions table - User sessions and tokens
CREATE TABLE auth.sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    session_token VARCHAR(255) UNIQUE NOT NULL,
    refresh_token VARCHAR(255) UNIQUE,
    ip_address INET,
    user_agent TEXT,
    expires_at TIMESTAMPTZ NOT NULL,
    last_activity_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for sessions
CREATE INDEX idx_sessions_user_id ON auth.sessions(user_id);
CREATE INDEX idx_sessions_token ON auth.sessions(session_token);
CREATE INDEX idx_sessions_refresh ON auth.sessions(refresh_token);
CREATE INDEX idx_sessions_expires ON auth.sessions(expires_at);
CREATE INDEX idx_sessions_active ON auth.sessions(is_active);

-- ==================================================
-- MCP SERVER MANAGEMENT SCHEMA
-- ==================================================

-- MCP server registry
CREATE TABLE mcp.servers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    description TEXT,
    server_url VARCHAR(2048) NOT NULL,
    server_type VARCHAR(50) DEFAULT 'http',
    protocol_version VARCHAR(20) DEFAULT '1.0',
    capabilities JSONB DEFAULT '{}',
    configuration JSONB DEFAULT '{}',
    health_check_url VARCHAR(2048),
    health_check_interval_seconds INTEGER DEFAULT 300,
    last_health_check_at TIMESTAMPTZ,
    health_status VARCHAR(20) DEFAULT 'unknown',
    tags TEXT[] DEFAULT ARRAY[]::TEXT[],
    is_public BOOLEAN DEFAULT false,
    requires_authentication BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id),
    UNIQUE(organization_id, name)
);

-- Add indexes for servers
CREATE INDEX idx_servers_org_id ON mcp.servers(organization_id);
CREATE INDEX idx_servers_type ON mcp.servers(server_type);
CREATE INDEX idx_servers_health ON mcp.servers(health_status);
CREATE INDEX idx_servers_public ON mcp.servers(is_public);
CREATE INDEX idx_servers_tags ON mcp.servers USING GIN(tags);

-- MCP server versions for change control
CREATE TABLE mcp.server_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    server_id UUID NOT NULL REFERENCES mcp.servers(id) ON DELETE CASCADE,
    version_number VARCHAR(50) NOT NULL,
    version_hash VARCHAR(64),
    changelog TEXT,
    deployment_notes TEXT,
    is_current BOOLEAN DEFAULT false,
    is_approved BOOLEAN DEFAULT false,
    approved_by UUID REFERENCES auth.users(id),
    approved_at TIMESTAMPTZ,
    deployed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    UNIQUE(server_id, version_number)
);

-- Add indexes for server_versions
CREATE INDEX idx_server_versions_server_id ON mcp.server_versions(server_id);
CREATE INDEX idx_server_versions_current ON mcp.server_versions(is_current);
CREATE INDEX idx_server_versions_approved ON mcp.server_versions(is_approved);

-- MCP server tools/capabilities registry
CREATE TABLE mcp.server_tools (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    server_id UUID NOT NULL REFERENCES mcp.servers(id) ON DELETE CASCADE,
    tool_name VARCHAR(255) NOT NULL,
    tool_description TEXT,
    input_schema JSONB,
    output_schema JSONB,
    required_permissions TEXT[] DEFAULT ARRAY[]::TEXT[],
    risk_level VARCHAR(20) DEFAULT 'medium',
    is_enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(server_id, tool_name)
);

-- Add indexes for server_tools
CREATE INDEX idx_server_tools_server_id ON mcp.server_tools(server_id);
CREATE INDEX idx_server_tools_enabled ON mcp.server_tools(is_enabled);
CREATE INDEX idx_server_tools_risk ON mcp.server_tools(risk_level);

-- MCP server access permissions
CREATE TABLE mcp.server_permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    server_id UUID NOT NULL REFERENCES mcp.servers(id) ON DELETE CASCADE,
    role_id UUID REFERENCES auth.roles(id) ON DELETE CASCADE,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    team_id UUID REFERENCES auth.teams(id) ON DELETE CASCADE,
    permission_type VARCHAR(50) NOT NULL, -- 'read', 'execute', 'admin'
    allowed_tools TEXT[] DEFAULT ARRAY[]::TEXT[], -- empty means all tools
    denied_tools TEXT[] DEFAULT ARRAY[]::TEXT[],
    conditions JSONB DEFAULT '{}', -- time restrictions, IP restrictions, etc.
    granted_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    granted_by UUID REFERENCES auth.users(id),
    expires_at TIMESTAMPTZ,
    is_active BOOLEAN DEFAULT true,
    CONSTRAINT chk_permission_target CHECK (
        (role_id IS NOT NULL AND user_id IS NULL AND team_id IS NULL) OR
        (role_id IS NULL AND user_id IS NOT NULL AND team_id IS NULL) OR
        (role_id IS NULL AND user_id IS NULL AND team_id IS NOT NULL)
    )
);

-- Add indexes for server_permissions
CREATE INDEX idx_server_permissions_server_id ON mcp.server_permissions(server_id);
CREATE INDEX idx_server_permissions_role_id ON mcp.server_permissions(role_id);
CREATE INDEX idx_server_permissions_user_id ON mcp.server_permissions(user_id);
CREATE INDEX idx_server_permissions_team_id ON mcp.server_permissions(team_id);
CREATE INDEX idx_server_permissions_type ON mcp.server_permissions(permission_type);
CREATE INDEX idx_server_permissions_active ON mcp.server_permissions(is_active);
