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
    city VARCHAR(50) NOT NULL,
    address TEXT NOT NULL,
    latitude DECIMAL(9,6),
    longitude DECIMAL(9,6),
    rating DECIMAL(3,2) DEFAULT 0,
    is_verified BOOLEAN DEFAULT FALSE,
    image_path VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

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
    status VARCHAR(20) DEFAULT 'waiting' CHECK (status IN ('waiting', 'in_chair', 'completed', 'cancelled')),
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
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

-- Function: Search Nearby Shops (Haversine Formula)
DROP FUNCTION IF EXISTS get_nearby_shops(DECIMAL, DECIMAL, DECIMAL);
CREATE OR REPLACE FUNCTION get_nearby_shops(lat DECIMAL, lon DECIMAL, radius_km DECIMAL)
RETURNS TABLE (id INT, name VARCHAR, city VARCHAR, rating DECIMAL, distance_km DECIMAL, image_path VARCHAR) AS $$
BEGIN
    RETURN QUERY
    SELECT s.id, s.name, s.city, s.rating,
    CAST((6371 * acos(cos(radians(lat)) * cos(radians(s.latitude)) * cos(radians(s.longitude) - radians(lon)) + sin(radians(lat)) * sin(radians(s.latitude)))) AS DECIMAL(10,2)) AS distance_km,
    s.image_path
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
