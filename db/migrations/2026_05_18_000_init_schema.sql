-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  Salio Sales AI — Initial Schema (Database Bootstrap)                    ║
-- ║                                                                          ║
-- ║  File này tạo TOÀN BỘ schema (40+ bảng + index + trigger) một lần duy    ║
-- ║  nhất — không cần chạy `dotnet ef database update` rồi mới đến migration ║
-- ║  base_fields (2026_05_18_001_add_base_fields.sql) nữa.                   ║
-- ║                                                                          ║
-- ║  Mỗi bảng đã có sẵn 10 base fields theo BaseEntity hierarchy:            ║
-- ║   • BaseEntity            : id                                           ║
-- ║   • AuditableEntity       : + created_at, created_by, updated_at,        ║
-- ║                              updated_by, is_active, sort_index           ║
-- ║                              (+ xmin của Postgres làm version)           ║
-- ║   • SoftDeletableEntity   : + deleted_at, deleted_by                     ║
-- ║   • TenantEntity          : + org_id                                     ║
-- ║                                                                          ║
-- ║  Cách dùng:                                                              ║
-- ║    psql -h localhost -U salio -d salio_dev -f 2026_05_18_000_init_schema.sql
-- ║                                                                          ║
-- ║  Đặc tính: idempotent — chạy nhiều lần an toàn nhờ "IF NOT EXISTS".      ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

