-- MCPVault Database Stored Procedures - Authentication and User Management
-- PostgreSQL Implementation for Enterprise MCP Compliance Platform
-- Author: MCPVault Development Team
-- Version: 1.0.0

-- ==================================================
-- USER AUTHENTICATION PROCEDURES
-- ==================================================

-- Create or update user procedure
CREATE OR REPLACE FUNCTION auth.upsert_user(
    p_organization_id UUID,
    p_email VARCHAR(255),
    p_first_name VARCHAR(100),
    p_last_name VARCHAR(100),
    p_password_hash VARCHAR(255) DEFAULT NULL,
    p_phone VARCHAR(50) DEFAULT NULL,
    p_timezone VARCHAR(50) DEFAULT 'UTC',
    p_locale VARCHAR(10) DEFAULT 'en_US',
    p_created_by UUID DEFAULT NULL
)
RETURNS TABLE(
    user_id UUID,
    success BOOLEAN,
    message TEXT
) 
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_user_id UUID;
    v_existing_user_id UUID;
BEGIN
    -- Check if organization exists and is active
    IF NOT EXISTS (
        SELECT 1 FROM auth.organizations 
        WHERE id = p_organization_id AND is_active = true
    ) THEN
        RETURN QUERY SELECT NULL::UUID, false, 'Organization not found or inactive';
        RETURN;
    END IF;

    -- Check if user already exists
    SELECT id INTO v_existing_user_id
    FROM auth.users
    WHERE email = p_email;

    IF v_existing_user_id IS NOT NULL THEN
        -- Update existing user
        UPDATE auth.users
        SET 
            organization_id = p_organization_id,
            first_name = p_first_name,
            last_name = p_last_name,
            password_hash = COALESCE(p_password_hash, password_hash),
            phone = p_phone,
            timezone = p_timezone,
            locale = p_locale,
            updated_by = p_created_by,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_existing_user_id;
        
        v_user_id := v_existing_user_id;
    ELSE
        -- Create new user
        INSERT INTO auth.users (
            organization_id, email, first_name, last_name, password_hash,
            phone, timezone, locale, created_by
        ) VALUES (
            p_organization_id, p_email, p_first_name, p_last_name, p_password_hash,
            p_phone, p_timezone, p_locale, p_created_by
        ) RETURNING id INTO v_user_id;
    END IF;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        p_organization_id, p_created_by, 'user_management', 'configuration',
        'user', v_user_id, CASE WHEN v_existing_user_id IS NOT NULL THEN 'update' ELSE 'create' END,
        jsonb_build_object(
            'email', p_email,
            'action_type', CASE WHEN v_existing_user_id IS NOT NULL THEN 'update' ELSE 'create' END
        )
    );

    RETURN QUERY SELECT v_user_id, true, 'User created/updated successfully';
END;
$$;

