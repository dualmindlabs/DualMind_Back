-- =====================================================
-- COMPLETE DATABASE SCHEMA DUMP
-- =====================================================
-- Run this ONE query to get everything about your database
-- Copy the entire result and share it with me

WITH table_info AS (
    SELECT 
        t.table_schema,
        t.table_name,
        c.column_name,
        c.ordinal_position,
        c.data_type,
        c.character_maximum_length,
        c.numeric_precision,
        c.numeric_scale,
        c.is_nullable,
        c.column_default,
        CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_primary_key,
        CASE WHEN fk.column_name IS NOT NULL THEN 
            fk.foreign_table_schema || '.' || fk.foreign_table_name || '(' || fk.foreign_column_name || ')'
        ELSE NULL END as foreign_key_reference
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
            tc.table_schema,
            kcu.table_name,
            kcu.column_name,
            ccu.table_schema AS foreign_table_schema,
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
)
SELECT 
    table_name as "TABLE",
    column_name as "COLUMN",
    ordinal_position as "POS",
    data_type as "TYPE",
    COALESCE(
        CASE 
            WHEN character_maximum_length IS NOT NULL 
            THEN data_type || '(' || character_maximum_length || ')'
            WHEN numeric_precision IS NOT NULL 
            THEN data_type || '(' || numeric_precision || 
                 CASE WHEN numeric_scale > 0 THEN ',' || numeric_scale ELSE '' END || ')'
            ELSE data_type
        END,
        data_type
    ) as "FULL_TYPE",
    is_nullable as "NULLABLE",
    column_default as "DEFAULT",
    is_primary_key as "PK",
    foreign_key_reference as "FK_REFERENCE"
FROM table_info
ORDER BY table_name, ordinal_position;

-- =====================================================
-- ALTERNATIVE: JSON FORMAT (Easier to copy/paste)
-- =====================================================
-- Run this if you want JSON output instead

SELECT jsonb_pretty(
    jsonb_agg(
        jsonb_build_object(
            'table', table_name,
            'columns', (
                SELECT jsonb_agg(
                    jsonb_build_object(
                        'name', column_name,
                        'position', ordinal_position,
                        'type', data_type,
                        'full_type', COALESCE(
                            CASE 
                                WHEN character_maximum_length IS NOT NULL 
                                THEN data_type || '(' || character_maximum_length || ')'
                                WHEN numeric_precision IS NOT NULL 
                                THEN data_type || '(' || numeric_precision || 
                                     CASE WHEN numeric_scale > 0 THEN ',' || numeric_scale ELSE '' END || ')'
                                ELSE data_type
                            END,
                            data_type
                        ),
                        'nullable', is_nullable = 'YES',
                        'default', column_default,
                        'is_primary_key', CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END,
                        'foreign_key', CASE WHEN fk.column_name IS NOT NULL THEN 
                            jsonb_build_object(
                                'table', fk.foreign_table_name,
                                'column', fk.foreign_column_name
                            )
                        ELSE NULL END
                    ) ORDER BY ordinal_position
                )
                FROM information_schema.columns c2
                LEFT JOIN (
                    SELECT ku.table_schema, ku.table_name, ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku 
                        ON tc.constraint_type = 'PRIMARY KEY' 
                        AND tc.constraint_name = ku.constraint_name
                        AND tc.table_schema = ku.table_schema
                ) pk ON c2.table_name = pk.table_name 
                    AND c2.table_schema = pk.table_schema 
                    AND c2.column_name = pk.column_name
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
                ) fk ON c2.table_name = fk.table_name
                    AND c2.table_schema = fk.table_schema
                    AND c2.column_name = fk.column_name
                WHERE c2.table_schema = 'public'
                    AND c2.table_name = t.table_name
            ),
            'indexes', (
                SELECT jsonb_agg(
                    jsonb_build_object(
                        'name', indexname,
                        'definition', indexdef
                    )
                )
                FROM pg_indexes
                WHERE schemaname = 'public'
                    AND tablename = t.table_name
            ),
            'constraints', (
                SELECT jsonb_agg(
                    jsonb_build_object(
                        'name', constraint_name,
                        'type', constraint_type
                    )
                )
                FROM information_schema.table_constraints
                WHERE table_schema = 'public'
                    AND table_name = t.table_name
            )
        )
    )
) as complete_schema
FROM information_schema.tables t
WHERE t.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
ORDER BY t.table_name;

-- =====================================================
-- SIMPLE VERSION (Just the essentials)
-- =====================================================
-- If the above queries are too complex, use this simpler one:

SELECT 
    t.table_name,
    c.column_name,
    c.data_type,
    CASE WHEN c.character_maximum_length IS NOT NULL 
        THEN c.data_type || '(' || c.character_maximum_length || ')'
        ELSE c.data_type END as full_type,
    c.is_nullable,
    c.column_default,
    CASE WHEN pk.column_name IS NOT NULL THEN 'PK' ELSE '' END as key,
    CASE WHEN fk.column_name IS NOT NULL 
        THEN 'FK→' || fk.foreign_table_name || '.' || fk.foreign_column_name
        ELSE '' END as references
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
) pk ON c.table_name = pk.table_name AND c.table_schema = pk.table_schema AND c.column_name = pk.column_name
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
    JOIN information_schema.constraint_column_usage ccu
        ON ccu.constraint_name = tc.constraint_name
    WHERE tc.constraint_type = 'FOREIGN KEY'
) fk ON c.table_name = fk.table_name AND c.table_schema = fk.table_schema AND c.column_name = fk.column_name
WHERE t.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
ORDER BY t.table_name, c.ordinal_position;