BEGIN;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  1. Extensions                                                           ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  2. IDENTITY — organizations, users, org_members                         ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS organizations (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    name        varchar(200) NOT NULL,
    slug        varchar(80)  NOT NULL,
    plan        varchar(40)  NOT NULL DEFAULT 'free',
    locale      varchar(10)  NOT NULL DEFAULT 'vi-VN',
    settings    jsonb        NULL,
    -- base fields
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL,
    deleted_at  timestamptz  NULL,
    deleted_by  uuid         NULL,
    is_active   boolean      NOT NULL DEFAULT TRUE,
    sort_index  integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_organizations_slug ON organizations(slug);
CREATE INDEX IF NOT EXISTS ix_organizations_active ON organizations(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS users (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           varchar(200) NOT NULL,
    full_name       varchar(200) NOT NULL,
    avatar_url      varchar(500) NULL,
    last_login_at   timestamptz  NULL,
    email_verified  boolean      NOT NULL DEFAULT FALSE,
    -- base fields (no sort_index per migration)
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users(email);
CREATE INDEX IF NOT EXISTS ix_users_active ON users(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS org_members (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id      uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    title       varchar(120) NULL,
    joined_at   timestamptz  NULL,
    -- base fields (no soft delete, no sort_index)
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL,
    is_active   boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_org_members_org_user ON org_members(org_id, user_id);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  3. AUTH                                                                 ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS auth_identities (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider            varchar(20)  NOT NULL,
    provider_user_id    varchar(200) NULL,
    password_hash       varchar(500) NULL,
    password_changed_at timestamptz  NULL,
    provider_metadata   jsonb        NULL,
    last_used_at        timestamptz  NULL,
    -- base fields
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          uuid         NULL,
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          uuid         NULL,
    is_active           boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_identities_provider_user
    ON auth_identities(provider, provider_user_id)
    WHERE provider_user_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_auth_identities_user_provider
    ON auth_identities(user_id, provider);

CREATE TABLE IF NOT EXISTS user_sessions (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    session_token       varchar(120) NOT NULL,
    ip_address          varchar(64)  NULL,
    user_agent          varchar(500) NULL,
    device_fingerprint  varchar(200) NULL,
    device_name         varchar(120) NULL,
    last_active_at      timestamptz  NULL,
    expires_at          timestamptz  NOT NULL,
    revoked_at          timestamptz  NULL,
    -- base fields (no is_active, no sort, no soft del)
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          uuid         NULL,
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          uuid         NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_user_sessions_token ON user_sessions(session_token);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id                 uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    session_id              uuid NULL     REFERENCES user_sessions(id) ON DELETE SET NULL,
    token_hash              varchar(200) NOT NULL,
    expires_at              timestamptz  NOT NULL,
    revoked_at              timestamptz  NULL,
    replaced_by_token_id    uuid         NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    created_by              uuid         NULL,
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    updated_by              uuid         NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_refresh_tokens_hash ON refresh_tokens(token_hash);

CREATE TABLE IF NOT EXISTS email_verification_tokens (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  varchar(200) NOT NULL,
    email       varchar(200) NOT NULL,
    expires_at  timestamptz  NOT NULL,
    verified_at timestamptz  NULL,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_email_verification_tokens_hash ON email_verification_tokens(token_hash);

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  varchar(200) NOT NULL,
    expires_at  timestamptz  NOT NULL,
    used_at     timestamptz  NULL,
    ip_address  varchar(64)  NULL,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_password_reset_tokens_hash ON password_reset_tokens(token_hash);

CREATE TABLE IF NOT EXISTS mfa_factors (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id             uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type                varchar(20)  NOT NULL,
    secret_encrypted    text         NULL,
    phone_number        varchar(40)  NULL,
    label               varchar(120) NULL,
    is_primary          boolean      NOT NULL DEFAULT FALSE,
    verified_at         timestamptz  NULL,
    last_used_at        timestamptz  NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          uuid         NULL,
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          uuid         NULL,
    is_active           boolean      NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS mfa_challenges (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    factor_id   uuid NOT NULL REFERENCES mfa_factors(id) ON DELETE CASCADE,
    code_hash   varchar(200) NOT NULL,
    expires_at  timestamptz  NOT NULL,
    verified_at timestamptz  NULL,
    attempts    integer      NOT NULL DEFAULT 0,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL
);

CREATE TABLE IF NOT EXISTS login_attempts (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           varchar(200) NOT NULL,
    user_id         uuid         NULL REFERENCES users(id) ON DELETE SET NULL,
    result          varchar(30)  NOT NULL,
    ip_address      varchar(64)  NULL,
    user_agent      varchar(500) NULL,
    failure_reason  varchar(200) NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL
);
CREATE INDEX IF NOT EXISTS ix_login_attempts_email_created ON login_attempts(email, created_at);

CREATE TABLE IF NOT EXISTS api_keys (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    created_by_id   uuid NOT NULL REFERENCES users(id)         ON DELETE RESTRICT,
    name            varchar(120) NOT NULL,
    key_prefix      varchar(20)  NOT NULL,
    key_hash        varchar(200) NOT NULL,
    scopes          jsonb        NULL,
    expires_at      timestamptz  NULL,
    last_used_at    timestamptz  NULL,
    revoked_at      timestamptz  NULL,
    -- base fields (soft del, toggle, no sort)
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_api_keys_hash ON api_keys(key_hash);
CREATE INDEX IF NOT EXISTS ix_api_keys_org_prefix ON api_keys(org_id, key_prefix);
CREATE INDEX IF NOT EXISTS ix_api_keys_active ON api_keys(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS invitations (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id                  uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    invited_by_id           uuid NOT NULL REFERENCES users(id)         ON DELETE RESTRICT,
    accepted_by_user_id     uuid NULL     REFERENCES users(id)         ON DELETE SET NULL,
    email                   varchar(200) NOT NULL,
    token                   varchar(200) NOT NULL,
    role_code               varchar(60)  NULL,
    expires_at              timestamptz  NOT NULL,
    accepted_at             timestamptz  NULL,
    revoked_at              timestamptz  NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    created_by              uuid         NULL,
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    updated_by              uuid         NULL,
    is_active               boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_invitations_token ON invitations(token);
CREATE INDEX IF NOT EXISTS ix_invitations_org_email ON invitations(org_id, email);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  4. RBAC                                                                 ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS system_functions (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    code            varchar(80)  NOT NULL,
    name            varchar(120) NOT NULL,
    description     text         NULL,
    module_group    varchar(30)  NOT NULL,
    path            varchar(200) NULL,
    icon            varchar(60)  NULL,
    risk_level      varchar(20)  NOT NULL DEFAULT 'Low',
    "order"         integer      NOT NULL DEFAULT 0,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_system_functions_code ON system_functions(code);

CREATE TABLE IF NOT EXISTS system_actions (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    code            varchar(40) NOT NULL,
    name            varchar(80) NOT NULL,
    description     text        NULL,
    "order"         integer     NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    created_by      uuid        NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    updated_by      uuid        NULL,
    is_active       boolean     NOT NULL DEFAULT TRUE,
    sort_index      integer     NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_system_actions_code ON system_actions(code);

CREATE TABLE IF NOT EXISTS function_actions (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    function_id uuid NOT NULL REFERENCES system_functions(id) ON DELETE CASCADE,
    action_id   uuid NOT NULL REFERENCES system_actions(id)   ON DELETE CASCADE,
    is_default  boolean NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_function_actions ON function_actions(function_id, action_id);

CREATE TABLE IF NOT EXISTS permissions (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    function_id     uuid NOT NULL REFERENCES system_functions(id) ON DELETE CASCADE,
    action_id       uuid NOT NULL REFERENCES system_actions(id)   ON DELETE CASCADE,
    scope           varchar(20)  NOT NULL DEFAULT 'Any',
    code            varchar(120) NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_permissions_code ON permissions(code);
CREATE UNIQUE INDEX IF NOT EXISTS ux_permissions_fn_action_scope ON permissions(function_id, action_id, scope);

CREATE TABLE IF NOT EXISTS roles (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid         NULL REFERENCES organizations(id) ON DELETE CASCADE,
    code            varchar(60)  NOT NULL,
    name            varchar(120) NOT NULL,
    description     text         NULL,
    is_system       boolean      NOT NULL DEFAULT FALSE,
    parent_role_id  uuid         NULL,
    priority        integer      NOT NULL DEFAULT 0,
    created_by_id   uuid         NULL REFERENCES users(id) ON DELETE SET NULL,
    -- base fields (soft del)
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0,
    CONSTRAINT fk_roles_parent FOREIGN KEY (parent_role_id) REFERENCES roles(id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_roles_org_code ON roles(org_id, code);
CREATE INDEX IF NOT EXISTS ix_roles_active ON roles(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS role_permissions (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    role_id         uuid NOT NULL REFERENCES roles(id)       ON DELETE CASCADE,
    permission_id   uuid NOT NULL REFERENCES permissions(id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_role_permissions ON role_permissions(role_id, permission_id);

CREATE TABLE IF NOT EXISTS user_roles (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id         uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    role_id         uuid NOT NULL REFERENCES roles(id)         ON DELETE CASCADE,
    assigned_by_id  uuid NULL     REFERENCES users(id)         ON DELETE SET NULL,
    expires_at      timestamptz  NULL,
    -- base fields (soft del, no sort)
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_user_roles ON user_roles(user_id, org_id, role_id);
CREATE INDEX IF NOT EXISTS ix_user_roles_active ON user_roles(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS permission_grants (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id         uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    permission_id   uuid NOT NULL REFERENCES permissions(id)   ON DELETE CASCADE,
    effect          varchar(10) NOT NULL DEFAULT 'Allow',
    reason          text        NULL,
    granted_by_id   uuid NULL   REFERENCES users(id) ON DELETE SET NULL,
    expires_at      timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    created_by      uuid        NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    updated_by      uuid        NULL,
    deleted_at      timestamptz NULL,
    deleted_by      uuid        NULL,
    is_active       boolean     NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_permission_grants ON permission_grants(user_id, org_id, permission_id);
CREATE INDEX IF NOT EXISTS ix_permission_grants_active ON permission_grants(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS teams (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            varchar(120) NOT NULL,
    code            varchar(40)  NOT NULL,
    manager_id      uuid         NULL REFERENCES users(id) ON DELETE SET NULL,
    parent_team_id  uuid         NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0,
    CONSTRAINT fk_teams_parent FOREIGN KEY (parent_team_id) REFERENCES teams(id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_teams_org_code ON teams(org_id, code);
CREATE INDEX IF NOT EXISTS ix_teams_active ON teams(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS team_members (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    team_id     uuid NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_type   varchar(20) NOT NULL DEFAULT 'Member',
    created_at  timestamptz NOT NULL DEFAULT now(),
    created_by  uuid        NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  uuid        NULL,
    is_active   boolean     NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_team_members ON team_members(team_id, user_id);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  5. CRM — companies, contacts, pipelines, deals, products, tasks         ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS companies (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            varchar(200) NOT NULL,
    tax_code        varchar(40)  NULL,
    industry        varchar(80)  NULL,
    size            varchar(40)  NULL,
    website         varchar(200) NULL,
    phone           varchar(40)  NULL,
    email           varchar(200) NULL,
    address         text         NULL,
    owner_id        uuid         NULL REFERENCES users(id) ON DELETE SET NULL,
    custom_fields   jsonb        NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_companies_org_tax ON companies(org_id, tax_code);
CREATE INDEX IF NOT EXISTS ix_companies_org_name ON companies(org_id, name);
CREATE INDEX IF NOT EXISTS ix_companies_active ON companies(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS contacts (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    company_id      uuid NULL     REFERENCES companies(id)     ON DELETE SET NULL,
    full_name       varchar(200) NOT NULL,
    email           varchar(200) NULL,
    phone           varchar(40)  NULL,
    title           varchar(120) NULL,
    is_primary      boolean      NOT NULL DEFAULT FALSE,
    custom_fields   jsonb        NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_contacts_org_email ON contacts(org_id, email);
CREATE INDEX IF NOT EXISTS ix_contacts_org_phone ON contacts(org_id, phone);
CREATE INDEX IF NOT EXISTS ix_contacts_active ON contacts(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS pipelines (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            varchar(120) NOT NULL,
    is_default      boolean      NOT NULL DEFAULT FALSE,
    "order"         integer      NOT NULL DEFAULT 0,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_pipelines_org_name ON pipelines(org_id, name);
CREATE INDEX IF NOT EXISTS ix_pipelines_active ON pipelines(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS pipeline_stages (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    pipeline_id             uuid NOT NULL REFERENCES pipelines(id) ON DELETE CASCADE,
    code                    varchar(40)  NOT NULL,
    name                    varchar(120) NOT NULL,
    "order"                 integer      NOT NULL DEFAULT 0,
    default_probability     integer      NOT NULL DEFAULT 0,
    is_won                  boolean      NOT NULL DEFAULT FALSE,
    is_lost                 boolean      NOT NULL DEFAULT FALSE,
    color                   varchar(20)  NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    created_by              uuid         NULL,
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    updated_by              uuid         NULL,
    is_active               boolean      NOT NULL DEFAULT TRUE,
    sort_index              integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_pipeline_stages_pipeline_code ON pipeline_stages(pipeline_id, code);

CREATE TABLE IF NOT EXISTS deals (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id              uuid NOT NULL REFERENCES organizations(id)  ON DELETE CASCADE,
    code                varchar(40)  NOT NULL,
    title               varchar(200) NOT NULL,
    pipeline_id         uuid NOT NULL REFERENCES pipelines(id)      ON DELETE RESTRICT,
    stage_id            uuid NOT NULL REFERENCES pipeline_stages(id) ON DELETE RESTRICT,
    value               numeric(18,2) NOT NULL DEFAULT 0,
    currency            varchar(3)   NOT NULL DEFAULT 'VND',
    probability         integer      NOT NULL DEFAULT 0,
    source              varchar(20)  NOT NULL DEFAULT 'Other',
    company_id          uuid NULL    REFERENCES companies(id) ON DELETE SET NULL,
    contact_id          uuid NULL    REFERENCES contacts(id)  ON DELETE SET NULL,
    assignee_id         uuid NULL    REFERENCES users(id)     ON DELETE SET NULL,
    expected_close_date date         NULL,
    actual_close_date   timestamptz  NULL,
    ai_score            integer      NULL,
    ai_score_reasons    jsonb        NULL,
    last_activity_at    timestamptz  NULL,
    notes               text         NULL,
    custom_fields       jsonb        NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          uuid         NULL,
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          uuid         NULL,
    deleted_at          timestamptz  NULL,
    deleted_by          uuid         NULL,
    is_active           boolean      NOT NULL DEFAULT TRUE,
    sort_index          integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_deals_org_code ON deals(org_id, code);
CREATE INDEX IF NOT EXISTS ix_deals_org_stage    ON deals(org_id, stage_id);
CREATE INDEX IF NOT EXISTS ix_deals_org_assignee ON deals(org_id, assignee_id);
CREATE INDEX IF NOT EXISTS ix_deals_active ON deals(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS deal_activities (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    deal_id     uuid NOT NULL REFERENCES deals(id) ON DELETE CASCADE,
    type        varchar(40)  NOT NULL,
    title       varchar(300) NOT NULL,
    description text         NULL,
    metadata    jsonb        NULL,
    actor_id    uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL
);
CREATE INDEX IF NOT EXISTS ix_deal_activities_deal_created ON deal_activities(deal_id, created_at);

CREATE TABLE IF NOT EXISTS deal_stage_history (
    id                              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    deal_id                         uuid NOT NULL REFERENCES deals(id)           ON DELETE CASCADE,
    from_stage_id                   uuid NULL     REFERENCES pipeline_stages(id) ON DELETE SET NULL,
    to_stage_id                     uuid NOT NULL REFERENCES pipeline_stages(id) ON DELETE RESTRICT,
    duration_in_prev_stage_seconds  bigint NOT NULL DEFAULT 0,
    changed_by_id                   uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at                      timestamptz NOT NULL DEFAULT now(),
    created_by                      uuid        NULL,
    updated_at                      timestamptz NOT NULL DEFAULT now(),
    updated_by                      uuid        NULL
);
CREATE INDEX IF NOT EXISTS ix_deal_stage_history_deal_created ON deal_stage_history(deal_id, created_at);

CREATE TABLE IF NOT EXISTS products (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    code            varchar(40)  NOT NULL,
    name            varchar(200) NOT NULL,
    description     text         NULL,
    unit_price      numeric(18,2) NOT NULL DEFAULT 0,
    unit            varchar(20)  NOT NULL DEFAULT 'unit',
    currency        varchar(3)   NOT NULL DEFAULT 'VND',
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_products_org_code ON products(org_id, code);
CREATE INDEX IF NOT EXISTS ix_products_active ON products(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS deal_products (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    deal_id         uuid NOT NULL REFERENCES deals(id)    ON DELETE CASCADE,
    product_id      uuid NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    quantity        numeric(18,4) NOT NULL DEFAULT 1,
    unit_price      numeric(18,2) NOT NULL DEFAULT 0,
    discount_pct    numeric(5,2)  NOT NULL DEFAULT 0,
    total           numeric(18,2) NOT NULL DEFAULT 0,
    created_at      timestamptz   NOT NULL DEFAULT now(),
    created_by      uuid          NULL,
    updated_at      timestamptz   NOT NULL DEFAULT now(),
    updated_by      uuid          NULL,
    is_active       boolean       NOT NULL DEFAULT TRUE,
    sort_index      integer       NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS deal_followers (
    deal_id     uuid NOT NULL REFERENCES deals(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    followed_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (deal_id, user_id)
);

CREATE TABLE IF NOT EXISTS tasks (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    title           varchar(300) NOT NULL,
    description     text         NULL,
    assignee_id     uuid NULL    REFERENCES users(id) ON DELETE SET NULL,
    deal_id         uuid NULL    REFERENCES deals(id) ON DELETE CASCADE,
    due_at          timestamptz  NULL,
    completed_at    timestamptz  NULL,
    priority        varchar(20)  NOT NULL DEFAULT 'Medium',
    status          varchar(20)  NOT NULL DEFAULT 'Pending',
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_tasks_org_assignee_status ON tasks(org_id, assignee_id, status);
CREATE INDEX IF NOT EXISTS ix_tasks_org_deal           ON tasks(org_id, deal_id);
CREATE INDEX IF NOT EXISTS ix_tasks_active ON tasks(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  6. LIBRARY (Knowledge base)                                             ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS library_nodes (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    parent_id       uuid NULL,
    root_type       varchar(20)  NOT NULL,
    type            varchar(20)  NOT NULL,
    name            varchar(300) NOT NULL,
    status          varchar(20)  NOT NULL DEFAULT 'Active',
    file_id         varchar(120) NULL,
    file_url        text         NULL,
    file_mime       varchar(80)  NULL,
    file_size_bytes bigint       NULL,
    path            varchar(2000) NULL,
    is_system       boolean      NOT NULL DEFAULT FALSE,
    owner_id        uuid         NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0,
    CONSTRAINT fk_library_nodes_parent FOREIGN KEY (parent_id) REFERENCES library_nodes(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_library_nodes_org_root_parent ON library_nodes(org_id, root_type, parent_id);
CREATE INDEX IF NOT EXISTS ix_library_nodes_active ON library_nodes(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS library_permissions (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    node_id         uuid NOT NULL REFERENCES library_nodes(id) ON DELETE CASCADE,
    principal_type  varchar(20) NOT NULL DEFAULT 'user',
    principal_id    uuid        NOT NULL,
    permission      varchar(20) NOT NULL DEFAULT 'view',
    created_at      timestamptz NOT NULL DEFAULT now(),
    created_by      uuid        NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    updated_by      uuid        NULL,
    is_active       boolean     NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_library_permissions
    ON library_permissions(node_id, principal_type, principal_id);

CREATE TABLE IF NOT EXISTS document_chunks (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    node_id         uuid NOT NULL REFERENCES library_nodes(id) ON DELETE CASCADE,
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    chunk_index     integer NOT NULL DEFAULT 0,
    content         text    NOT NULL,
    content_tokens  integer NOT NULL DEFAULT 0,
    embedding       vector(1536) NULL,
    metadata        jsonb   NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    created_by      uuid        NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    updated_by      uuid        NULL,
    sort_index      integer     NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_document_chunks_node_chunk ON document_chunks(node_id, chunk_index);
CREATE INDEX IF NOT EXISTS ix_document_chunks_org ON document_chunks(org_id);
-- HNSW index cho similarity search (chỉ tạo nếu pgvector >= 0.5)
-- Bật khi đã có data lớn: CREATE INDEX ON document_chunks USING hnsw (embedding vector_cosine_ops);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  7. CHAT (AI assistant)                                                  ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS chat_conversations (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    title           varchar(300) NOT NULL,
    context_type    varchar(40)  NULL,
    context_id      uuid         NULL,
    pinned          boolean      NOT NULL DEFAULT FALSE,
    last_message_at timestamptz  NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    deleted_at      timestamptz  NULL,
    deleted_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE,
    sort_index      integer      NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_chat_conversations_org_user_last ON chat_conversations(org_id, user_id, last_message_at);
CREATE INDEX IF NOT EXISTS ix_chat_conversations_active ON chat_conversations(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS chat_messages (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    conversation_id uuid NOT NULL REFERENCES chat_conversations(id) ON DELETE CASCADE,
    role            varchar(20)  NOT NULL,
    content         text         NOT NULL,
    content_tokens  integer      NOT NULL DEFAULT 0,
    model           varchar(80)  NULL,
    latency_ms      integer      NULL,
    metadata        jsonb        NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL
);
CREATE INDEX IF NOT EXISTS ix_chat_messages_conv_created ON chat_messages(conversation_id, created_at);

CREATE TABLE IF NOT EXISTS chat_message_sources (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    message_id  uuid NOT NULL REFERENCES chat_messages(id)   ON DELETE CASCADE,
    chunk_id    uuid NOT NULL REFERENCES document_chunks(id) ON DELETE RESTRICT,
    score       numeric(5,4) NOT NULL DEFAULT 0,
    label       varchar(200) NULL,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL
);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  8. AI INSIGHT / SCORING                                                 ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS ai_insights (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    scope_type          varchar(40)  NOT NULL,
    scope_id            uuid         NOT NULL,
    type                varchar(60)  NOT NULL,
    title               varchar(300) NOT NULL,
    body                text         NULL,
    priority            varchar(20)  NULL,
    suggested_action    jsonb        NULL,
    model               varchar(80)  NULL,
    status              varchar(20)  NOT NULL DEFAULT 'Active',
    expires_at          timestamptz  NULL,
    dismissed_by_id     uuid         NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    created_by          uuid         NULL,
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          uuid         NULL,
    deleted_at          timestamptz  NULL,
    deleted_by          uuid         NULL,
    is_active           boolean      NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS ix_ai_insights_org_status_created ON ai_insights(org_id, status, created_at);
CREATE INDEX IF NOT EXISTS ix_ai_insights_active ON ai_insights(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS ai_score_history (
    id          uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    deal_id     uuid NOT NULL REFERENCES deals(id) ON DELETE CASCADE,
    score       integer NOT NULL DEFAULT 0,
    reasons     jsonb   NULL,
    model       varchar(80) NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    created_by  uuid        NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  uuid        NULL
);
CREATE INDEX IF NOT EXISTS ix_ai_score_history_deal_created ON ai_score_history(deal_id, created_at);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  9. DUPLICATE DETECTION                                                  ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS dup_match_groups (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id              uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    entity_type         varchar(40) NOT NULL,
    match_field         varchar(60) NOT NULL,
    confidence          varchar(20) NOT NULL,
    confidence_score    numeric(5,4) NOT NULL DEFAULT 0,
    status              varchar(20) NOT NULL DEFAULT 'Pending',
    master_record_id    uuid        NULL,
    resolved_by_id      uuid        NULL REFERENCES users(id) ON DELETE SET NULL,
    resolved_at         timestamptz NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    created_by          uuid        NULL,
    updated_at          timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid        NULL,
    deleted_at          timestamptz NULL,
    deleted_by          uuid        NULL,
    is_active           boolean     NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS ix_dup_match_groups_org_entity_status ON dup_match_groups(org_id, entity_type, status);
CREATE INDEX IF NOT EXISTS ix_dup_match_groups_active ON dup_match_groups(id)
    WHERE deleted_at IS NULL AND is_active = TRUE;

CREATE TABLE IF NOT EXISTS dup_match_records (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    match_group_id      uuid NOT NULL REFERENCES dup_match_groups(id) ON DELETE CASCADE,
    record_id           uuid NOT NULL,
    record_snapshot     jsonb NULL,
    is_master_candidate boolean NOT NULL DEFAULT FALSE,
    created_at          timestamptz NOT NULL DEFAULT now(),
    created_by          uuid        NULL,
    updated_at          timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid        NULL
);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  10. CROSS-CUTTING — notifications, audit_logs                           ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE TABLE IF NOT EXISTS notifications (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    recipient_id    uuid NOT NULL REFERENCES users(id)         ON DELETE CASCADE,
    type            varchar(60)  NOT NULL,
    title           varchar(300) NOT NULL,
    body            text         NULL,
    link_url        varchar(500) NULL,
    entity_type     varchar(60)  NULL,
    entity_id       uuid         NULL,
    read_at         timestamptz  NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL,
    is_active       boolean      NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS ix_notifications_recipient_read ON notifications(recipient_id, read_at);

CREATE TABLE IF NOT EXISTS audit_logs (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    org_id          uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    actor_id        uuid NULL     REFERENCES users(id)         ON DELETE SET NULL,
    action          varchar(80)  NOT NULL,
    entity_type     varchar(80)  NOT NULL,
    entity_id       uuid         NULL,
    before          jsonb        NULL,
    after           jsonb        NULL,
    ip_address      varchar(64)  NULL,
    user_agent      varchar(500) NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      uuid         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    updated_by      uuid         NULL
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_org_entity ON audit_logs(org_id, entity_type, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_org_created ON audit_logs(org_id, created_at);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  11. Trigger: tự cập nhật updated_at mỗi khi UPDATE                      ║
-- ║      (version dùng cột xmin hệ thống — không cần trigger)                ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

CREATE OR REPLACE FUNCTION trg_set_updated_at() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at := CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION trg_set_updated_at() IS
    'Trigger function: tự cập nhật updated_at = CURRENT_TIMESTAMP mỗi khi UPDATE';

-- Gắn trigger cho mọi bảng có cột updated_at
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT c.table_name
        FROM information_schema.columns c
        WHERE c.column_name = 'updated_at'
          AND c.table_schema = current_schema()
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_%I_updated_at ON %I;
             CREATE TRIGGER trg_%I_updated_at
             BEFORE UPDATE ON %I
             FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();',
            r.table_name, r.table_name, r.table_name, r.table_name
        );
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  12. Comments cho các cột base fields chuẩn                              ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

DO $$
DECLARE r RECORD;
BEGIN
    FOR r IN
        SELECT DISTINCT c.table_name, c.column_name
        FROM information_schema.columns c
        WHERE c.column_name IN ('created_at','created_by','updated_at','updated_by',
                                'deleted_at','deleted_by','is_active','sort_index')
          AND c.table_schema = current_schema()
    LOOP
        EXECUTE format(
            'COMMENT ON COLUMN %I.%I IS %L',
            r.table_name, r.column_name,
            CASE r.column_name
                WHEN 'created_at' THEN 'Thời điểm tạo bản ghi (UTC)'
                WHEN 'created_by' THEN 'UserId người tạo bản ghi — FK users(id)'
                WHEN 'updated_at' THEN 'Thời điểm cập nhật gần nhất (UTC) — auto trigger'
                WHEN 'updated_by' THEN 'UserId người cập nhật gần nhất — FK users(id)'
                WHEN 'deleted_at' THEN 'Xóa mềm — thời điểm bị xóa. NULL = còn hiệu lực.'
                WHEN 'deleted_by' THEN 'UserId người thực hiện xóa mềm — FK users(id)'
                WHEN 'is_active'  THEN 'Bật/tắt trạng thái hoạt động (không phải soft-delete)'
                WHEN 'sort_index' THEN 'Thứ tự sắp xếp UI (drag & drop)'
            END
        );
    END LOOP;
END $$;

COMMIT;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  KIỂM TRA SAU INIT                                                       ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
-- Đếm tổng số bảng đã tạo (kỳ vọng: 46 bảng)
--   SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema();
--
-- Liệt kê bảng + số cột:
--   SELECT t.table_name, COUNT(c.column_name) AS columns
--   FROM information_schema.tables t
--   JOIN information_schema.columns c ON c.table_name = t.table_name
--   WHERE t.table_schema = current_schema()
--   GROUP BY t.table_name
--   ORDER BY t.table_name;
--
-- Kiểm tra extension đã bật:
--   SELECT extname, extversion FROM pg_extension ORDER BY extname;
