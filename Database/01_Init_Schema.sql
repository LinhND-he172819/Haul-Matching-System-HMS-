-- =========================================================================
-- KHỞI TẠO MÔI TRƯỜNG & EXTENSION (V1.1 ULTIMATE)
-- =========================================================================
-- Loại bỏ uuid-ossp cũ, sử dụng hàm gen_random_uuid() Native của PostgreSQL 13+
CREATE EXTENSION IF NOT EXISTS "postgis";

-- TẠO CÁC SCHEMA (CHUẨN MODULAR MONOLITH)
CREATE SCHEMA identity;
CREATE SCHEMA warehouse;
CREATE SCHEMA transport;
CREATE SCHEMA finance;

-- =========================================================================
-- DOMAIN 1: M1 - IDENTITY MODULE
-- =========================================================================

CREATE TABLE identity.hubs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(150) NOT NULL,
    address TEXT NOT NULL,
    geo_location GEOGRAPHY(Point, 4326) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE identity.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id UUID REFERENCES identity.hubs(id) ON DELETE SET NULL,
    
    full_name VARCHAR(150) NOT NULL,
    avatar_url TEXT,
    phone VARCHAR(20) UNIQUE, 
    email VARCHAR(255) UNIQUE, 
    password_hash VARCHAR(255), 
    google_id VARCHAR(255) UNIQUE, 
    
    reset_password_token VARCHAR(255),
    reset_token_expires_at TIMESTAMPTZ,
    
    role VARCHAR(30) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT chk_user_role CHECK (role IN ('Admin', 'Warehouse_Staff', 'Driver', 'Customer'))
);

CREATE TABLE identity.vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id UUID NOT NULL REFERENCES identity.hubs(id),
    license_plate VARCHAR(20) NOT NULL UNIQUE,
    truck_type VARCHAR(50) NOT NULL, 
    max_weight_kg DECIMAL(10,2) NOT NULL,
    max_volume_cbm DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT chk_positive_weight CHECK (max_weight_kg > 0),
    CONSTRAINT chk_positive_volume CHECK (max_volume_cbm > 0)
);

-- =========================================================================
-- DOMAIN 2: M3 - WAREHOUSE MODULE
-- =========================================================================

CREATE TABLE warehouse.shipments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    qr_code VARCHAR(100) NOT NULL UNIQUE,
    customer_id UUID NOT NULL REFERENCES identity.users(id),
    current_hub_id UUID REFERENCES identity.hubs(id),
    
    cargo_type VARCHAR(100) NOT NULL,
    weight_kg DECIMAL(10,2) NOT NULL,
    volume_cbm DECIMAL(10,2) NOT NULL,
    
    receiver_name VARCHAR(150) NOT NULL,
    receiver_phone VARCHAR(20) NOT NULL,
    dest_address TEXT NOT NULL,
    dest_location GEOGRAPHY(Point, 4326) NOT NULL,
    
    shipping_fee DECIMAL(12,2) NOT NULL DEFAULT 0.0, 
    cod_amount DECIMAL(12,2) NOT NULL DEFAULT 0.0,   
    cancellation_fee DECIMAL(12,2) DEFAULT 0.0,      
    
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    
    CONSTRAINT chk_shipment_status CHECK (status IN (
        'Draft', 'In_Warehouse', 'Matched', 'In_Transit',
        'Delivered', 'Pending_Rescue', 'Delivery_Failed',
        'Cancelled_Return_Pending', 'Returned_To_Hub',
        'Forced_Return', 'Arrived_At_Destination_Hub'
    )),
    CONSTRAINT chk_shipment_weight CHECK (weight_kg > 0),
    CONSTRAINT chk_shipment_volume CHECK (volume_cbm > 0)
);

-- =========================================================================
-- DOMAIN 3: M2 - TRANSPORT MODULE
-- =========================================================================

CREATE TABLE transport.trips (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    driver_id UUID NOT NULL REFERENCES identity.users(id),
    vehicle_id UUID NOT NULL REFERENCES identity.vehicles(id),
    origin_hub_id UUID NOT NULL REFERENCES identity.hubs(id),
    dest_hub_id UUID NOT NULL REFERENCES identity.hubs(id),
    
    route_linestring GEOMETRY(LineString, 4326) NOT NULL,
    current_load_weight DECIMAL(10,2) NOT NULL DEFAULT 0.0,
    current_load_volume DECIMAL(10,2) NOT NULL DEFAULT 0.0,
    
    started_at TIMESTAMPTZ,  
    finished_at TIMESTAMPTZ, 
    
    version INT NOT NULL DEFAULT 1, 
    status VARCHAR(30) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT chk_trip_status CHECK (status IN ('Active', 'Completed', 'Breakdown'))
);

CREATE TABLE transport.trip_shipments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id UUID NOT NULL REFERENCES transport.trips(id),
    shipment_id UUID NOT NULL REFERENCES warehouse.shipments(id), 
    transferred_from_trip_id UUID REFERENCES transport.trips(id), 
    delivery_sequence INT NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_ts_status CHECK (status IN ('Matched', 'In_Transit', 'Delivered', 'Failed'))
);

