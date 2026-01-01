-- =====================================================
-- DATABASE VERIFICATION & OPTIMIZATION FOR SUPABASE
-- =====================================================

-- 1. CHECK PROVIDERS TABLE STRUCTURE
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
    AND table_name = 'providers'
ORDER BY ordinal_position;

-- 2. CHECK FOR INDEXES (Performance optimization)
-- Check indexes on provider_api_keys
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public' 
    AND tablename = 'provider_api_keys'
ORDER BY indexname;

-- Check indexes on providers
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public' 
    AND tablename = 'providers'
ORDER BY indexname;

-- 3. CHECK FOREIGN KEY CONSTRAINTS
SELECT
    tc.table_name, 
    kcu.column_name, 
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name,
    tc.constraint_name
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_schema = 'public'
    AND (tc.table_name = 'provider_api_keys' OR tc.table_name = 'providers')
ORDER BY tc.table_name, kcu.column_name;

-- =====================================================
-- RECOMMENDED INDEXES FOR PERFORMANCE
-- =====================================================

-- These indexes will improve query performance for common operations
-- Run these if they don't already exist

-- Index on provider_name for fast lookups (critical for GetKeysForProviderAsync)
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_provider_name 
ON provider_api_keys(provider_name);

-- Index on is_active for filtering active keys (used in GetNextKeyAsync)
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_is_active 
ON provider_api_keys(is_active) 
WHERE is_active = true;

-- Composite index for provider + active status (most common query pattern)
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_provider_active 
ON provider_api_keys(provider_name, is_active) 
WHERE is_active = true;

-- Index on key_id for fast lookups (already PK but good to verify)
-- Primary key automatically creates index, so this is just documentation

-- Index on last_used_at for LRU selection (used in GetNextKeyAsync)
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_last_used_at 
ON provider_api_keys(last_used_at) 
WHERE is_active = true;

-- Index on cooldown_until for filtering keys not in cooldown
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_cooldown 
ON provider_api_keys(cooldown_until) 
WHERE cooldown_until IS NOT NULL;

-- Index on provider_name for providers table (if not already PK)
CREATE INDEX IF NOT EXISTS idx_providers_provider_name 
ON providers(provider_name);

-- Index on is_enabled for filtering enabled providers
CREATE INDEX IF NOT EXISTS idx_providers_is_enabled 
ON providers(is_enabled) 
WHERE is_enabled = true;

-- Index on priority for sorting providers (used in GetAllProvidersAsync)
CREATE INDEX IF NOT EXISTS idx_providers_priority 
ON providers(priority DESC);

-- =====================================================
-- VERIFY DATA INTEGRITY CONSTRAINTS
-- =====================================================

-- Check if foreign key exists from provider_api_keys to providers
-- If not, you should add it for data integrity
SELECT 
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_name = 'provider_api_keys'
    AND kcu.column_name = 'provider_name'
    AND ccu.table_name = 'providers'
    AND ccu.column_name = 'provider_name';

-- =====================================================
-- CREATE FOREIGN KEY IF MISSING (Run only if needed)
-- =====================================================

-- Uncomment and run if foreign key doesn't exist:
/*
ALTER TABLE provider_api_keys
ADD CONSTRAINT fk_provider_api_keys_provider_name 
FOREIGN KEY (provider_name) 
REFERENCES providers(provider_name)
ON DELETE CASCADE
ON UPDATE CASCADE;
*/

-- =====================================================
-- ADD CHECK CONSTRAINTS FOR DATA VALIDITY (Optional)
-- =====================================================

-- Ensure failure_count is non-negative
/*
ALTER TABLE provider_api_keys
ADD CONSTRAINT chk_provider_api_keys_failure_count 
CHECK (failure_count >= 0);
*/

-- Ensure total_calls is non-negative
/*
ALTER TABLE provider_api_keys
ADD CONSTRAINT chk_provider_api_keys_total_calls 
CHECK (total_calls >= 0);
*/

-- =====================================================
-- VERIFY DEFAULT VALUES
-- =====================================================

-- Check current defaults
SELECT 
    column_name,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
    AND table_name = 'provider_api_keys'
    AND column_name IN ('is_active', 'failure_count', 'total_calls', 'created_at', 'updated_at')
ORDER BY column_name;

-- =====================================================
-- SET DEFAULT VALUES IF MISSING (Run only if needed)
-- =====================================================

-- Uncomment and run if defaults are missing:
/*
-- Set default for is_active
ALTER TABLE provider_api_keys 
ALTER COLUMN is_active SET DEFAULT true;

-- Set default for failure_count
ALTER TABLE provider_api_keys 
ALTER COLUMN failure_count SET DEFAULT 0;

-- Set default for total_calls
ALTER TABLE provider_api_keys 
ALTER COLUMN total_calls SET DEFAULT 0;

-- Set default for created_at (if using timestamp)
ALTER TABLE provider_api_keys 
ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;

-- Set default for updated_at (requires trigger for auto-update)
*/

-- =====================================================
-- CREATE UPDATED_AT AUTO-UPDATE TRIGGER (Recommended)
-- =====================================================

-- This automatically updates updated_at when row is modified

-- Create function if it doesn't exist
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create trigger for provider_api_keys
DROP TRIGGER IF EXISTS update_provider_api_keys_updated_at ON provider_api_keys;
CREATE TRIGGER update_provider_api_keys_updated_at
    BEFORE UPDATE ON provider_api_keys
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Create trigger for providers table (if it has updated_at)
-- Uncomment if providers table has updated_at column:
/*
DROP TRIGGER IF EXISTS update_providers_updated_at ON providers;
CREATE TRIGGER update_providers_updated_at
    BEFORE UPDATE ON providers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
*/