-- Authenticate user procedure
CREATE OR REPLACE FUNCTION auth.authenticate_user(
    p_email VARCHAR(255),
    p_password_hash VARCHAR(255),
    p_ip_address INET DEFAULT NULL,
    p_user_agent TEXT DEFAULT NULL
)
RETURNS TABLE(
    user_id UUID,
    organization_id UUID,
    session_token VARCHAR(255),
    success BOOLEAN,
    message TEXT,
    requires_mfa BOOLEAN
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_user_record RECORD;
    v_session_token VARCHAR(255);
    v_refresh_token VARCHAR(255);
    v_session_id UUID;
    v_lockout_minutes INTEGER := 30;
    v_max_failed_attempts INTEGER := 5;
BEGIN
    -- Get user record
    SELECT u.*, o.is_active as org_active 
    INTO v_user_record
    FROM auth.users u
    JOIN auth.organizations o ON u.organization_id = o.id
    WHERE u.email = p_email;

    -- Check if user exists
    IF v_user_record.id IS NULL THEN
        -- Log failed attempt
        INSERT INTO audit.audit_logs (
            event_type, event_category, action, details, ip_address, user_agent
        ) VALUES (
            'authentication', 'authentication', 'login_failed',
            jsonb_build_object('email', p_email, 'reason', 'user_not_found'),
            p_ip_address, p_user_agent
        );
        
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, NULL::VARCHAR, false, 'Invalid credentials', false;
        RETURN;
    END IF;

    -- Check if user is active
    IF NOT v_user_record.is_active THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, NULL::VARCHAR, false, 'Account is inactive', false;
        RETURN;
    END IF;

    -- Check if organization is active
    IF NOT v_user_record.org_active THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, NULL::VARCHAR, false, 'Organization is inactive', false;
        RETURN;
    END IF;

    -- Check if account is locked
    IF v_user_record.locked_until IS NOT NULL AND v_user_record.locked_until > CURRENT_TIMESTAMP THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, NULL::VARCHAR, false, 'Account is temporarily locked', false;
        RETURN;
    END IF;

    -- Verify password
    IF v_user_record.password_hash != p_password_hash THEN
        -- Increment failed attempts
        UPDATE auth.users
        SET 
            failed_login_attempts = failed_login_attempts + 1,
            locked_until = CASE 
                WHEN failed_login_attempts + 1 >= v_max_failed_attempts 
                THEN CURRENT_TIMESTAMP + INTERVAL '1 minute' * v_lockout_minutes
                ELSE NULL
            END
        WHERE id = v_user_record.id;

        -- Log failed attempt
        INSERT INTO audit.audit_logs (
            organization_id, user_id, event_type, event_category, action, details, ip_address, user_agent
        ) VALUES (
            v_user_record.organization_id, v_user_record.id, 'authentication', 'authentication', 'login_failed',
            jsonb_build_object('email', p_email, 'reason', 'invalid_password', 'failed_attempts', v_user_record.failed_login_attempts + 1),
            p_ip_address, p_user_agent
        );

        RETURN QUERY SELECT NULL::UUID, NULL::UUID, NULL::VARCHAR, false, 'Invalid credentials', false;
        RETURN;
    END IF;

    -- Check if MFA is required
    IF v_user_record.mfa_enabled THEN
        RETURN QUERY SELECT v_user_record.id, v_user_record.organization_id, NULL::VARCHAR, true, 'MFA required', true;
        RETURN;
    END IF;

    -- Generate session tokens
    v_session_token := encode(gen_random_bytes(32), 'base64');
    v_refresh_token := encode(gen_random_bytes(32), 'base64');

    -- Create session
    INSERT INTO auth.sessions (
        user_id, session_token, refresh_token, ip_address, user_agent, 
        expires_at, last_activity_at
    ) VALUES (
        v_user_record.id, v_session_token, v_refresh_token, p_ip_address, p_user_agent,
        CURRENT_TIMESTAMP + INTERVAL '8 hours', CURRENT_TIMESTAMP
    ) RETURNING id INTO v_session_id;

    -- Reset failed login attempts and update last login
    UPDATE auth.users
    SET 
        failed_login_attempts = 0,
        locked_until = NULL,
        last_login_at = CURRENT_TIMESTAMP,
        last_login_ip = p_ip_address
    WHERE id = v_user_record.id;

    -- Log successful login
    INSERT INTO audit.audit_logs (
        organization_id, user_id, session_id, event_type, event_category, action, details, ip_address, user_agent
    ) VALUES (
        v_user_record.organization_id, v_user_record.id, v_session_id, 'authentication', 'authentication', 'login_success',
        jsonb_build_object('email', p_email, 'session_id', v_session_id),
        p_ip_address, p_user_agent
    );

    RETURN QUERY SELECT v_user_record.id, v_user_record.organization_id, v_session_token, true, 'Login successful', false;
END;
$$;

-- Validate session procedure
CREATE OR REPLACE FUNCTION auth.validate_session(
    p_session_token VARCHAR(255),
    p_ip_address INET DEFAULT NULL,
    p_user_agent TEXT DEFAULT NULL
)
RETURNS TABLE(
    user_id UUID,
    organization_id UUID,
    session_valid BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_session_record RECORD;
BEGIN
    -- Get session with user info
    SELECT 
        s.id as session_id, s.user_id, s.expires_at, s.is_active,
        u.organization_id, u.is_active as user_active,
        o.is_active as org_active
    INTO v_session_record
    FROM auth.sessions s
    JOIN auth.users u ON s.user_id = u.id
    JOIN auth.organizations o ON u.organization_id = o.id
    WHERE s.session_token = p_session_token;

    -- Check if session exists
    IF v_session_record.session_id IS NULL THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, false, 'Invalid session token';
        RETURN;
    END IF;

    -- Check if session is active
    IF NOT v_session_record.is_active THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, false, 'Session is inactive';
        RETURN;
    END IF;

    -- Check if session has expired
    IF v_session_record.expires_at < CURRENT_TIMESTAMP THEN
        -- Mark session as inactive
        UPDATE auth.sessions
        SET is_active = false
        WHERE id = v_session_record.session_id;

        RETURN QUERY SELECT NULL::UUID, NULL::UUID, false, 'Session has expired';
        RETURN;
    END IF;

    -- Check if user is active
    IF NOT v_session_record.user_active THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, false, 'User account is inactive';
        RETURN;
    END IF;

    -- Check if organization is active
    IF NOT v_session_record.org_active THEN
        RETURN QUERY SELECT NULL::UUID, NULL::UUID, false, 'Organization is inactive';
        RETURN;
    END IF;

    -- Update last activity
    UPDATE auth.sessions
    SET last_activity_at = CURRENT_TIMESTAMP
    WHERE id = v_session_record.session_id;

    RETURN QUERY SELECT v_session_record.user_id, v_session_record.organization_id, true, 'Session is valid';
END;
$$;

-- Logout user procedure
CREATE OR REPLACE FUNCTION auth.logout_user(
    p_session_token VARCHAR(255),
    p_ip_address INET DEFAULT NULL,
    p_user_agent TEXT DEFAULT NULL
)
RETURNS TABLE(
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_session_record RECORD;
BEGIN
    -- Get session info
    SELECT s.id, s.user_id, u.organization_id
    INTO v_session_record
    FROM auth.sessions s
    JOIN auth.users u ON s.user_id = u.id
    WHERE s.session_token = p_session_token AND s.is_active = true;

    IF v_session_record.id IS NULL THEN
        RETURN QUERY SELECT false, 'Invalid session token';
        RETURN;
    END IF;

    -- Deactivate session
    UPDATE auth.sessions
    SET is_active = false
    WHERE id = v_session_record.id;

    -- Log logout
    INSERT INTO audit.audit_logs (
        organization_id, user_id, session_id, event_type, event_category, action, details, ip_address, user_agent
    ) VALUES (
        v_session_record.organization_id, v_session_record.user_id, v_session_record.id, 
        'authentication', 'authentication', 'logout',
        jsonb_build_object('session_id', v_session_record.id),
        p_ip_address, p_user_agent
    );

    RETURN QUERY SELECT true, 'Logout successful';
END;
$$;

-- ==================================================
-- ROLE AND PERMISSION PROCEDURES
-- ==================================================

-- Get user permissions procedure
CREATE OR REPLACE FUNCTION auth.get_user_permissions(
    p_user_id UUID,
    p_organization_id UUID
)
RETURNS TABLE(
    permission_type VARCHAR(100),
    resource_type VARCHAR(100),
    resource_id UUID,
    conditions JSONB
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    -- Return permissions from roles and direct assignments
    RETURN QUERY
    WITH user_effective_roles AS (
        -- Direct role assignments
        SELECT r.id as role_id, r.permissions
        FROM auth.user_roles ur
        JOIN auth.roles r ON ur.role_id = r.id
        WHERE ur.user_id = p_user_id 
        AND ur.is_active = true
        AND (ur.expires_at IS NULL OR ur.expires_at > CURRENT_TIMESTAMP)
        AND r.is_active = true
        AND r.organization_id = p_organization_id
        
        UNION
        
        -- Team-based role assignments
        SELECT r.id as role_id, r.permissions
        FROM auth.user_teams ut
        JOIN auth.teams t ON ut.team_id = t.id
        JOIN auth.roles r ON r.name = ut.role_in_team
        WHERE ut.user_id = p_user_id
        AND ut.is_active = true
        AND t.is_active = true
        AND r.is_active = true
        AND t.organization_id = p_organization_id
        AND r.organization_id = p_organization_id
    )
    SELECT 
        (perm.value->>'type')::VARCHAR(100) as permission_type,
        (perm.value->>'resource_type')::VARCHAR(100) as resource_type,
        (perm.value->>'resource_id')::UUID as resource_id,
        (perm.value->'conditions')::JSONB as conditions
    FROM user_effective_roles uer
    CROSS JOIN LATERAL jsonb_array_elements(uer.permissions->'permissions') as perm;
END;
$$;

-- Assign role to user procedure
CREATE OR REPLACE FUNCTION auth.assign_user_role(
    p_user_id UUID,
    p_role_id UUID,
    p_granted_by UUID,
    p_expires_at TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE(
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_user_org_id UUID;
    v_role_org_id UUID;
BEGIN
    -- Verify user and role belong to same organization
    SELECT organization_id INTO v_user_org_id FROM auth.users WHERE id = p_user_id;
    SELECT organization_id INTO v_role_org_id FROM auth.roles WHERE id = p_role_id;

    IF v_user_org_id IS NULL THEN
        RETURN QUERY SELECT false, 'User not found';
        RETURN;
    END IF;

    IF v_role_org_id IS NULL THEN
        RETURN QUERY SELECT false, 'Role not found';
        RETURN;
    END IF;

    IF v_user_org_id != v_role_org_id THEN
        RETURN QUERY SELECT false, 'User and role must belong to the same organization';
        RETURN;
    END IF;

    -- Insert or update role assignment
    INSERT INTO auth.user_roles (user_id, role_id, granted_by, expires_at)
    VALUES (p_user_id, p_role_id, p_granted_by, p_expires_at)
    ON CONFLICT (user_id, role_id)
    DO UPDATE SET
        granted_by = p_granted_by,
        expires_at = p_expires_at,
        is_active = true,
        granted_at = CURRENT_TIMESTAMP;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        v_user_org_id, p_granted_by, 'role_assignment', 'authorization',
        'user_role', p_user_id, 'assign',
        jsonb_build_object(
            'target_user_id', p_user_id,
            'role_id', p_role_id,
            'expires_at', p_expires_at
        )
    );

    RETURN QUERY SELECT true, 'Role assigned successfully';
END;
$$;

-- Check user permission procedure
CREATE OR REPLACE FUNCTION auth.check_user_permission(
    p_user_id UUID,
    p_organization_id UUID,
    p_permission_type VARCHAR(100),
    p_resource_type VARCHAR(100) DEFAULT NULL,
    p_resource_id UUID DEFAULT NULL
)
RETURNS BOOLEAN
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_has_permission BOOLEAN := false;
BEGIN
    -- Check if user has the specific permission
    SELECT EXISTS(
        SELECT 1 FROM auth.get_user_permissions(p_user_id, p_organization_id) up
        WHERE up.permission_type = p_permission_type
        AND (p_resource_type IS NULL OR up.resource_type = p_resource_type)
        AND (p_resource_id IS NULL OR up.resource_id = p_resource_id OR up.resource_id IS NULL)
    ) INTO v_has_permission;

    -- Log permission check for audit
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category, action, details
    ) VALUES (
        p_organization_id, p_user_id, 'permission_check', 'authorization', 'check',
        jsonb_build_object(
            'permission_type', p_permission_type,
            'resource_type', p_resource_type,
            'resource_id', p_resource_id,
            'result', v_has_permission
        )
    );

    RETURN v_has_permission;
END;
$$;

-- Print success message
DO $$
BEGIN
    RAISE NOTICE 'Authentication and user management procedures created successfully';
    RAISE NOTICE 'Procedures: upsert_user, authenticate_user, validate_session, logout_user';
    RAISE NOTICE 'Procedures: get_user_permissions, assign_user_role, check_user_permission';
END $$;        CASE 
            WHEN usp.allowed_tools = ARRAY[]::TEXT[] OR usp.allowed_tools IS NULL THEN
                COALESCE(
                    ARRAY(
                        SELECT unnest(st.all_tools) 
                        EXCEPT 
                        SELECT unnest(COALESCE(usp.denied_tools, ARRAY[]::TEXT[]))
                    ),
                    ARRAY[]::TEXT[]
                )
            ELSE
                ARRAY(
                    SELECT unnest(usp.allowed_tools) 
                    EXCEPT 
                    SELECT unnest(COALESCE(usp.denied_tools, ARRAY[]::TEXT[]))
                )
        END as accessible_tools,
        usp.permission_type
    FROM mcp.servers s
    LEFT JOIN user_server_permissions usp ON s.id = usp.server_id
    LEFT JOIN server_tools st ON s.id = st.server_id
    WHERE s.organization_id = p_organization_id
    AND (s.is_public = true OR usp.server_id IS NOT NULL)
    AND (p_server_type IS NULL OR s.server_type = p_server_type)
    AND (p_tags IS NULL OR s.tags && p_tags)
    ORDER BY s.display_name;
END;
$$;

-- Grant server permission procedure
CREATE OR REPLACE FUNCTION mcp.grant_server_permission(
    p_server_id UUID,
    p_role_id UUID DEFAULT NULL,
    p_user_id UUID DEFAULT NULL,
    p_team_id UUID DEFAULT NULL,
    p_permission_type VARCHAR(50),
    p_allowed_tools TEXT[] DEFAULT ARRAY[]::TEXT[],
    p_denied_tools TEXT[] DEFAULT ARRAY[]::TEXT[],
    p_conditions JSONB DEFAULT '{}',
    p_expires_at TIMESTAMPTZ DEFAULT NULL,
    p_granted_by UUID
)
RETURNS TABLE(
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_server_org_id UUID;
    v_target_org_id UUID;
    v_permission_id UUID;
BEGIN
    -- Validate exactly one target is specified
    IF (p_role_id IS NOT NULL)::INTEGER + (p_user_id IS NOT NULL)::INTEGER + (p_team_id IS NOT NULL)::INTEGER != 1 THEN
        RETURN QUERY SELECT false, 'Exactly one of role_id, user_id, or team_id must be specified';
        RETURN;
    END IF;

    -- Get server organization
    SELECT organization_id INTO v_server_org_id
    FROM mcp.servers
    WHERE id = p_server_id;

    IF v_server_org_id IS NULL THEN
        RETURN QUERY SELECT false, 'Server not found';
        RETURN;
    END IF;

    -- Get target organization and validate it matches server organization
    IF p_role_id IS NOT NULL THEN
        SELECT organization_id INTO v_target_org_id FROM auth.roles WHERE id = p_role_id;
    ELSIF p_user_id IS NOT NULL THEN
        SELECT organization_id INTO v_target_org_id FROM auth.users WHERE id = p_user_id;
    ELSIF p_team_id IS NOT NULL THEN
        SELECT organization_id INTO v_target_org_id FROM auth.teams WHERE id = p_team_id;
    END IF;

    IF v_target_org_id IS NULL THEN
        RETURN QUERY SELECT false, 'Target role/user/team not found';
        RETURN;
    END IF;

    IF v_server_org_id != v_target_org_id THEN
        RETURN QUERY SELECT false, 'Server and target must belong to the same organization';
        RETURN;
    END IF;

    -- Insert or update permission
    INSERT INTO mcp.server_permissions (
        server_id, role_id, user_id, team_id, permission_type,
        allowed_tools, denied_tools, conditions, expires_at, granted_by
    ) VALUES (
        p_server_id, p_role_id, p_user_id, p_team_id, p_permission_type,
        p_allowed_tools, p_denied_tools, p_conditions, p_expires_at, p_granted_by
    ) RETURNING id INTO v_permission_id;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        v_server_org_id, p_granted_by, 'permission_grant', 'authorization',
        'server_permission', v_permission_id, 'create',
        jsonb_build_object(
            'server_id', p_server_id,
            'role_id', p_role_id,
            'user_id', p_user_id,
            'team_id', p_team_id,
            'permission_type', p_permission_type,
            'allowed_tools', p_allowed_tools,
            'denied_tools', p_denied_tools
        )
    );

    RETURN QUERY SELECT true, 'Server permission granted successfully';
END;
$$;

-- Register server tool procedure
CREATE OR REPLACE FUNCTION mcp.register_server_tool(
    p_server_id UUID,
    p_tool_name VARCHAR(255),
    p_tool_description TEXT,
    p_input_schema JSONB DEFAULT NULL,
    p_output_schema JSONB DEFAULT NULL,
    p_required_permissions TEXT[] DEFAULT ARRAY[]::TEXT[],
    p_risk_level VARCHAR(20) DEFAULT 'medium'
)
RETURNS TABLE(
    tool_id UUID,
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_tool_id UUID;
    v_server_org_id UUID;
BEGIN
    -- Get server organization
    SELECT organization_id INTO v_server_org_id
    FROM mcp.servers
    WHERE id = p_server_id;

    IF v_server_org_id IS NULL THEN
        RETURN QUERY SELECT NULL::UUID, false, 'Server not found';
        RETURN;
    END IF;

    -- Insert or update tool
    INSERT INTO mcp.server_tools (
        server_id, tool_name, tool_description, input_schema, output_schema,
        required_permissions, risk_level
    ) VALUES (
        p_server_id, p_tool_name, p_tool_description, p_input_schema, p_output_schema,
        p_required_permissions, p_risk_level
    ) 
    ON CONFLICT (server_id, tool_name)
    DO UPDATE SET
        tool_description = p_tool_description,
        input_schema = p_input_schema,
        output_schema = p_output_schema,
        required_permissions = p_required_permissions,
        risk_level = p_risk_level,
        updated_at = CURRENT_TIMESTAMP
    RETURNING id INTO v_tool_id;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        v_server_org_id, 'tool_registration', 'configuration',
        'server_tool', v_tool_id, 'upsert',
        jsonb_build_object(
            'server_id', p_server_id,
            'tool_name', p_tool_name,
            'risk_level', p_risk_level
        )
    );

    RETURN QUERY SELECT v_tool_id, true, 'Server tool registered successfully';
END;
$$;

-- Check user server access procedure
CREATE OR REPLACE FUNCTION mcp.check_user_server_access(
    p_user_id UUID,
    p_server_id UUID,
    p_tool_name VARCHAR(255) DEFAULT NULL,
    p_permission_type VARCHAR(50) DEFAULT 'execute'
)
RETURNS TABLE(
    has_access BOOLEAN,
    permission_type VARCHAR(50),
    allowed_tools TEXT[],
    denied_tools TEXT[]
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_server_org_id UUID;
    v_user_org_id UUID;
    v_result RECORD;
BEGIN
    -- Get server and user organizations
    SELECT organization_id INTO v_server_org_id FROM mcp.servers WHERE id = p_server_id;
    SELECT organization_id INTO v_user_org_id FROM auth.users WHERE id = p_user_id;

    IF v_server_org_id IS NULL OR v_user_org_id IS NULL THEN
        RETURN QUERY SELECT false, NULL::VARCHAR, NULL::TEXT[], NULL::TEXT[];
        RETURN;
    END IF;

    IF v_server_org_id != v_user_org_id THEN
        RETURN QUERY SELECT false, NULL::VARCHAR, NULL::TEXT[], NULL::TEXT[];
        RETURN;
    END IF;

    -- Check for highest permission level available to user
    SELECT 
        sp.permission_type,
        sp.allowed_tools,
        sp.denied_tools,
        CASE sp.permission_type
            WHEN 'admin' THEN 3
            WHEN 'execute' THEN 2
            WHEN 'read' THEN 1
            ELSE 0
        END as permission_level
    INTO v_result
    FROM mcp.server_permissions sp
    WHERE sp.server_id = p_server_id
    AND sp.is_active = true
    AND (sp.expires_at IS NULL OR sp.expires_at > CURRENT_TIMESTAMP)
    AND (
        -- Direct user permission
        sp.user_id = p_user_id
        OR
        -- Role-based permission
        sp.role_id IN (
            SELECT ur.role_id 
            FROM auth.user_roles ur 
            WHERE ur.user_id = p_user_id 
            AND ur.is_active = true
            AND (ur.expires_at IS NULL OR ur.expires_at > CURRENT_TIMESTAMP)
        )
        OR
        -- Team-based permission
        sp.team_id IN (
            SELECT ut.team_id 
            FROM auth.user_teams ut 
            WHERE ut.user_id = p_user_id 
            AND ut.is_active = true
        )
    )
    ORDER BY permission_level DESC
    LIMIT 1;

    -- Check if user has required permission level
    IF v_result.permission_type IS NULL THEN
        -- Check if server is public
        IF EXISTS (SELECT 1 FROM mcp.servers WHERE id = p_server_id AND is_public = true) THEN
            RETURN QUERY SELECT true, 'read'::VARCHAR, ARRAY[]::TEXT[], ARRAY[]::TEXT[];
        ELSE
            RETURN QUERY SELECT false, NULL::VARCHAR, NULL::TEXT[], NULL::TEXT[];
        END IF;
        RETURN;
    END IF;

    -- Check permission hierarchy (admin > execute > read)
    IF (p_permission_type = 'admin' AND v_result.permission_type != 'admin') OR
       (p_permission_type = 'execute' AND v_result.permission_type NOT IN ('admin', 'execute')) THEN
        RETURN QUERY SELECT false, v_result.permission_type, v_result.allowed_tools, v_result.denied_tools;
        RETURN;
    END IF;

    -- Check tool-specific access if tool specified
    IF p_tool_name IS NOT NULL THEN
        -- If denied_tools contains the tool, deny access
        IF v_result.denied_tools @> ARRAY[p_tool_name] THEN
            RETURN QUERY SELECT false, v_result.permission_type, v_result.allowed_tools, v_result.denied_tools;
            RETURN;
        END IF;

        -- If allowed_tools is not empty and doesn't contain the tool, deny access
        IF array_length(v_result.allowed_tools, 1) > 0 AND NOT (v_result.allowed_tools @> ARRAY[p_tool_name]) THEN
            RETURN QUERY SELECT false, v_result.permission_type, v_result.allowed_tools, v_result.denied_tools;
            RETURN;
        END IF;
    END IF;

    -- Log access check
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        v_server_org_id, p_user_id, 'access_check', 'authorization',
        'server', p_server_id, 'check',
        jsonb_build_object(
            'tool_name', p_tool_name,
            'permission_type', p_permission_type,
            'result', true
        )
    );

    RETURN QUERY SELECT true, v_result.permission_type, v_result.allowed_tools, v_result.denied_tools;
END;
$$;

-- ==================================================
-- KEY VAULT MANAGEMENT PROCEDURES
-- ==================================================

-- Register key vault provider procedure
CREATE OR REPLACE FUNCTION vault.register_provider(
    p_organization_id UUID,
    p_provider_name VARCHAR(50),
    p_display_name VARCHAR(255),
    p_configuration JSONB,
    p_is_primary BOOLEAN DEFAULT false,
    p_created_by UUID
)
RETURNS TABLE(
    provider_id UUID,
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_provider_id UUID;
    v_existing_provider_id UUID;
BEGIN
    -- Check if organization exists and is active
    IF NOT EXISTS (
        SELECT 1 FROM auth.organizations 
        WHERE id = p_organization_id AND is_active = true
    ) THEN
        RETURN QUERY SELECT NULL::UUID, false, 'Organization not found or inactive';
        RETURN;
    END IF;

    -- Check if provider already exists for organization
    SELECT id INTO v_existing_provider_id
    FROM vault.providers
    WHERE organization_id = p_organization_id AND provider_name = p_provider_name;

    IF v_existing_provider_id IS NOT NULL THEN
        -- Update existing provider
        UPDATE vault.providers
        SET 
            display_name = p_display_name,
            configuration = p_configuration,
            is_primary = p_is_primary,
            updated_by = p_created_by,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_existing_provider_id;
        
        v_provider_id := v_existing_provider_id;
    ELSE
        -- If setting as primary, unset other primary providers
        IF p_is_primary THEN
            UPDATE vault.providers
            SET is_primary = false
            WHERE organization_id = p_organization_id;
        END IF;

        -- Insert new provider
        INSERT INTO vault.providers (
            organization_id, provider_name, display_name, configuration,
            is_primary, created_by
        ) VALUES (
            p_organization_id, p_provider_name, p_display_name, p_configuration,
            p_is_primary, p_created_by
        ) RETURNING id INTO v_provider_id;
    END IF;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        p_organization_id, p_created_by, 'provider_management', 'configuration',
        'vault_provider', v_provider_id, 
        CASE WHEN v_existing_provider_id IS NOT NULL THEN 'update' ELSE 'create' END,
        jsonb_build_object(
            'provider_name', p_provider_name,
            'is_primary', p_is_primary
        )
    );

    RETURN QUERY SELECT v_provider_id, true, 'Key vault provider registered successfully';
END;
$$;

-- Register secret metadata procedure (NO ACTUAL SECRET VALUES)
CREATE OR REPLACE FUNCTION vault.register_secret_metadata(
    p_organization_id UUID,
    p_provider_id UUID,
    p_secret_name VARCHAR(255),
    p_secret_path VARCHAR(1024),
    p_secret_type VARCHAR(50),
    p_description TEXT DEFAULT NULL,
    p_tags TEXT[] DEFAULT ARRAY[]::TEXT[],
    p_rotation_days INTEGER DEFAULT NULL,
    p_created_by UUID
)
RETURNS TABLE(
    secret_id UUID,
    success BOOLEAN,
    message TEXT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_secret_id UUID;
    v_existing_secret_id UUID;
    v_provider_org_id UUID;
BEGIN
    -- Verify provider belongs to organization
    SELECT organization_id INTO v_provider_org_id
    FROM vault.providers
    WHERE id = p_provider_id;

    IF v_provider_org_id IS NULL THEN
        RETURN QUERY SELECT NULL::UUID, false, 'Provider not found';
        RETURN;
    END IF;

    IF v_provider_org_id != p_organization_id THEN
        RETURN QUERY SELECT NULL::UUID, false, 'Provider does not belong to organization';
        RETURN;
    END IF;

    -- Check if secret name already exists in organization
    SELECT id INTO v_existing_secret_id
    FROM vault.secrets
    WHERE organization_id = p_organization_id AND secret_name = p_secret_name;

    IF v_existing_secret_id IS NOT NULL THEN
        -- Update existing secret metadata
        UPDATE vault.secrets
        SET 
            provider_id = p_provider_id,
            secret_path = p_secret_path,
            secret_type = p_secret_type,
            description = p_description,
            tags = p_tags,
            rotation_days = p_rotation_days,
            next_rotation_at = CASE 
                WHEN p_rotation_days IS NOT NULL 
                THEN CURRENT_TIMESTAMP + INTERVAL '1 day' * p_rotation_days
                ELSE NULL
            END,
            updated_by = p_created_by,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_existing_secret_id;
        
        v_secret_id := v_existing_secret_id;
    ELSE
        -- Insert new secret metadata
        INSERT INTO vault.secrets (
            organization_id, provider_id, secret_name, secret_path, secret_type,
            description, tags, rotation_days, next_rotation_at, created_by
        ) VALUES (
            p_organization_id, p_provider_id, p_secret_name, p_secret_path, p_secret_type,
            p_description, p_tags, p_rotation_days,
            CASE 
                WHEN p_rotation_days IS NOT NULL 
                THEN CURRENT_TIMESTAMP + INTERVAL '1 day' * p_rotation_days
                ELSE NULL
            END,
            p_created_by
        ) RETURNING id INTO v_secret_id;
    END IF;

    -- Log the action
    INSERT INTO audit.audit_logs (
        organization_id, user_id, event_type, event_category,
        resource_type, resource_id, action, details
    ) VALUES (
        p_organization_id, p_created_by, 'secret_management', 'configuration',
        'secret_metadata', v_secret_id,
        CASE WHEN v_existing_secret_id IS NOT NULL THEN 'update' ELSE 'create' END,
        jsonb_build_object(
            'secret_name', p_secret_name,
            'secret_type', p_secret_type,
            'provider_id', p_provider_id
        )
    );

    RETURN QUERY SELECT v_secret_id, true, 'Secret metadata registered successfully';
END;
$$;

-- Print success message
DO $$
BEGIN
    RAISE NOTICE 'MCP server and key vault management procedures created successfully';
    RAISE NOTICE 'MCP Procedures: register_server, update_server_health, get_user_accessible_servers';
    RAISE NOTICE 'MCP Procedures: grant_server_permission, register_server_tool, check_user_server_access';
    RAISE NOTICE 'Vault Procedures: register_provider, register_secret_metadata';
END $$;