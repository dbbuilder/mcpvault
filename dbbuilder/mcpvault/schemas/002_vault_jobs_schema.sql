-- MCPVault Database Schema - Key Vault and Jobs
-- PostgreSQL Implementation for Enterprise MCP Compliance Platform
-- Author: MCPVault Development Team
-- Version: 1.0.0

-- ==================================================
-- KEY VAULT MANAGEMENT SCHEMA
-- ==================================================

-- Key vault providers configuration
CREATE TABLE vault.providers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    provider_name VARCHAR(50) NOT NULL, -- 'azure', 'aws', 'gcp'
    display_name VARCHAR(255) NOT NULL,
    configuration JSONB NOT NULL, -- encrypted connection details
    is_primary BOOLEAN DEFAULT false,
    is_enabled BOOLEAN DEFAULT true,
    last_sync_at TIMESTAMPTZ,
    sync_status VARCHAR(20) DEFAULT 'pending',
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id),
    UNIQUE(organization_id, provider_name)
);

-- Add indexes for providers
CREATE INDEX idx_providers_org_id ON vault.providers(organization_id);
CREATE INDEX idx_providers_enabled ON vault.providers(is_enabled);
CREATE INDEX idx_providers_primary ON vault.providers(is_primary);

-- Key vault secrets metadata (NO ACTUAL SECRETS STORED)
CREATE TABLE vault.secrets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    provider_id UUID NOT NULL REFERENCES vault.providers(id) ON DELETE CASCADE,
    secret_name VARCHAR(255) NOT NULL,
    secret_path VARCHAR(1024) NOT NULL, -- vault-specific path/identifier
    secret_type VARCHAR(50) NOT NULL, -- 'api_key', 'token', 'certificate', 'password'
    description TEXT,
    tags TEXT[] DEFAULT ARRAY[]::TEXT[],
    rotation_days INTEGER,
    last_rotated_at TIMESTAMPTZ,
    next_rotation_at TIMESTAMPTZ,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id),
    UNIQUE(organization_id, secret_name)
);

-- Add indexes for secrets
CREATE INDEX idx_secrets_org_id ON vault.secrets(organization_id);
CREATE INDEX idx_secrets_provider_id ON vault.secrets(provider_id);
CREATE INDEX idx_secrets_type ON vault.secrets(secret_type);
CREATE INDEX idx_secrets_active ON vault.secrets(is_active);
CREATE INDEX idx_secrets_rotation ON vault.secrets(next_rotation_at);
CREATE INDEX idx_secrets_tags ON vault.secrets USING GIN(tags);

-- Secret access permissions
CREATE TABLE vault.secret_permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    secret_id UUID NOT NULL REFERENCES vault.secrets(id) ON DELETE CASCADE,
    role_id UUID REFERENCES auth.roles(id) ON DELETE CASCADE,
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    team_id UUID REFERENCES auth.teams(id) ON DELETE CASCADE,
    permission_type VARCHAR(20) NOT NULL, -- 'read', 'rotate', 'admin'
    conditions JSONB DEFAULT '{}',
    granted_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    granted_by UUID REFERENCES auth.users(id),
    expires_at TIMESTAMPTZ,
    is_active BOOLEAN DEFAULT true,
    CONSTRAINT chk_secret_permission_target CHECK (
        (role_id IS NOT NULL AND user_id IS NULL AND team_id IS NULL) OR
        (role_id IS NULL AND user_id IS NOT NULL AND team_id IS NULL) OR
        (role_id IS NULL AND user_id IS NULL AND team_id IS NOT NULL)
    )
);

-- Add indexes for secret_permissions
CREATE INDEX idx_secret_permissions_secret_id ON vault.secret_permissions(secret_id);
CREATE INDEX idx_secret_permissions_role_id ON vault.secret_permissions(role_id);
CREATE INDEX idx_secret_permissions_user_id ON vault.secret_permissions(user_id);
CREATE INDEX idx_secret_permissions_team_id ON vault.secret_permissions(team_id);
CREATE INDEX idx_secret_permissions_active ON vault.secret_permissions(is_active);

-- Encrypted token cache for users (temporary storage)
CREATE TABLE vault.user_token_cache (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    secret_id UUID NOT NULL REFERENCES vault.secrets(id) ON DELETE CASCADE,
    encrypted_token TEXT NOT NULL, -- encrypted with user-specific key
    token_hash VARCHAR(64) NOT NULL, -- hash for verification
    expires_at TIMESTAMPTZ NOT NULL,
    last_used_at TIMESTAMPTZ,
    use_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, secret_id)
);

