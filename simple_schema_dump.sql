-- =====================================================
-- SIMPLE ONE-QUERY SCHEMA DUMP
-- =====================================================
-- Copy and paste the ENTIRE output of this query
-- It shows everything: tables, columns, types, PKs, FKs, indexes

SELECT 
    'TABLE: ' || table_name as info,
    '' as column_name,
    '' as data_type,
    '' as nullable,
    '' as default_value,
    '' as keys,
    '' as indexes
FROM information_schema.tables
WHERE table_schema = 'public' 
    AND table_type = 'BASE TABLE'
    AND table_name IN (
        'users', 'providers', 'provider_api_keys', 'ai_models', 
        'comparisons', 'model_votes', 'threads', 'thread_messages', 'admins'
    )
ORDER BY table_name

UNION ALL

SELECT 
    '  COLUMN: ' || c.column_name,
    c.data_type || 
    CASE 
        WHEN c.character_maximum_length IS NOT NULL 
        THEN '(' || c.character_maximum_length || ')'
        WHEN c.numeric_precision IS NOT NULL 
        THEN '(' || c.numeric_precision || 
             CASE WHEN c.numeric_scale > 0 THEN ',' || c.numeric_scale ELSE '' END || ')'
        ELSE ''
    END,
    CASE WHEN c.is_nullable = 'YES' THEN 'NULL' ELSE 'NOT NULL' END,
    COALESCE('DEFAULT: ' || c.column_default, ''),
    CASE 
        WHEN pk.column_name IS NOT NULL THEN 'PRIMARY KEY'
        WHEN fk.column_name IS NOT NULL THEN 'FK→' || fk.foreign_table_name || '(' || fk.foreign_column_name || ')'
        ELSE ''
    END,
    ''
FROM information_schema.tables t
JOIN information_schema.columns c 
    ON t.table_name = c.table_name 
    AND t.table_schema = c.table_schema
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
LEFT JOIN (
    SELECT 
        kcu.table_schema,
        kcu.table_name,
        kcu.column_name,
        ccu.table_name AS foreign_table_name,
        ccu.column_name AS foreign_column_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
        AND tc.table_schema = kcu.table_schema
    JOIN information_schema.constraint_column_usage ccu
        ON ccu.constraint_name = tc.constraint_name
        AND ccu.table_schema = tc.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
) fk ON c.table_name = fk.table_name
    AND c.table_schema = fk.table_schema
    AND c.column_name = fk.column_name
WHERE t.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
    AND t.table_name IN (
        'users', 'providers', 'provider_api_keys', 'ai_models', 
        'comparisons', 'model_votes', 'threads', 'thread_messages', 'admins'
    )
ORDER BY info, c.ordinal_position;

-- =====================================================
-- OR EVEN SIMPLER: Just run this single query
-- =====================================================

SELECT 
    t.table_name,
    STRING_AGG(
        c.column_name || ' ' || 
        c.data_type || 
        CASE WHEN c.character_maximum_length IS NOT NULL 
            THEN '(' || c.character_maximum_length || ')' 
            ELSE '' END ||
        CASE WHEN c.is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END ||
        CASE WHEN c.column_default IS NOT NULL THEN ' DEFAULT ' || c.column_default ELSE '' END ||
        CASE WHEN pk.column_name IS NOT NULL THEN ' PRIMARY KEY' ELSE '' END,
        ', '
        ORDER BY c.ordinal_position
    ) as schema_definition
FROM information_schema.tables t
JOIN information_schema.columns c 
    ON t.table_name = c.table_name 
    AND t.table_schema = c.table_schema
LEFT JOIN (
    SELECT ku.table_schema, ku.table_name, ku.column_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage ku 
        ON tc.constraint_type = 'PRIMARY KEY' 
        AND tc.constraint_name = ku.constraint_name
) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name
WHERE t.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
GROUP BY t.table_name
ORDER BY t.table_name;

