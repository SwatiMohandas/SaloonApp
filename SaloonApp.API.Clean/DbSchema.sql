-- Database Schema for Saloon Application

-- Users Table
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL CHECK (role IN ('customer', 'owner')),
    phone VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Shops Table
CREATE TABLE IF NOT EXISTS shops (
    id SERIAL PRIMARY KEY,
    owner_id INT REFERENCES users(id),
    name VARCHAR(100) NOT NULL,
    city VARCHAR(100),
    address VARCHAR(255),
    latitude DECIMAL(9,6),
    longitude DECIMAL(9,6),
    rating DECIMAL(2,1) DEFAULT 0,
    is_verified BOOLEAN DEFAULT FALSE,
    image_path VARCHAR(255),
    open_time TIME,
    close_time TIME,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS shop_working_hours (
    id SERIAL PRIMARY KEY,
    shop_id INT REFERENCES shops(id) ON DELETE CASCADE,
    day_of_week INT NOT NULL, -- 0=Sunday, 1=Monday, etc.
    open_time TIME,
    close_time TIME,
    is_closed BOOLEAN DEFAULT FALSE,
    UNIQUE(shop_id, day_of_week)
);

-- Ensure image_path and shop hours exist (Migration)
DO $$ 
BEGIN 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='shops' AND column_name='image_path') THEN 
        ALTER TABLE shops ADD COLUMN image_path VARCHAR(255); 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='shops' AND column_name='open_time') THEN 
        ALTER TABLE shops ADD COLUMN open_time TIME DEFAULT '09:00:00'; 
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='shops' AND column_name='close_time') THEN 
        ALTER TABLE shops ADD COLUMN close_time TIME DEFAULT '21:00:00'; 
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='shops' AND column_name='close_time') THEN 
        ALTER TABLE shops ADD COLUMN close_time TIME DEFAULT '21:00:00'; 
    END IF;
    -- Users Migration for OTP
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='mobile_number') THEN 
        ALTER TABLE users ADD COLUMN mobile_number VARCHAR(15); 
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='otp_code') THEN 
        ALTER TABLE users ADD COLUMN otp_code VARCHAR(6); 
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='otp_expiry') THEN 
        ALTER TABLE users ADD COLUMN otp_expiry TIMESTAMP; 
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='is_mobile_verified') THEN 
        ALTER TABLE users ADD COLUMN is_mobile_verified BOOLEAN DEFAULT FALSE; 
    END IF;
    -- Bookings Migration for Appointments
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='bookings' AND column_name='appointment_time') THEN 
        ALTER TABLE bookings ADD COLUMN appointment_time TIMESTAMP; 
        -- Update Check Constraint for status to include 'scheduled'
        ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_status_check;
        ALTER TABLE bookings ADD CONSTRAINT bookings_status_check CHECK (status IN ('waiting', 'in_chair', 'completed', 'cancelled', 'scheduled'));
    END IF;
END $$;

-- Services Table
CREATE TABLE IF NOT EXISTS services (
    id SERIAL PRIMARY KEY,
    shop_id INT REFERENCES shops(id),
    name VARCHAR(100) NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    duration_mins INT NOT NULL
);

-- Queue/Bookings Table
CREATE TABLE IF NOT EXISTS bookings (
    id SERIAL PRIMARY KEY,
    shop_id INT REFERENCES shops(id),
    user_id INT REFERENCES users(id),
    customer_name VARCHAR(100), -- For walk-ins or manual entry
    status VARCHAR(20) DEFAULT 'waiting' CHECK (status IN ('waiting', 'in_chair', 'completed', 'cancelled', 'scheduled')),
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    appointment_time TIMESTAMP,
    service_id INT REFERENCES services(id)
);

-- Reviews Table
CREATE TABLE IF NOT EXISTS reviews (
    id SERIAL PRIMARY KEY,
    shop_id INT REFERENCES shops(id),
    user_id INT REFERENCES users(id),
    rating INT CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_shops_city ON shops(city);
CREATE INDEX IF NOT EXISTS idx_bookings_shop_status ON bookings(shop_id, status);
CREATE INDEX IF NOT EXISTS idx_reviews_shop_id ON reviews(shop_id);
-- New Performance Indexes
CREATE INDEX IF NOT EXISTS idx_bookings_user_id ON bookings(user_id);
CREATE INDEX IF NOT EXISTS idx_bookings_appointment_time ON bookings(appointment_time);

-- Function: Search Nearby Shops (Haversine Formula)
DROP FUNCTION IF EXISTS get_nearby_shops(DECIMAL, DECIMAL, DECIMAL);
CREATE OR REPLACE FUNCTION get_nearby_shops(lat DECIMAL, lon DECIMAL, radius_km DECIMAL)
RETURNS TABLE (id INT, name VARCHAR, city VARCHAR, rating DECIMAL, distance_km DECIMAL, image_path VARCHAR, open_time TIME, close_time TIME) AS $$
BEGIN
    RETURN QUERY
    SELECT s.id, s.name, s.city, s.rating,
    CAST((6371 * acos(cos(radians(lat)) * cos(radians(s.latitude)) * cos(radians(s.longitude) - radians(lon)) + sin(radians(lat)) * sin(radians(s.latitude)))) AS DECIMAL(10,2)) AS distance_km,
    s.image_path,
    s.open_time,
    s.close_time
    FROM shops s
    WHERE (6371 * acos(cos(radians(lat)) * cos(radians(s.latitude)) * cos(radians(s.longitude) - radians(lon)) + sin(radians(lat)) * sin(radians(s.latitude)))) < radius_km
    ORDER BY distance_km;
END;
$$ LANGUAGE plpgsql;

-- Procedure: Update Queue Status
CREATE OR REPLACE PROCEDURE update_queue_status(p_booking_id INT, p_status VARCHAR)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE bookings 
    SET status = p_status 
    WHERE id = p_booking_id;
END;
$$;
