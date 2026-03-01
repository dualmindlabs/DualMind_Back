-- 1. Add Energy Columns to public.users
ALTER TABLE public.users
ADD COLUMN IF NOT EXISTS energy_balance INT DEFAULT 20,
ADD COLUMN IF NOT EXISTS last_energy_refill_date DATE DEFAULT CURRENT_DATE,
ADD COLUMN IF NOT EXISTS has_claimed_demo_video BOOLEAN DEFAULT FALSE;

-- 2. Create RPC for atomic energy consumption
-- This prevents race conditions where a user spams the battle button 
-- to bypass the energy check.
CREATE OR REPLACE FUNCTION public.consume_energy(
    user_id_param UUID, 
    amount INT
)
RETURNS BOOLEAN
LANGUAGE plpgsql
SECURITY DEFINER -- Runs as DB admin so users can't bypass via PostgREST
AS $$
DECLARE
    current_balance INT;
BEGIN
    -- Lock the user row for update to prevent concurrent race conditions
    SELECT energy_balance INTO current_balance
    FROM public.users
    WHERE user_id = user_id_param
    FOR UPDATE;

    -- If user not found, fail
    IF NOT FOUND THEN
        RETURN FALSE;
    END IF;

    -- If enough energy, deduct and return true
    IF current_balance >= amount THEN
        UPDATE public.users
        SET energy_balance = energy_balance - amount
        WHERE user_id = user_id_param;
        
        RETURN TRUE;
    ELSE
        -- Not enough energy
        RETURN FALSE;
    END IF;
END;
$$;

-- Grant execute permission to authenticated users
GRANT EXECUTE ON FUNCTION public.consume_energy(UUID, INT) TO authenticated;
