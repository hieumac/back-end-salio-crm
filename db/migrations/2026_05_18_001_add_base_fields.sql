-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  Migration: 2026-05-18 — Add Base Fields System (10 trường chuẩn)        ║
-- ║                                                                          ║
-- ║  Bổ sung 10 trường audit/control chuẩn cho mọi bảng nghiệp vụ:           ║
-- ║    1.  id          (đã có — UUID)                                        ║
-- ║    2.  created_at  (rename từ "CreatedAt" → snake_case)                  ║
-- ║    3.  created_by  (MỚI — UUID FK users.id)                              ║
-- ║    4.  updated_at  (rename từ "UpdatedAt" → snake_case)                  ║
-- ║    5.  updated_by  (MỚI — UUID FK users.id)                              ║
-- ║    6.  deleted_at  (rename từ "DeletedAt" → snake_case, chỉ soft-delete) ║
-- ║    7.  deleted_by  (MỚI — UUID FK users.id, chỉ soft-delete)             ║
-- ║    8.  is_active   (rename từ "IsActive" hoặc MỚI, default TRUE)         ║
-- ║    9.  sort_index  (MỚI — int default 0)                                 ║
-- ║    10. version     → dùng cột hệ thống xmin của PostgreSQL (không tạo)   ║
-- ║                                                                          ║
-- ║  Đặc tính: idempotent — chạy nhiều lần an toàn nhờ kiểm tra IF EXISTS.   ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

BEGIN;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  Helper function: thêm base fields cho 1 bảng                            ║
-- ║    p_table     — tên bảng                                                 ║
-- ║    p_soft_del  — TRUE nếu bảng cần deleted_at/deleted_by + soft delete    ║
-- ║    p_sortable  — TRUE nếu bảng cần sort_index (mặc định FALSE cho log)    ║
-- ║    p_toggle    — TRUE nếu bảng cần is_active (mặc định FALSE cho log)     ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
CREATE OR REPLACE FUNCTION pg_temp.add_base_fields(
    p_table     text,
    p_soft_del  boolean DEFAULT FALSE,
    p_sortable  boolean DEFAULT TRUE,
    p_toggle    boolean DEFAULT TRUE
) RETURNS void AS $$
DECLARE
    v_exists boolean;
BEGIN
    -- 1. Rename các cột PascalCase sẵn có sang snake_case (idempotent)
    PERFORM pg_temp.rename_col(p_table, 'CreatedAt', 'created_at');
    PERFORM pg_temp.rename_col(p_table, 'UpdatedAt', 'updated_at');
    PERFORM pg_temp.rename_col(p_table, 'DeletedAt', 'deleted_at');
    PERFORM pg_temp.rename_col(p_table, 'IsActive',  'is_active');

    -- 2. created_by (UUID, nullable, FK)
    EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS created_by uuid NULL', p_table);
    EXECUTE format('COMMENT ON COLUMN %I.created_by IS ''UserId người tạo bản ghi — FK users(id)''', p_table);

    -- 3. updated_by (UUID, nullable, FK)
    EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS updated_by uuid NULL', p_table);
    EXECUTE format('COMMENT ON COLUMN %I.updated_by IS ''UserId người cập nhật gần nhất — FK users(id)''', p_table);

    -- 4. is_active (boolean, default true)
    IF p_toggle THEN
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT TRUE', p_table);
        EXECUTE format('COMMENT ON COLUMN %I.is_active IS ''Bật/tắt trạng thái hoạt động (không phải soft-delete)''', p_table);
    END IF;

    -- 5. sort_index (int, default 0)
    IF p_sortable THEN
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS sort_index integer NOT NULL DEFAULT 0', p_table);
        EXECUTE format('COMMENT ON COLUMN %I.sort_index IS ''Thứ tự sắp xếp UI (drag & drop)''', p_table);
    END IF;

    -- 6. deleted_at / deleted_by (chỉ bảng cần soft delete)
    IF p_soft_del THEN
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS deleted_at timestamptz NULL', p_table);
        EXECUTE format('COMMENT ON COLUMN %I.deleted_at IS ''Xóa mềm — thời điểm bị xóa. NULL = còn hiệu lực.''', p_table);
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS deleted_by uuid NULL', p_table);
        EXECUTE format('COMMENT ON COLUMN %I.deleted_by IS ''UserId người thực hiện xóa mềm — FK users(id)''', p_table);

        -- Partial index để query nhanh các bản ghi còn hiệu lực
        EXECUTE format(
            'CREATE INDEX IF NOT EXISTS ix_%I_active ON %I (id) WHERE deleted_at IS NULL%s',
            p_table, p_table,
            CASE WHEN p_toggle THEN ' AND is_active = TRUE' ELSE '' END
        );
    END IF;

    -- 7. Comment cho created_at / updated_at (luôn áp dụng)
    EXECUTE format('COMMENT ON COLUMN %I.created_at IS ''Thời điểm tạo bản ghi (UTC)''', p_table);
    EXECUTE format('COMMENT ON COLUMN %I.updated_at IS ''Thời điểm cập nhật gần nhất (UTC)''', p_table);