-- Add indexes for user_token_cache
CREATE INDEX idx_token_cache_user_id ON vault.user_token_cache(user_id);
CREATE INDEX idx_token_cache_secret_id ON vault.user_token_cache(secret_id);
CREATE INDEX idx_token_cache_expires ON vault.user_token_cache(expires_at);
CREATE INDEX idx_token_cache_hash ON vault.user_token_cache(token_hash);

-- ==================================================
-- JOB MANAGEMENT AND ORCHESTRATION SCHEMA
-- ==================================================

-- Job definitions and workflows
CREATE TABLE jobs.job_definitions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    description TEXT,
    workflow_definition JSONB NOT NULL, -- workflow steps and configuration
    input_schema JSONB,
    output_schema JSONB,
    timeout_minutes INTEGER DEFAULT 60,
    retry_count INTEGER DEFAULT 3,
    retry_delay_seconds INTEGER DEFAULT 30,
    priority INTEGER DEFAULT 5, -- 1-10, higher is higher priority
    tags TEXT[] DEFAULT ARRAY[]::TEXT[],
    is_template BOOLEAN DEFAULT false,
    is_enabled BOOLEAN DEFAULT true,
    version INTEGER DEFAULT 1,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id),
    UNIQUE(organization_id, name, version)
);

-- Add indexes for job_definitions
CREATE INDEX idx_job_definitions_org_id ON jobs.job_definitions(organization_id);
CREATE INDEX idx_job_definitions_enabled ON jobs.job_definitions(is_enabled);
CREATE INDEX idx_job_definitions_template ON jobs.job_definitions(is_template);
CREATE INDEX idx_job_definitions_tags ON jobs.job_definitions USING GIN(tags);

-- Job executions
CREATE TABLE jobs.job_executions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_definition_id UUID NOT NULL REFERENCES jobs.job_definitions(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    execution_name VARCHAR(255),
    input_data JSONB,
    output_data JSONB,
    error_data JSONB,
    status VARCHAR(20) DEFAULT 'pending', -- 'pending', 'running', 'completed', 'failed', 'cancelled'
    progress_percentage INTEGER DEFAULT 0,
    progress_message TEXT,
    current_step VARCHAR(255),
    step_count INTEGER DEFAULT 0,
    completed_steps INTEGER DEFAULT 0,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_seconds INTEGER,
    retry_count INTEGER DEFAULT 0,
    parent_execution_id UUID REFERENCES jobs.job_executions(id),
    correlation_id UUID,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for job_executions
CREATE INDEX idx_job_executions_definition_id ON jobs.job_executions(job_definition_id);
CREATE INDEX idx_job_executions_user_id ON jobs.job_executions(user_id);
CREATE INDEX idx_job_executions_status ON jobs.job_executions(status);
CREATE INDEX idx_job_executions_created ON jobs.job_executions(created_at);
CREATE INDEX idx_job_executions_parent ON jobs.job_executions(parent_execution_id);
CREATE INDEX idx_job_executions_correlation ON jobs.job_executions(correlation_id);

-- Job execution steps (for detailed progress tracking)
CREATE TABLE jobs.job_execution_steps (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_execution_id UUID NOT NULL REFERENCES jobs.job_executions(id) ON DELETE CASCADE,
    step_name VARCHAR(255) NOT NULL,
    step_type VARCHAR(50) NOT NULL, -- 'mcp_call', 'transform', 'condition', 'loop'
    step_order INTEGER NOT NULL,
    server_id UUID REFERENCES mcp.servers(id),
    tool_name VARCHAR(255),
    input_data JSONB,
    output_data JSONB,
    error_data JSONB,
    status VARCHAR(20) DEFAULT 'pending',
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_seconds INTEGER,
    retry_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for job_execution_steps
CREATE INDEX idx_job_execution_steps_execution_id ON jobs.job_execution_steps(job_execution_id);
CREATE INDEX idx_job_execution_steps_server_id ON jobs.job_execution_steps(server_id);
CREATE INDEX idx_job_execution_steps_status ON jobs.job_execution_steps(status);
CREATE INDEX idx_job_execution_steps_order ON jobs.job_execution_steps(step_order);

-- Job schedules for recurring jobs
CREATE TABLE jobs.job_schedules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_definition_id UUID NOT NULL REFERENCES jobs.job_definitions(id) ON DELETE CASCADE,
    schedule_name VARCHAR(255) NOT NULL,
    cron_expression VARCHAR(100) NOT NULL,
    timezone VARCHAR(50) DEFAULT 'UTC',
    input_data JSONB,
    is_enabled BOOLEAN DEFAULT true,
    last_run_at TIMESTAMPTZ,
    next_run_at TIMESTAMPTZ,
    run_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id)
);

