    -- =====================================================
    -- DATABASE STRUCTURE INSPECTION QUERIES FOR SUPABASE
    -- =====================================================
    -- Run these queries in Supabase SQL Editor to inspect your database

    -- 1. GET ALL TABLES IN THE DATABASE
    SELECT 
        table_schema,
        table_name,
        table_type
    FROM information_schema.tables 
    WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
        AND table_schema NOT LIKE 'pg_toast%'
    ORDER BY table_schema, table_name;

    -- 2. GET DETAILED TABLE STRUCTURE (COLUMNS, TYPES, NULLABLE, DEFAULTS)
    SELECT 
        t.table_schema,
        t.table_name,
        c.column_name,
        c.data_type,
        c.character_maximum_length,
        c.is_nullable,
        c.column_default,
        CASE 
            WHEN pk.column_name IS NOT NULL THEN 'YES'
            ELSE 'NO'
        END as is_primary_key
    FROM information_schema.tables t
    JOIN information_schema.columns c ON t.table_name = c.table_name AND t.table_schema = c.table_schema
    LEFT JOIN (
        SELECT ku.table_schema, ku.table_name, ku.column_name
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage ku 
            ON tc.constraint_type = 'PRIMARY KEY' 
            AND tc.constraint_name = ku.constraint_name
            AND tc.table_schema = ku.table_schema
    ) pk ON c.table_name = pk.table_name 
        AND c.table_schema = pk.table_schema 
        AND c.column_name = pk.column_name
    WHERE t.table_schema NOT IN ('pg_catalog', 'information_schema')
        AND t.table_schema NOT LIKE 'pg_toast%'
        AND t.table_type = 'BASE TABLE'
    ORDER BY t.table_name, c.ordinal_position;

    -- 3. GET FOREIGN KEY RELATIONSHIPS
    SELECT
        tc.table_schema, 
        tc.constraint_name, 
        tc.table_name, 
        kcu.column_name, 
        ccu.table_schema AS foreign_table_schema,
        ccu.table_name AS foreign_table_name,
        ccu.column_name AS foreign_column_name 
    FROM information_schema.table_constraints AS tc 
    JOIN information_schema.key_column_usage AS kcu
        ON tc.constraint_name = kcu.constraint_name
        AND tc.table_schema = kcu.table_schema
    JOIN information_schema.constraint_column_usage AS ccu
        ON ccu.constraint_name = tc.constraint_name
        AND ccu.table_schema = tc.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
        AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
    ORDER BY tc.table_name, kcu.column_name;

    -- 4. GET TABLE ROW COUNTS
    SELECT 
        schemaname,
        tablename,
        pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size,
        (SELECT COUNT(*) FROM information_schema.columns 
        WHERE table_schema = schemaname AND table_name = tablename) as column_count
    FROM pg_tables
    WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
        AND schemaname NOT LIKE 'pg_toast%'
    ORDER BY tablename;

    -- 5. SPECIFIC TABLE STRUCTURE (Run for each table)
    -- Example for 'providers' table:
    SELECT 
        column_name,
        data_type,
        character_maximum_length,
        is_nullable,
        column_default
    FROM information_schema.columns
    WHERE table_schema = 'public'
        AND table_name = 'providers'  -- Change table name here
    ORDER BY ordinal_position;

    -- 6. CHECK IF SPECIFIC TABLES EXIST (Quick check)
    SELECT table_name 
    FROM information_schema.tables 
    WHERE table_schema = 'public' 
        AND table_name IN (
            'users',
            'providers',
            'provider_api_keys',
            'ai_models',
            'comparisons',
            'model_votes',
            'threads',
            'thread_messages',
            'admins'
        )
    ORDER BY table_name;

    -- =====================================================
    -- QUICK STRUCTURE CHECK FOR PROVIDER-RELATED TABLES
    -- =====================================================

    -- Check providers table structure
    SELECT 
        column_name,
        data_type,
        is_nullable,
        column_default
    FROM information_schema.columns
    WHERE table_schema = 'public' 
        AND table_name = 'providers'
    ORDER BY ordinal_position;

    -- Check provider_api_keys table structure
    SELECT 
        column_name,
        data_type,
        is_nullable,
        column_default
    FROM information_schema.columns
    WHERE table_schema = 'public' 
        AND table_name = 'provider_api_keys'
    ORDER BY ordinal_position;

    -- Get sample data from providers (first 5 rows)
    SELECT * FROM providers LIMIT 5;

    -- Get sample data from provider_api_keys (first 5 rows, excluding encrypted keys for security)
    SELECT 
        key_id,
        provider_name,
        display_mask,
        is_active,
        failure_count,
        total_calls,
        last_used_at,
        last_error_type,
        last_error_category,
        cooldown_until,
        created_at,
        updated_at
    FROM provider_api_keys 
    LIMIT 5;

    -- =====================================================
    -- CHECK FOR MISSING COLUMNS (Based on our code)
    -- =====================================================

    -- Check if provider_api_keys has all required columns
    SELECT 
        column_name,
        data_type
    FROM information_schema.columns
    WHERE table_schema = 'public' 
        AND table_name = 'provider_api_keys'
        AND column_name IN (
            'key_id',
            'provider_name',
            'encrypted_api_key',
            'display_mask',
            'is_active',
            'failure_count',
            'total_calls',
            'last_used_at',
            'last_error_type',
            'last_error_category',
            'cooldown_until',
            'created_at',
            'updated_at',
            'created_by'
        )
    ORDER BY column_name;

    -- =====================================================
    -- CREATE MISSING COLUMNS (Run only if needed)
    -- =====================================================

    -- Uncomment and run these if columns are missing:

    /*
    -- Add total_calls if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS total_calls INTEGER DEFAULT 0;

    -- Add failure_count if missing  
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS failure_count INTEGER DEFAULT 0;

    -- Add last_used_at if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS last_used_at TIMESTAMP WITH TIME ZONE;

    -- Add last_error_type if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS last_error_type VARCHAR(50);

    -- Add last_error_category if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS last_error_category VARCHAR(50);

    -- Add cooldown_until if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS cooldown_until TIMESTAMP WITH TIME ZONE;

    -- Add display_mask if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS display_mask VARCHAR(20);

    -- Add created_by if missing
    ALTER TABLE provider_api_keys 
    ADD COLUMN IF NOT EXISTS created_by UUID;
    */