END;
$$ LANGUAGE plpgsql;

-- Helper rename column nếu tồn tại
CREATE OR REPLACE FUNCTION pg_temp.rename_col(p_table text, p_old text, p_new text)
RETURNS void AS $$
DECLARE
    v_exists_old boolean;
    v_exists_new boolean;
BEGIN
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = p_table AND column_name = p_old) INTO v_exists_old;
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = p_table AND column_name = p_new) INTO v_exists_new;
    IF v_exists_old AND NOT v_exists_new THEN
        EXECUTE format('ALTER TABLE %I RENAME COLUMN %I TO %I', p_table, p_old, p_new);
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  IDENTITY                                                                ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
SELECT pg_temp.add_base_fields('organizations', p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('users',         p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('org_members',   p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  AUTH (token/session — chỉ thêm audit cơ bản)                            ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
SELECT pg_temp.add_base_fields('auth_identities',           p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('user_sessions',             p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('refresh_tokens',            p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('email_verification_tokens', p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('password_reset_tokens',     p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('mfa_factors',               p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('mfa_challenges',            p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('login_attempts',            p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('api_keys',                  p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('invitations',               p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  RBAC                                                                    ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
SELECT pg_temp.add_base_fields('system_functions', p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('system_actions',   p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := TRUE);
-- function_actions  : Junction (function_id, action_id) — KHÔNG thêm base fields
SELECT pg_temp.add_base_fields('permissions',      p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('roles',            p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
-- role_permissions  : Junction (role_id, permission_id) — KHÔNG thêm base fields
SELECT pg_temp.add_base_fields('user_roles',       p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('permission_grants',p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('teams',            p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('team_members',     p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  CRM                                                                     ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
SELECT pg_temp.add_base_fields('companies',          p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('contacts',           p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('pipelines',          p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('pipeline_stages',    p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('deals',              p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('deal_activities',    p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('deal_stage_history', p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('products',           p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('deal_products',      p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := TRUE);
-- deal_followers : Junction (deal_id, user_id) — KHÔNG thêm base fields
SELECT pg_temp.add_base_fields('tasks',              p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  CHAT / AI / LIBRARY / DUPLICATE / CROSS-CUTTING                         ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
SELECT pg_temp.add_base_fields('chat_conversations',    p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('chat_messages',         p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);
SELECT pg_temp.add_base_fields('chat_message_sources',  p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);

SELECT pg_temp.add_base_fields('ai_insights',           p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('ai_score_history',      p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);

SELECT pg_temp.add_base_fields('library_nodes',         p_soft_del := TRUE,  p_sortable := TRUE,  p_toggle := TRUE);
SELECT pg_temp.add_base_fields('library_permissions',   p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('document_chunks',       p_soft_del := FALSE, p_sortable := TRUE,  p_toggle := FALSE);

SELECT pg_temp.add_base_fields('dup_match_groups',      p_soft_del := TRUE,  p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('dup_match_records',     p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);

SELECT pg_temp.add_base_fields('notifications',         p_soft_del := FALSE, p_sortable := FALSE, p_toggle := TRUE);
SELECT pg_temp.add_base_fields('audit_logs',            p_soft_del := FALSE, p_sortable := FALSE, p_toggle := FALSE);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  Trigger: tự cập nhật updated_at mỗi khi UPDATE                          ║
-- ║  (version dùng xmin của PostgreSQL — không cần trigger)                  ║
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
-- ║  Optional FK constraints (created_by/updated_by/deleted_by → users.id)   ║
-- ║  → Chỉ enable nếu DB của bạn không gặp issue circular FK với users       ║
-- ║    (user tự tạo chính mình → created_by = NULL trong seed).              ║
-- ║                                                                          ║
-- ║  Vì có thể gây seeding khó khăn, để CONSTRAINT DEFERRABLE.               ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
-- DO $$
-- DECLARE r RECORD;
-- BEGIN
--     FOR r IN
--         SELECT DISTINCT c.table_name
--         FROM information_schema.columns c
--         WHERE c.column_name IN ('created_by','updated_by','deleted_by')
--           AND c.table_schema = current_schema()
--           AND c.table_name <> 'users'
--     LOOP
--         EXECUTE format(
--             'ALTER TABLE %I ADD CONSTRAINT fk_%I_created_by FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL DEFERRABLE INITIALLY DEFERRED',
--             r.table_name, r.table_name);
--         -- ... tương tự cho updated_by, deleted_by
--     END LOOP;
-- END $$;

COMMIT;

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  KIỂM TRA SAU MIGRATION                                                  ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
-- SELECT table_name, column_name, data_type, is_nullable, column_default
-- FROM information_schema.columns
-- WHERE column_name IN ('created_at','created_by','updated_at','updated_by',
--                       'deleted_at','deleted_by','is_active','sort_index')
--   AND table_schema = current_schema()
-- ORDER BY table_name, ordinal_position;