-- [NÂNG CẤP TỪ DB MỚI] Partial Unique Index: Đảm bảo 1 kiện hàng KHÔNG THỂ bị ghép 
-- cho 2 xe tải cùng lúc nếu nó đang trên đường đi (nhưng cho phép gán lại nếu xe trước đó Failed).
CREATE UNIQUE INDEX ux_active_trip_shipment 
ON transport.trip_shipments(shipment_id) 
WHERE status IN ('Matched', 'In_Transit');

CREATE TABLE transport.off_system_loads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id UUID NOT NULL REFERENCES transport.trips(id),
    weight_kg DECIMAL(10,2) NOT NULL,
    volume_cbm DECIMAL(10,2) NOT NULL,
    dest_address TEXT NOT NULL,
    commission_amt DECIMAL(12,2) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_offload_weight CHECK (weight_kg > 0),
    CONSTRAINT chk_offload_volume CHECK (volume_cbm > 0)
);

CREATE TABLE transport.trip_exceptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id UUID NOT NULL REFERENCES transport.trips(id),
    shipment_id UUID REFERENCES warehouse.shipments(id),
    exception_type VARCHAR(40) NOT NULL,
    reason TEXT NOT NULL,
    evidence_image_url VARCHAR(255),
    lat DECIMAL(9,6),
    lng DECIMAL(9,6),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_exception_type CHECK (exception_type IN (
        'Breakdown', 'Failed_Delivery', 'Signal_Loss',
        'Route_Deviation', 'Prolonged_Stop'
    ))
);

-- [NÂNG CẤP TỪ DB MỚI] Áp dụng Idempotency Key và Timestamp hai chiều chống lỗi mất mạng
CREATE TABLE transport.gps_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id UUID NOT NULL REFERENCES transport.trips(id),
    lat DECIMAL(9,6) NOT NULL,
    lng DECIMAL(9,6) NOT NULL,
    speed DECIMAL(5,2),
    device_timestamp TIMESTAMPTZ NOT NULL, -- Thời gian thực tế máy tài xế ghi nhận
    server_received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), -- Thời gian server nhận được
    idempotency_key VARCHAR(100) UNIQUE -- Mã chống ghi đè/trùng lặp khi app tự động đồng bộ lại lúc có mạng
);

-- =========================================================================
-- DOMAIN 4: FINANCE MODULE
-- =========================================================================

CREATE TABLE finance.driver_salary_periods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    driver_id UUID NOT NULL REFERENCES identity.users(id),
    period_month INT NOT NULL,
    period_year INT NOT NULL,
    base_salary DECIMAL(12,2) NOT NULL DEFAULT 0.0,
    total_trip_bonus DECIMAL(12,2) NOT NULL DEFAULT 0.0,
    total_commission DECIMAL(12,2) NOT NULL DEFAULT 0.0,
    status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_salary_status CHECK (status IN ('Pending', 'Paid')),
    CONSTRAINT uq_salary_period UNIQUE (driver_id, period_month, period_year)
);

CREATE TABLE finance.financial_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.users(id),
    amount DECIMAL(12,2) NOT NULL,
    transaction_type VARCHAR(40) NOT NULL,
    reference_id UUID NOT NULL, 
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_trans_type CHECK (transaction_type IN (
        'Trip_Bonus',
        'Side_Job_Commission',
        'Cancellation_Penalty',
        'External_Freight_Revenue',
        'COD_Collection' 
    ))
);

-- =========================================================================
-- INDEXING
-- =========================================================================

CREATE INDEX idx_hubs_geo ON identity.hubs USING GIST (geo_location);
CREATE INDEX idx_shipments_geo ON warehouse.shipments USING GIST (dest_location);
CREATE INDEX idx_trips_route ON transport.trips USING GIST (route_linestring);

CREATE INDEX idx_users_email ON identity.users (email);
CREATE INDEX idx_users_google_id ON identity.users (google_id);
CREATE INDEX idx_shipments_qr ON warehouse.shipments (qr_code);
CREATE INDEX idx_shipments_status ON warehouse.shipments (status);
CREATE INDEX idx_trips_status ON transport.trips (status);
CREATE INDEX idx_gps_logs_trip_time ON transport.gps_logs (trip_id, device_timestamp DESC);
CREATE INDEX idx_fin_trans_user ON finance.financial_transactions (user_id, transaction_type);

ALTER TABLE transport.trip_shipments
DROP CONSTRAINT chk_ts_status;
ALTER TABLE transport.trip_shipments
ADD CONSTRAINT chk_ts_status CHECK (
   status IN (
       'Suggested',
       'Matched',
       'In_Transit',
       'Delivered',
       'Failed',
       'Rejected'
   )
);
ALTER TABLE transport.trip_shipments
ADD COLUMN suggested_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE transport.trip_shipments
ADD COLUMN responded_at TIMESTAMPTZ;
ALTER TABLE transport.trip_shipments
ADD COLUMN responded_by UUID
REFERENCES identity.users(id);
ALTER TABLE warehouse.shipments
ADD COLUMN special_handling_note TEXT;
CREATE INDEX idx_trip_shipments_trip_status
ON transport.trip_shipments(trip_id, status);