-- Add indexes for job_schedules
CREATE INDEX idx_job_schedules_definition_id ON jobs.job_schedules(job_definition_id);
CREATE INDEX idx_job_schedules_enabled ON jobs.job_schedules(is_enabled);
CREATE INDEX idx_job_schedules_next_run ON jobs.job_schedules(next_run_at);

-- Job queue for managing execution order
CREATE TABLE jobs.job_queue (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_execution_id UUID NOT NULL REFERENCES jobs.job_executions(id) ON DELETE CASCADE,
    priority INTEGER DEFAULT 5,
    scheduled_for TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    picked_up_at TIMESTAMPTZ,
    picked_up_by VARCHAR(100), -- worker identifier
    status VARCHAR(20) DEFAULT 'queued', -- 'queued', 'processing', 'completed', 'failed'
    retry_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for job_queue
CREATE INDEX idx_job_queue_execution_id ON jobs.job_queue(job_execution_id);
CREATE INDEX idx_job_queue_status ON jobs.job_queue(status);
CREATE INDEX idx_job_queue_scheduled ON jobs.job_queue(scheduled_for);
CREATE INDEX idx_job_queue_priority ON jobs.job_queue(priority DESC, scheduled_for ASC);

-- ==================================================
-- AUDIT AND COMPLIANCE SCHEMA
-- ==================================================

-- Comprehensive audit log
CREATE TABLE audit.audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID REFERENCES auth.organizations(id) ON DELETE CASCADE,
    user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL,
    session_id UUID REFERENCES auth.sessions(id) ON DELETE SET NULL,
    event_type VARCHAR(100) NOT NULL, -- 'login', 'mcp_call', 'secret_access', 'permission_change'
    event_category VARCHAR(50) NOT NULL, -- 'authentication', 'authorization', 'data_access', 'configuration'
    resource_type VARCHAR(100), -- 'server', 'secret', 'user', 'role'
    resource_id UUID,
    action VARCHAR(100) NOT NULL, -- 'create', 'read', 'update', 'delete', 'execute'
    details JSONB DEFAULT '{}',
    ip_address INET,
    user_agent TEXT,
    risk_score INTEGER, -- 1-10, calculated risk score
    compliance_tags TEXT[] DEFAULT ARRAY[]::TEXT[],
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for audit_logs
CREATE INDEX idx_audit_logs_org_id ON audit.audit_logs(organization_id);
CREATE INDEX idx_audit_logs_user_id ON audit.audit_logs(user_id);
CREATE INDEX idx_audit_logs_event_type ON audit.audit_logs(event_type);
CREATE INDEX idx_audit_logs_event_category ON audit.audit_logs(event_category);
CREATE INDEX idx_audit_logs_resource ON audit.audit_logs(resource_type, resource_id);
CREATE INDEX idx_audit_logs_created ON audit.audit_logs(created_at);
CREATE INDEX idx_audit_logs_risk ON audit.audit_logs(risk_score);
CREATE INDEX idx_audit_logs_compliance ON audit.audit_logs USING GIN(compliance_tags);

-- Compliance policies
CREATE TABLE audit.compliance_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES auth.organizations(id) ON DELETE CASCADE,
    policy_name VARCHAR(255) NOT NULL,
    policy_type VARCHAR(50) NOT NULL, -- 'data_retention', 'access_control', 'encryption'
    framework VARCHAR(50) NOT NULL, -- 'SOC2', 'HIPAA', 'GDPR', 'PCI'
    description TEXT,
    policy_rules JSONB NOT NULL,
    enforcement_level VARCHAR(20) DEFAULT 'warn', -- 'block', 'warn', 'log'
    is_enabled BOOLEAN DEFAULT true,
    last_evaluated_at TIMESTAMPTZ,
    violations_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by UUID REFERENCES auth.users(id),
    updated_by UUID REFERENCES auth.users(id),
    UNIQUE(organization_id, policy_name)
);

-- Add indexes for compliance_policies
CREATE INDEX idx_compliance_policies_org_id ON audit.compliance_policies(organization_id);
CREATE INDEX idx_compliance_policies_framework ON audit.compliance_policies(framework);
CREATE INDEX idx_compliance_policies_enabled ON audit.compliance_policies(is_enabled);
CREATE INDEX idx_compliance_policies_type ON audit.compliance_policies(policy_type);

-- Policy violations
CREATE TABLE audit.policy_violations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    policy_id UUID NOT NULL REFERENCES audit.compliance_policies(id) ON DELETE CASCADE,
    audit_log_id UUID REFERENCES audit.audit_logs(id) ON DELETE CASCADE,
    violation_type VARCHAR(100) NOT NULL,
    severity VARCHAR(20) NOT NULL, -- 'critical', 'high', 'medium', 'low'
    description TEXT,
    remediation_action VARCHAR(100),
    status VARCHAR(20) DEFAULT 'open', -- 'open', 'acknowledged', 'resolved', 'false_positive'
    resolved_at TIMESTAMPTZ,
    resolved_by UUID REFERENCES auth.users(id),
    resolution_notes TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for policy_violations
CREATE INDEX idx_policy_violations_policy_id ON audit.policy_violations(policy_id);
CREATE INDEX idx_policy_violations_audit_log_id ON audit.policy_violations(audit_log_id);
CREATE INDEX idx_policy_violations_severity ON audit.policy_violations(severity);
CREATE INDEX idx_policy_violations_status ON audit.policy_violations(status);
CREATE INDEX idx_policy_violations_created ON audit.policy_violations(created_at);

-- ==================================================
-- PERFORMANCE AND MONITORING SCHEMA
-- ==================================================

-- System metrics for monitoring
CREATE TABLE public.system_metrics (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    metric_name VARCHAR(100) NOT NULL,
    metric_type VARCHAR(50) NOT NULL, -- 'counter', 'gauge', 'histogram'
    metric_value DECIMAL(15,2),
    labels JSONB DEFAULT '{}',
    recorded_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add indexes for system_metrics
CREATE INDEX idx_system_metrics_name ON public.system_metrics(metric_name);
CREATE INDEX idx_system_metrics_recorded ON public.system_metrics(recorded_at);

-- Data retention table for automated cleanup
CREATE TABLE public.data_retention_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    table_name VARCHAR(100) NOT NULL,
    schema_name VARCHAR(100) DEFAULT 'public',
    retention_days INTEGER NOT NULL,
    date_column VARCHAR(100) DEFAULT 'created_at',
    is_enabled BOOLEAN DEFAULT true,
    last_cleanup_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(schema_name, table_name)
);

-- Add default retention policies
INSERT INTO public.data_retention_policies (table_name, schema_name, retention_days) VALUES
('audit_logs', 'audit', 2555), -- 7 years for compliance
('sessions', 'auth', 90),
('user_token_cache', 'vault', 1),
('job_executions', 'jobs', 365),
('job_execution_steps', 'jobs', 365),
('system_metrics', 'public', 90),
('policy_violations', 'audit', 2555); -- 7 years for compliance

-- Add triggers for updating timestamps
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Apply triggers to tables with updated_at columns
CREATE TRIGGER update_organizations_updated_at BEFORE UPDATE ON auth.organizations FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_roles_updated_at BEFORE UPDATE ON auth.roles FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_teams_updated_at BEFORE UPDATE ON auth.teams FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON auth.users FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_servers_updated_at BEFORE UPDATE ON mcp.servers FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_server_tools_updated_at BEFORE UPDATE ON mcp.server_tools FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_providers_updated_at BEFORE UPDATE ON vault.providers FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_secrets_updated_at BEFORE UPDATE ON vault.secrets FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_job_definitions_updated_at BEFORE UPDATE ON jobs.job_definitions FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_job_executions_updated_at BEFORE UPDATE ON jobs.job_executions FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_job_execution_steps_updated_at BEFORE UPDATE ON jobs.job_execution_steps FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_job_schedules_updated_at BEFORE UPDATE ON jobs.job_schedules FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_job_queue_updated_at BEFORE UPDATE ON jobs.job_queue FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_compliance_policies_updated_at BEFORE UPDATE ON audit.compliance_policies FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Print completion message
DO $$
BEGIN
    RAISE NOTICE 'MCPVault database schema created successfully';
    RAISE NOTICE 'Schemas: auth, mcp, vault, audit, jobs, public';
    RAISE NOTICE 'Total tables: %', (
        SELECT count(*) 
        FROM information_schema.tables 
        WHERE table_schema IN ('auth', 'mcp', 'vault', 'audit', 'jobs', 'public')
        AND table_type = 'BASE TABLE'
    );
END $$;
