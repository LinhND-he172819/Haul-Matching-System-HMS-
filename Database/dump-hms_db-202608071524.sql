--
-- PostgreSQL database dump
--

-- Dumped from database version 15.4 (Debian 15.4-1.pgdg110+1)
-- Dumped by pg_dump version 17.0

-- Started on 2026-08-07 15:24:14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS shipments_intake_confirmed_by_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS shipments_customer_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS shipments_current_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipment_status_history DROP CONSTRAINT IF EXISTS shipment_status_history_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.quotations DROP CONSTRAINT IF EXISTS quotations_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.payments DROP CONSTRAINT IF EXISTS payments_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.payments DROP CONSTRAINT IF EXISTS payments_quotation_id_fkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipment_proposals DROP CONSTRAINT IF EXISTS fk_shipment_proposals_shipment;
ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS fk_shipment_pickedupby;
ALTER TABLE IF EXISTS ONLY transport.vehicles DROP CONSTRAINT IF EXISTS vehicles_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trips DROP CONSTRAINT IF EXISTS trips_origin_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trips DROP CONSTRAINT IF EXISTS trips_driver_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trips DROP CONSTRAINT IF EXISTS trips_dest_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_transferred_from_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_responded_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_rejected_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_cancelled_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_accepted_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_posts DROP CONSTRAINT IF EXISTS trip_posts_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_posts DROP CONSTRAINT IF EXISTS trip_posts_created_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_incidents DROP CONSTRAINT IF EXISTS trip_incidents_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_exceptions DROP CONSTRAINT IF EXISTS trip_exceptions_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_exceptions DROP CONSTRAINT IF EXISTS trip_exceptions_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_trip_post_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_shipment_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_responded_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_customer_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.proposal_logs DROP CONSTRAINT IF EXISTS proposal_logs_proposal_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.proposal_logs DROP CONSTRAINT IF EXISTS proposal_logs_performed_by_fkey;
ALTER TABLE IF EXISTS ONLY transport.off_system_loads DROP CONSTRAINT IF EXISTS off_system_loads_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.gps_logs DROP CONSTRAINT IF EXISTS gps_logs_trip_id_fkey;
ALTER TABLE IF EXISTS ONLY transport.trip_posts DROP CONSTRAINT IF EXISTS fk_trip_posts_hub;
ALTER TABLE IF EXISTS ONLY transport.trips DROP CONSTRAINT IF EXISTS fk_transport_trips_vehicle;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS fk_proposal_reject;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS fk_proposal_accept;
ALTER TABLE IF EXISTS ONLY public.trips DROP CONSTRAINT IF EXISTS "FK_trips_vehicles_VehicleId";
ALTER TABLE IF EXISTS ONLY public.trip_shipments DROP CONSTRAINT IF EXISTS "FK_trip_shipments_trips_TripId";
ALTER TABLE IF EXISTS ONLY public.trip_shipments DROP CONSTRAINT IF EXISTS "FK_trip_shipments_shipments_ShipmentId";
ALTER TABLE IF EXISTS ONLY identity.vehicles DROP CONSTRAINT IF EXISTS vehicles_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY identity.users DROP CONSTRAINT IF EXISTS users_hub_id_fkey;
ALTER TABLE IF EXISTS ONLY finance.financial_transactions DROP CONSTRAINT IF EXISTS financial_transactions_user_id_fkey;
ALTER TABLE IF EXISTS ONLY finance.driver_salary_periods DROP CONSTRAINT IF EXISTS driver_salary_periods_driver_id_fkey;
DROP TRIGGER IF EXISTS trg_prevent_shipment_history_modification ON warehouse.shipment_status_history;
DROP INDEX IF EXISTS warehouse.uq_quotations_active_per_proposal;
DROP INDEX IF EXISTS warehouse.uq_payments_transaction_ref;
DROP INDEX IF EXISTS warehouse.uq_payments_idempotency_key;
DROP INDEX IF EXISTS warehouse.uq_payments_deposit_paid_per_quotation;
DROP INDEX IF EXISTS warehouse.ix_warehouse_shipments_status;
DROP INDEX IF EXISTS warehouse.ix_warehouse_shipments_destination_gist;
DROP INDEX IF EXISTS warehouse.ix_shipment_status_history_shipment_id;
DROP INDEX IF EXISTS warehouse.ix_shipment_proposals_proposal_source;
DROP INDEX IF EXISTS warehouse.ix_shipment_proposals_driver_id;
DROP INDEX IF EXISTS warehouse.idx_shipments_type;
DROP INDEX IF EXISTS warehouse.idx_shipments_status;
DROP INDEX IF EXISTS warehouse.idx_shipments_qr;
DROP INDEX IF EXISTS warehouse.idx_shipments_pickup;
DROP INDEX IF EXISTS warehouse.idx_shipments_geo;
DROP INDEX IF EXISTS warehouse.idx_shipment_proposals_trip_post_id;
DROP INDEX IF EXISTS warehouse.idx_shipment_proposals_status;
DROP INDEX IF EXISTS warehouse.idx_shipment_proposals_shipment_id;
DROP INDEX IF EXISTS warehouse.idx_shipment_proposals_customer_id;
DROP INDEX IF EXISTS warehouse.idx_quotations_status;
DROP INDEX IF EXISTS warehouse.idx_quotations_shipment_id;
DROP INDEX IF EXISTS warehouse.idx_quotations_proposal_id;
DROP INDEX IF EXISTS warehouse.idx_payments_shipment_id;
DROP INDEX IF EXISTS warehouse.idx_payments_quotation_id;
DROP INDEX IF EXISTS warehouse.idx_payments_customer_id;
DROP INDEX IF EXISTS transport.ux_trip_posts_open_trip;
DROP INDEX IF EXISTS transport.ux_transport_vehicles_license_plate;
DROP INDEX IF EXISTS transport.ux_transport_vehicles_code;
DROP INDEX IF EXISTS transport.ux_transport_trip_posts_open_per_trip;
DROP INDEX IF EXISTS transport.ux_transport_active_trip_shipment;
DROP INDEX IF EXISTS transport.ux_pending_proposal_post_shipment;
DROP INDEX IF EXISTS transport.ux_active_trip_shipment;
DROP INDEX IF EXISTS transport.ux_accepted_proposal_shipment;
DROP INDEX IF EXISTS transport.ix_transport_vehicles_status;
DROP INDEX IF EXISTS transport.ix_transport_vehicles_hub_id;
DROP INDEX IF EXISTS transport.ix_transport_trips_vehicle_id;
DROP INDEX IF EXISTS transport.ix_transport_trips_status;
DROP INDEX IF EXISTS transport.ix_transport_trips_route_gist;
DROP INDEX IF EXISTS transport.ix_transport_trips_driver_id;
DROP INDEX IF EXISTS transport.ix_transport_trip_shipments_trip_status;
DROP INDEX IF EXISTS transport.ix_transport_trip_shipments_shipment_status;
DROP INDEX IF EXISTS transport.ix_transport_trip_posts_trip_id;
DROP INDEX IF EXISTS transport.ix_transport_trip_posts_status;
DROP INDEX IF EXISTS transport.ix_transport_trip_posts_created_at;
DROP INDEX IF EXISTS transport.ix_transport_gps_logs_trip_time;
DROP INDEX IF EXISTS transport.idx_trips_status;
DROP INDEX IF EXISTS transport.idx_trips_route;
DROP INDEX IF EXISTS transport.idx_trip_shipments_trip_status;
DROP INDEX IF EXISTS transport.idx_trip_posts_trip;
DROP INDEX IF EXISTS transport.idx_trip_posts_status_accept_until;
DROP INDEX IF EXISTS transport.idx_trip_incidents_trip_id;
DROP INDEX IF EXISTS transport.idx_proposals_trip_post_status;
DROP INDEX IF EXISTS transport.idx_proposals_status;
DROP INDEX IF EXISTS transport.idx_proposals_shipment;
DROP INDEX IF EXISTS transport.idx_proposals_customer_status;
DROP INDEX IF EXISTS transport.idx_proposals_created_at;
DROP INDEX IF EXISTS transport.idx_proposal_logs_proposal_time;
DROP INDEX IF EXISTS transport.idx_proposal_logs_performed_by;
DROP INDEX IF EXISTS transport.idx_gps_logs_trip_time;
DROP INDEX IF EXISTS shared.idx_notifications_user_id;
DROP INDEX IF EXISTS shared.idx_notifications_created_at;
DROP INDEX IF EXISTS shared.idx_audit_log_entity;
DROP INDEX IF EXISTS shared.idx_audit_log_created_at;
DROP INDEX IF EXISTS public.ix_trip_shipments_shipment_status;
DROP INDEX IF EXISTS public.ix_shipments_status;
DROP INDEX IF EXISTS public.ix_shipments_delivery_location_gist;
DROP INDEX IF EXISTS public."IX_trips_VehicleId";
DROP INDEX IF EXISTS public."IX_trips_DriverId";
DROP INDEX IF EXISTS public."IX_trip_shipments_TripId";
DROP INDEX IF EXISTS public."IX_trip_shipments_ShipmentId";
DROP INDEX IF EXISTS identity.idx_users_google_id;
DROP INDEX IF EXISTS identity.idx_users_email;
DROP INDEX IF EXISTS identity.idx_hubs_geo;
DROP INDEX IF EXISTS finance.idx_fin_trans_user;
ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS shipments_qr_code_key;
ALTER TABLE IF EXISTS ONLY warehouse.shipments DROP CONSTRAINT IF EXISTS shipments_pkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipment_status_history DROP CONSTRAINT IF EXISTS shipment_status_history_pkey;
ALTER TABLE IF EXISTS ONLY warehouse.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_pkey;
ALTER TABLE IF EXISTS ONLY warehouse.quotations DROP CONSTRAINT IF EXISTS quotations_pkey;
ALTER TABLE IF EXISTS ONLY warehouse.payments DROP CONSTRAINT IF EXISTS payments_pkey;
ALTER TABLE IF EXISTS ONLY transport.vehicles DROP CONSTRAINT IF EXISTS vehicles_pkey;
ALTER TABLE IF EXISTS ONLY transport.trips DROP CONSTRAINT IF EXISTS trips_pkey;
ALTER TABLE IF EXISTS ONLY transport.trip_shipments DROP CONSTRAINT IF EXISTS trip_shipments_pkey;
ALTER TABLE IF EXISTS ONLY transport.trip_posts DROP CONSTRAINT IF EXISTS trip_posts_pkey;
ALTER TABLE IF EXISTS ONLY transport.trip_incidents DROP CONSTRAINT IF EXISTS trip_incidents_pkey;
ALTER TABLE IF EXISTS ONLY transport.trip_exceptions DROP CONSTRAINT IF EXISTS trip_exceptions_pkey;
ALTER TABLE IF EXISTS ONLY transport.shipment_proposals DROP CONSTRAINT IF EXISTS shipment_proposals_pkey;
ALTER TABLE IF EXISTS ONLY transport.proposal_logs DROP CONSTRAINT IF EXISTS proposal_logs_pkey;
ALTER TABLE IF EXISTS ONLY transport.off_system_loads DROP CONSTRAINT IF EXISTS off_system_loads_pkey;
ALTER TABLE IF EXISTS ONLY transport.gps_logs DROP CONSTRAINT IF EXISTS gps_logs_pkey;
ALTER TABLE IF EXISTS ONLY transport.gps_logs DROP CONSTRAINT IF EXISTS gps_logs_idempotency_key_key;
ALTER TABLE IF EXISTS ONLY shared.notifications DROP CONSTRAINT IF EXISTS notifications_pkey;
ALTER TABLE IF EXISTS ONLY shared.audit_log DROP CONSTRAINT IF EXISTS audit_log_pkey;
ALTER TABLE IF EXISTS ONLY public.hubs DROP CONSTRAINT IF EXISTS hubs_pkey;
ALTER TABLE IF EXISTS ONLY public.vehicles DROP CONSTRAINT IF EXISTS "PK_vehicles";
ALTER TABLE IF EXISTS ONLY public.trips DROP CONSTRAINT IF EXISTS "PK_trips";
ALTER TABLE IF EXISTS ONLY public.trip_shipments DROP CONSTRAINT IF EXISTS "PK_trip_shipments";
ALTER TABLE IF EXISTS ONLY public.shipments DROP CONSTRAINT IF EXISTS "PK_shipments";
ALTER TABLE IF EXISTS ONLY public."__EFMigrationsHistory" DROP CONSTRAINT IF EXISTS "PK___EFMigrationsHistory";
ALTER TABLE IF EXISTS ONLY identity.vehicles DROP CONSTRAINT IF EXISTS vehicles_pkey;
ALTER TABLE IF EXISTS ONLY identity.vehicles DROP CONSTRAINT IF EXISTS vehicles_license_plate_key;
ALTER TABLE IF EXISTS ONLY identity.users DROP CONSTRAINT IF EXISTS users_pkey;
ALTER TABLE IF EXISTS ONLY identity.users DROP CONSTRAINT IF EXISTS users_phone_key;
ALTER TABLE IF EXISTS ONLY identity.users DROP CONSTRAINT IF EXISTS users_google_id_key;
ALTER TABLE IF EXISTS ONLY identity.users DROP CONSTRAINT IF EXISTS users_email_key;
ALTER TABLE IF EXISTS ONLY identity.hubs DROP CONSTRAINT IF EXISTS hubs_pkey;
ALTER TABLE IF EXISTS ONLY finance.driver_salary_periods DROP CONSTRAINT IF EXISTS uq_salary_period;
ALTER TABLE IF EXISTS ONLY finance.financial_transactions DROP CONSTRAINT IF EXISTS financial_transactions_pkey;
ALTER TABLE IF EXISTS ONLY finance.driver_salary_periods DROP CONSTRAINT IF EXISTS driver_salary_periods_pkey;
DROP TABLE IF EXISTS warehouse.shipments;
DROP TABLE IF EXISTS warehouse.shipment_status_history;
DROP TABLE IF EXISTS warehouse.shipment_proposals;
DROP TABLE IF EXISTS warehouse.quotations;
DROP TABLE IF EXISTS warehouse.payments;
DROP TABLE IF EXISTS transport.vehicles;
DROP TABLE IF EXISTS transport.trips;
DROP TABLE IF EXISTS transport.trip_shipments;
DROP TABLE IF EXISTS transport.trip_posts;
DROP TABLE IF EXISTS transport.trip_incidents;
DROP TABLE IF EXISTS transport.trip_exceptions;
DROP TABLE IF EXISTS transport.shipment_proposals;
DROP TABLE IF EXISTS transport.proposal_logs;
DROP TABLE IF EXISTS transport.off_system_loads;
DROP TABLE IF EXISTS transport.gps_logs;
DROP TABLE IF EXISTS shared.notifications;
DROP TABLE IF EXISTS shared.audit_log;
DROP TABLE IF EXISTS public.vehicles;
DROP TABLE IF EXISTS public.trips;
DROP TABLE IF EXISTS public.trip_shipments;
DROP TABLE IF EXISTS public.shipments;
DROP TABLE IF EXISTS public.hubs;
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
DROP TABLE IF EXISTS identity.vehicles;
DROP TABLE IF EXISTS identity.users;
DROP TABLE IF EXISTS identity.hubs;
DROP TABLE IF EXISTS finance.financial_transactions;
DROP TABLE IF EXISTS finance.driver_salary_periods;
DROP FUNCTION IF EXISTS warehouse.prevent_shipment_history_modification();
DROP SCHEMA IF EXISTS warehouse;
DROP SCHEMA IF EXISTS transport;
DROP SCHEMA IF EXISTS topology;
DROP SCHEMA IF EXISTS tiger_data;
DROP SCHEMA IF EXISTS tiger;
DROP SCHEMA IF EXISTS shared;
DROP SCHEMA IF EXISTS public;
DROP SCHEMA IF EXISTS identity;
DROP SCHEMA IF EXISTS finance;
--
-- TOC entry 17 (class 2615 OID 19655)
-- Name: finance; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA finance;


ALTER SCHEMA finance OWNER TO postgres;

--
-- TOC entry 14 (class 2615 OID 19652)
-- Name: identity; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA identity;


ALTER SCHEMA identity OWNER TO postgres;

--
-- TOC entry 9 (class 2615 OID 2200)
-- Name: public; Type: SCHEMA; Schema: -; Owner: pg_database_owner
--

CREATE SCHEMA public;


ALTER SCHEMA public OWNER TO pg_database_owner;

--
-- TOC entry 5077 (class 0 OID 0)
-- Dependencies: 9
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: pg_database_owner
--

COMMENT ON SCHEMA public IS 'standard public schema';


--
-- TOC entry 18 (class 2615 OID 158970)
-- Name: shared; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA shared;


ALTER SCHEMA shared OWNER TO postgres;

--
-- TOC entry 12 (class 2615 OID 19226)
-- Name: tiger; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA tiger;


ALTER SCHEMA tiger OWNER TO postgres;

--
-- TOC entry 13 (class 2615 OID 19482)
-- Name: tiger_data; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA tiger_data;


ALTER SCHEMA tiger_data OWNER TO postgres;

--
-- TOC entry 11 (class 2615 OID 19052)
-- Name: topology; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA topology;


ALTER SCHEMA topology OWNER TO postgres;

--
-- TOC entry 5078 (class 0 OID 0)
-- Dependencies: 11
-- Name: SCHEMA topology; Type: COMMENT; Schema: -; Owner: postgres
--

COMMENT ON SCHEMA topology IS 'PostGIS Topology schema';


--
-- TOC entry 16 (class 2615 OID 19654)
-- Name: transport; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA transport;


ALTER SCHEMA transport OWNER TO postgres;

--
-- TOC entry 15 (class 2615 OID 19653)
-- Name: warehouse; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA warehouse;


ALTER SCHEMA warehouse OWNER TO postgres;

--
-- TOC entry 1208 (class 1255 OID 93374)
-- Name: prevent_shipment_history_modification(); Type: FUNCTION; Schema: warehouse; Owner: postgres
--

CREATE FUNCTION warehouse.prevent_shipment_history_modification() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
            BEGIN
                RAISE EXCEPTION 'Shipment status history is append-only. Updates and deletes are not allowed.';
                RETURN NULL;
            END;
            $$;


ALTER FUNCTION warehouse.prevent_shipment_history_modification() OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 298 (class 1259 OID 19849)
-- Name: driver_salary_periods; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.driver_salary_periods (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    driver_id uuid NOT NULL,
    period_month integer NOT NULL,
    period_year integer NOT NULL,
    base_salary numeric(12,2) DEFAULT 0.0 NOT NULL,
    total_trip_bonus numeric(12,2) DEFAULT 0.0 NOT NULL,
    total_commission numeric(12,2) DEFAULT 0.0 NOT NULL,
    status character varying(30) DEFAULT 'Pending'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_salary_status CHECK (((status)::text = ANY ((ARRAY['Pending'::character varying, 'Paid'::character varying])::text[])))
);


ALTER TABLE finance.driver_salary_periods OWNER TO postgres;

--
-- TOC entry 299 (class 1259 OID 19869)
-- Name: financial_transactions; Type: TABLE; Schema: finance; Owner: postgres
--

CREATE TABLE finance.financial_transactions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    amount numeric(12,2) NOT NULL,
    transaction_type character varying(40) NOT NULL,
    reference_id uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_trans_type CHECK (((transaction_type)::text = ANY ((ARRAY['Trip_Bonus'::character varying, 'Side_Job_Commission'::character varying, 'Cancellation_Penalty'::character varying, 'External_Freight_Revenue'::character varying, 'COD_Collection'::character varying])::text[])))
);


ALTER TABLE finance.financial_transactions OWNER TO postgres;

--
-- TOC entry 289 (class 1259 OID 19656)
-- Name: hubs; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.hubs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(150) NOT NULL,
    address text NOT NULL,
    geo_location public.geography(Point,4326),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


ALTER TABLE identity.hubs OWNER TO postgres;

--
-- TOC entry 290 (class 1259 OID 19667)
-- Name: users; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.users (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    hub_id uuid,
    full_name character varying(150) NOT NULL,
    avatar_url text,
    phone character varying(20),
    email character varying(255),
    password_hash character varying(255),
    google_id character varying(255),
    reset_password_token character varying(255),
    reset_token_expires_at timestamp with time zone,
    role character varying(30) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    refresh_token text,
    refresh_token_expiry_time timestamp with time zone,
    CONSTRAINT chk_user_role CHECK (((role)::text = ANY ((ARRAY['Admin'::character varying, 'Warehouse_Staff'::character varying, 'Driver'::character varying, 'Customer'::character varying])::text[])))
);


ALTER TABLE identity.users OWNER TO postgres;

--
-- TOC entry 291 (class 1259 OID 19690)
-- Name: vehicles; Type: TABLE; Schema: identity; Owner: postgres
--

CREATE TABLE identity.vehicles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    hub_id uuid NOT NULL,
    license_plate character varying(20) NOT NULL,
    truck_type character varying(50) NOT NULL,
    max_weight_kg numeric(10,2) NOT NULL,
    max_volume_cbm numeric(10,2) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    CONSTRAINT chk_positive_volume CHECK ((max_volume_cbm > (0)::numeric)),
    CONSTRAINT chk_positive_weight CHECK ((max_weight_kg > (0)::numeric))
);


ALTER TABLE identity.vehicles OWNER TO postgres;

--
-- TOC entry 300 (class 1259 OID 27818)
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- TOC entry 305 (class 1259 OID 36050)
-- Name: hubs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.hubs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name text NOT NULL,
    address text NOT NULL,
    geo_location public.geography(Point,4326),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    is_deleted boolean DEFAULT false
);


ALTER TABLE public.hubs OWNER TO postgres;

--
-- TOC entry 301 (class 1259 OID 27823)
-- Name: shipments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.shipments (
    "Id" uuid NOT NULL,
    "ReceiverName" text,
    "ReceiverPhone" text,
    "DestAddress" text,
    "WeightKg" numeric NOT NULL,
    "VolumeCbm" numeric NOT NULL,
    "CargoType" text,
    "SpecialHandlingNote" text,
    "Status" text,
    delivery_location public.geometry(Point,4326)
);


ALTER TABLE public.shipments OWNER TO postgres;

--
-- TOC entry 304 (class 1259 OID 27849)
-- Name: trip_shipments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.trip_shipments (
    "Id" uuid NOT NULL,
    "TripId" uuid NOT NULL,
    "ShipmentId" uuid NOT NULL,
    "DeliverySequence" integer NOT NULL,
    "Status" text,
    "SuggestedAt" timestamp with time zone,
    "RespondedAt" timestamp with time zone,
    "RespondedBy" uuid
);


ALTER TABLE public.trip_shipments OWNER TO postgres;

--
-- TOC entry 303 (class 1259 OID 27837)
-- Name: trips; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.trips (
    "Id" uuid NOT NULL,
    "DriverId" uuid NOT NULL,
    "VehicleId" uuid NOT NULL,
    "CurrentLoadWeight" numeric NOT NULL,
    "CurrentLoadVolume" numeric NOT NULL,
    "Status" text,
    "Version" bytea
);


ALTER TABLE public.trips OWNER TO postgres;

--
-- TOC entry 302 (class 1259 OID 27830)
-- Name: vehicles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.vehicles (
    "Id" uuid NOT NULL,
    "MaxWeightKg" numeric NOT NULL,
    "MaxVolumeCbm" numeric NOT NULL
);


ALTER TABLE public.vehicles OWNER TO postgres;

--
-- TOC entry 315 (class 1259 OID 158971)
-- Name: audit_log; Type: TABLE; Schema: shared; Owner: postgres
--

CREATE TABLE shared.audit_log (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    entity_type text NOT NULL,
    entity_id uuid NOT NULL,
    action text NOT NULL,
    performed_by uuid,
    details jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE shared.audit_log OWNER TO postgres;

--
-- TOC entry 316 (class 1259 OID 183486)
-- Name: notifications; Type: TABLE; Schema: shared; Owner: postgres
--

CREATE TABLE shared.notifications (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    title text NOT NULL,
    message text NOT NULL,
    entity_type text,
    entity_id uuid,
    is_read boolean DEFAULT false,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE shared.notifications OWNER TO postgres;

--
-- TOC entry 297 (class 1259 OID 19835)
-- Name: gps_logs; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.gps_logs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    lat numeric(9,6) NOT NULL,
    lng numeric(9,6) NOT NULL,
    speed numeric(5,2),
    device_timestamp timestamp with time zone NOT NULL,
    server_received_at timestamp with time zone DEFAULT now() NOT NULL,
    idempotency_key character varying(100)
);


ALTER TABLE transport.gps_logs OWNER TO postgres;

--
-- TOC entry 295 (class 1259 OID 19799)
-- Name: off_system_loads; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.off_system_loads (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    weight_kg numeric(10,2) NOT NULL,
    volume_cbm numeric(10,2) NOT NULL,
    dest_address text NOT NULL,
    commission_amt numeric(12,2) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_offload_volume CHECK ((volume_cbm > (0)::numeric)),
    CONSTRAINT chk_offload_weight CHECK ((weight_kg > (0)::numeric))
);


ALTER TABLE transport.off_system_loads OWNER TO postgres;

--
-- TOC entry 309 (class 1259 OID 77037)
-- Name: proposal_logs; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.proposal_logs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    proposal_id uuid NOT NULL,
    action character varying(30) NOT NULL,
    performed_by uuid,
    note text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_proposal_log_action CHECK (((action)::text = ANY ((ARRAY['Created'::character varying, 'Viewed'::character varying, 'Accepted'::character varying, 'Rejected'::character varying, 'Cancelled'::character varying, 'Expired'::character varying])::text[])))
);


ALTER TABLE transport.proposal_logs OWNER TO postgres;

--
-- TOC entry 308 (class 1259 OID 76998)
-- Name: shipment_proposals; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.shipment_proposals (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_post_id uuid NOT NULL,
    shipment_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    status character varying(30) DEFAULT 'Pending'::character varying NOT NULL,
    customer_note text,
    rejection_reason text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    responded_at timestamp with time zone,
    responded_by uuid,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    accepted_at timestamp with time zone,
    accepted_by uuid,
    rejected_at timestamp with time zone,
    rejected_by uuid,
    reject_reason text,
    CONSTRAINT chk_proposal_response_data CHECK (((((status)::text = 'Pending'::text) AND (responded_at IS NULL) AND (responded_by IS NULL)) OR (((status)::text = ANY ((ARRAY['Accepted'::character varying, 'Rejected'::character varying])::text[])) AND (responded_at IS NOT NULL) AND (responded_by IS NOT NULL)) OR ((status)::text = ANY ((ARRAY['Cancelled'::character varying, 'Expired'::character varying])::text[])))),
    CONSTRAINT chk_shipment_proposal_status CHECK (((status)::text = ANY ((ARRAY['Pending'::character varying, 'Accepted'::character varying, 'Rejected'::character varying, 'Cancelled'::character varying, 'Expired'::character varying])::text[])))
);


ALTER TABLE transport.shipment_proposals OWNER TO postgres;

--
-- TOC entry 296 (class 1259 OID 19815)
-- Name: trip_exceptions; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.trip_exceptions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    shipment_id uuid,
    exception_type character varying(40) NOT NULL,
    reason text NOT NULL,
    evidence_image_url character varying(255),
    lat numeric(9,6),
    lng numeric(9,6),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_exception_type CHECK (((exception_type)::text = ANY ((ARRAY['Breakdown'::character varying, 'Failed_Delivery'::character varying, 'Signal_Loss'::character varying, 'Route_Deviation'::character varying, 'Prolonged_Stop'::character varying])::text[])))
);


ALTER TABLE transport.trip_exceptions OWNER TO postgres;

--
-- TOC entry 314 (class 1259 OID 158955)
-- Name: trip_incidents; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.trip_incidents (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    shipment_id uuid,
    reported_by uuid NOT NULL,
    incident_type text NOT NULL,
    description text NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE transport.trip_incidents OWNER TO postgres;

--
-- TOC entry 307 (class 1259 OID 76970)
-- Name: trip_posts; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.trip_posts (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    created_by uuid NOT NULL,
    title character varying(200) NOT NULL,
    description text,
    accept_until timestamp with time zone NOT NULL,
    status character varying(30) DEFAULT 'Open'::character varying NOT NULL,
    published_at timestamp with time zone DEFAULT now() NOT NULL,
    closed_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    pickup_mode character varying(20) DEFAULT 'Hub'::character varying NOT NULL,
    created_by_staff_hub uuid,
    origin text,
    destination text,
    departure_time timestamp with time zone,
    max_weight numeric(12,2),
    max_volume numeric(12,2),
    CONSTRAINT chk_trip_post_accept_until CHECK ((accept_until > created_at)),
    CONSTRAINT chk_trip_post_status CHECK (((status)::text = ANY ((ARRAY['Open'::character varying, 'Closed'::character varying, 'Expired'::character varying, 'Cancelled'::character varying])::text[])))
);


ALTER TABLE transport.trip_posts OWNER TO postgres;

--
-- TOC entry 294 (class 1259 OID 19774)
-- Name: trip_shipments; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.trip_shipments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    trip_id uuid NOT NULL,
    shipment_id uuid NOT NULL,
    transferred_from_trip_id uuid,
    delivery_sequence integer NOT NULL,
    status character varying(30) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    suggested_at timestamp with time zone DEFAULT now() NOT NULL,
    responded_at timestamp with time zone,
    responded_by uuid,
    message text,
    accepted_at timestamp with time zone,
    accepted_by uuid,
    rejected_at timestamp with time zone,
    rejected_by uuid,
    reject_reason text,
    cancelled_at timestamp with time zone,
    cancelled_by uuid,
    expired_at timestamp with time zone,
    is_deleted boolean DEFAULT false NOT NULL,
    CONSTRAINT chk_ts_status CHECK (((status)::text = ANY ((ARRAY['Suggested'::character varying, 'Matched'::character varying, 'In_Transit'::character varying, 'Delivered'::character varying, 'Failed'::character varying, 'Rejected'::character varying])::text[])))
);


ALTER TABLE transport.trip_shipments OWNER TO postgres;

--
-- TOC entry 293 (class 1259 OID 19738)
-- Name: trips; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.trips (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    driver_id uuid NOT NULL,
    vehicle_id uuid NOT NULL,
    origin_hub_id uuid NOT NULL,
    dest_hub_id uuid NOT NULL,
    route_linestring public.geometry(LineString,4326) NOT NULL,
    current_load_weight numeric(10,2) DEFAULT 0.0 NOT NULL,
    current_load_volume numeric(10,2) DEFAULT 0.0 NOT NULL,
    started_at timestamp with time zone,
    finished_at timestamp with time zone,
    version integer DEFAULT 1 NOT NULL,
    status character varying(30) DEFAULT 'Active'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    started_at_v2 timestamp with time zone,
    completed_at timestamp with time zone,
    trip_code text,
    CONSTRAINT chk_trip_status CHECK (((status)::text = ANY ((ARRAY['Active'::character varying, 'Completed'::character varying, 'Breakdown'::character varying])::text[])))
);


ALTER TABLE transport.trips OWNER TO postgres;

--
-- TOC entry 306 (class 1259 OID 44202)
-- Name: vehicles; Type: TABLE; Schema: transport; Owner: postgres
--

CREATE TABLE transport.vehicles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code text NOT NULL,
    license_plate text NOT NULL,
    hub_id uuid NOT NULL,
    vehicle_type text NOT NULL,
    max_weight_kg numeric(12,2) NOT NULL,
    max_volume_cbm numeric(12,2) NOT NULL,
    status text DEFAULT 'Available'::text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    CONSTRAINT ck_transport_vehicles_volume_positive CHECK ((max_volume_cbm > (0)::numeric)),
    CONSTRAINT ck_transport_vehicles_weight_positive CHECK ((max_weight_kg > (0)::numeric))
);


ALTER TABLE transport.vehicles OWNER TO postgres;

--
-- TOC entry 313 (class 1259 OID 158931)
-- Name: payments; Type: TABLE; Schema: warehouse; Owner: postgres
--

CREATE TABLE warehouse.payments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    shipment_id uuid NOT NULL,
    quotation_id uuid,
    payment_code text NOT NULL,
    amount numeric(12,2) NOT NULL,
    payment_type text NOT NULL,
    status text DEFAULT 'Pending'::text NOT NULL,
    transaction_ref text,
    paid_at timestamp with time zone,
    created_by uuid,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    customer_id uuid DEFAULT '00000000-0000-0000-0000-000000000000'::uuid NOT NULL,
    currency text DEFAULT 'VND'::text NOT NULL,
    payment_method text,
    transaction_reference text,
    idempotency_key text,
    confirmed_at timestamp with time zone,
    confirmed_by uuid,
    failure_reason text,
    row_version bytea
);


ALTER TABLE warehouse.payments OWNER TO postgres;

--
-- TOC entry 312 (class 1259 OID 158910)
-- Name: quotations; Type: TABLE; Schema: warehouse; Owner: postgres
--

CREATE TABLE warehouse.quotations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    shipment_id uuid NOT NULL,
    proposal_id uuid,
    quotation_code text NOT NULL,
    shipping_fee numeric(12,2) DEFAULT 0 NOT NULL,
    deposit_amount numeric(12,2) DEFAULT 0 NOT NULL,
    remaining_amount numeric(12,2) DEFAULT 0 NOT NULL,
    status text DEFAULT 'Draft'::text NOT NULL,
    sent_at timestamp with time zone,
    expires_at timestamp with time zone,
    created_by uuid,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    currency text DEFAULT 'VND'::text,
    accepted_at timestamp with time zone,
    quoted_by uuid,
    quoted_at timestamp with time zone,
    cancelled_at timestamp with time zone,
    expired_at timestamp with time zone
);


ALTER TABLE warehouse.quotations OWNER TO postgres;

--
-- TOC entry 311 (class 1259 OID 142521)
-- Name: shipment_proposals; Type: TABLE; Schema: warehouse; Owner: postgres
--

CREATE TABLE warehouse.shipment_proposals (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    shipment_id uuid NOT NULL,
    trip_post_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    sender_name character varying(200) DEFAULT ''::character varying NOT NULL,
    sender_phone character varying(20) DEFAULT ''::character varying NOT NULL,
    pickup_address character varying(500) DEFAULT ''::character varying NOT NULL,
    pickup_latitude double precision,
    pickup_longitude double precision,
    pickup_note character varying(500),
    status character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    cancelled_at timestamp with time zone,
    accepted_at timestamp with time zone,
    accepted_by uuid,
    rejected_at timestamp with time zone,
    rejected_by uuid,
    reject_reason text,
    expired_at timestamp with time zone,
    reviewed_at timestamp with time zone,
    reviewed_by uuid,
    approved_at timestamp with time zone,
    approved_by uuid,
    reviewer_id uuid,
    reviewer_name text,
    is_deleted boolean DEFAULT false,
    proposal_source text DEFAULT 'Customer'::text NOT NULL,
    driver_id uuid,
    requested_trip_id uuid,
    CONSTRAINT chk_proposal_source_customer CHECK (((proposal_source <> 'Customer'::text) OR ((trip_post_id IS NOT NULL) AND (customer_id IS NOT NULL) AND (requested_trip_id IS NULL)))),
    CONSTRAINT chk_proposal_source_driver CHECK (((proposal_source <> 'Driver'::text) OR ((requested_trip_id IS NOT NULL) AND (driver_id IS NOT NULL) AND (trip_post_id IS NULL) AND (customer_id IS NULL))))
);


ALTER TABLE warehouse.shipment_proposals OWNER TO postgres;

--
-- TOC entry 310 (class 1259 OID 93359)
-- Name: shipment_status_history; Type: TABLE; Schema: warehouse; Owner: postgres
--

CREATE TABLE warehouse.shipment_status_history (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    shipment_id uuid NOT NULL,
    from_status text NOT NULL,
    to_status text NOT NULL,
    performed_by uuid,
    reason text,
    occurred_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE warehouse.shipment_status_history OWNER TO postgres;

--
-- TOC entry 292 (class 1259 OID 19708)
-- Name: shipments; Type: TABLE; Schema: warehouse; Owner: postgres
--

CREATE TABLE warehouse.shipments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    qr_code character varying(100) NOT NULL,
    customer_id uuid NOT NULL,
    current_hub_id uuid,
    cargo_type character varying(100) NOT NULL,
    weight_kg numeric(10,2) NOT NULL,
    volume_cbm numeric(10,2) NOT NULL,
    receiver_name character varying(150) NOT NULL,
    receiver_phone character varying(20) NOT NULL,
    dest_address text NOT NULL,
    dest_location public.geography(Point,4326) NOT NULL,
    shipping_fee numeric(12,2) DEFAULT 0.0 NOT NULL,
    cod_amount numeric(12,2) DEFAULT 0.0 NOT NULL,
    cancellation_fee numeric(12,2) DEFAULT 0.0,
    status character varying(50) DEFAULT 'Draft'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    special_handling_note text,
    intake_confirmed_by uuid,
    intake_confirmed_at timestamp with time zone,
    sender_name character varying(255),
    sender_phone character varying(30),
    pickup_address text,
    pickup_latitude numeric(10,7),
    pickup_longitude numeric(10,7),
    pickup_note text,
    shipment_type character varying(30) DEFAULT 'Hub'::character varying NOT NULL,
    picked_up_at timestamp with time zone,
    picked_up_by uuid,
    cancel_reason text,
    cancelled_at timestamp with time zone,
    cancelled_by uuid,
    delivered_at timestamp with time zone,
    delivered_by uuid,
    delivery_note text,
    proof_image_url text,
    dest_address_name text,
    dest_latitude double precision,
    dest_longitude double precision,
    commodity text,
    shipment_code text,
    CONSTRAINT chk_shipment_status CHECK (((status)::text = ANY ((ARRAY['Draft'::character varying, 'PendingReview'::character varying, 'PendingDeposit'::character varying, 'In_Warehouse'::character varying, 'Matched'::character varying, 'In_Transit'::character varying, 'Delivered'::character varying, 'Pending_Rescue'::character varying, 'Delivery_Failed'::character varying, 'Cancelled_Return_Pending'::character varying, 'Returned_To_Hub'::character varying, 'Forced_Return'::character varying, 'Arrived_At_Destination_Hub'::character varying, 'Cancelled'::character varying, 'Completed'::character varying])::text[]))),
    CONSTRAINT chk_shipment_volume CHECK ((volume_cbm > (0)::numeric)),
    CONSTRAINT chk_shipment_weight CHECK ((weight_kg > (0)::numeric))
);


ALTER TABLE warehouse.shipments OWNER TO postgres;

--
-- TOC entry 5079 (class 0 OID 0)
-- Dependencies: 292
-- Name: COLUMN shipments.shipment_type; Type: COMMENT; Schema: warehouse; Owner: postgres
--

COMMENT ON COLUMN warehouse.shipments.shipment_type IS 'Hub = Qua kho, DirectPickup = Giao trực tiếp cho tài xế';


--
-- TOC entry 4791 (class 2606 OID 19861)
-- Name: driver_salary_periods driver_salary_periods_pkey; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.driver_salary_periods
    ADD CONSTRAINT driver_salary_periods_pkey PRIMARY KEY (id);


--
-- TOC entry 4795 (class 2606 OID 19876)
-- Name: financial_transactions financial_transactions_pkey; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.financial_transactions
    ADD CONSTRAINT financial_transactions_pkey PRIMARY KEY (id);


--
-- TOC entry 4793 (class 2606 OID 19863)
-- Name: driver_salary_periods uq_salary_period; Type: CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.driver_salary_periods
    ADD CONSTRAINT uq_salary_period UNIQUE (driver_id, period_month, period_year);


--
-- TOC entry 4738 (class 2606 OID 19666)
-- Name: hubs hubs_pkey; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.hubs
    ADD CONSTRAINT hubs_pkey PRIMARY KEY (id);


--
-- TOC entry 4743 (class 2606 OID 19682)
-- Name: users users_email_key; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- TOC entry 4745 (class 2606 OID 19684)
-- Name: users users_google_id_key; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_google_id_key UNIQUE (google_id);


--
-- TOC entry 4747 (class 2606 OID 19680)
-- Name: users users_phone_key; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_phone_key UNIQUE (phone);


--
-- TOC entry 4749 (class 2606 OID 19678)
-- Name: users users_pkey; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- TOC entry 4751 (class 2606 OID 19702)
-- Name: vehicles vehicles_license_plate_key; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.vehicles
    ADD CONSTRAINT vehicles_license_plate_key UNIQUE (license_plate);


--
-- TOC entry 4753 (class 2606 OID 19700)
-- Name: vehicles vehicles_pkey; Type: CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.vehicles
    ADD CONSTRAINT vehicles_pkey PRIMARY KEY (id);


--
-- TOC entry 4798 (class 2606 OID 27822)
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- TOC entry 4800 (class 2606 OID 27829)
-- Name: shipments PK_shipments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.shipments
    ADD CONSTRAINT "PK_shipments" PRIMARY KEY ("Id");


--
-- TOC entry 4812 (class 2606 OID 27855)
-- Name: trip_shipments PK_trip_shipments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trip_shipments
    ADD CONSTRAINT "PK_trip_shipments" PRIMARY KEY ("Id");


--
-- TOC entry 4808 (class 2606 OID 27843)
-- Name: trips PK_trips; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trips
    ADD CONSTRAINT "PK_trips" PRIMARY KEY ("Id");


--
-- TOC entry 4804 (class 2606 OID 27836)
-- Name: vehicles PK_vehicles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.vehicles
    ADD CONSTRAINT "PK_vehicles" PRIMARY KEY ("Id");


--
-- TOC entry 4815 (class 2606 OID 36060)
-- Name: hubs hubs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.hubs
    ADD CONSTRAINT hubs_pkey PRIMARY KEY (id);


--
-- TOC entry 4873 (class 2606 OID 158979)
-- Name: audit_log audit_log_pkey; Type: CONSTRAINT; Schema: shared; Owner: postgres
--

ALTER TABLE ONLY shared.audit_log
    ADD CONSTRAINT audit_log_pkey PRIMARY KEY (id);


--
-- TOC entry 4879 (class 2606 OID 183496)
-- Name: notifications notifications_pkey; Type: CONSTRAINT; Schema: shared; Owner: postgres
--

ALTER TABLE ONLY shared.notifications
    ADD CONSTRAINT notifications_pkey PRIMARY KEY (id);


--
-- TOC entry 4785 (class 2606 OID 19843)
-- Name: gps_logs gps_logs_idempotency_key_key; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.gps_logs
    ADD CONSTRAINT gps_logs_idempotency_key_key UNIQUE (idempotency_key);


--
-- TOC entry 4787 (class 2606 OID 19841)
-- Name: gps_logs gps_logs_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.gps_logs
    ADD CONSTRAINT gps_logs_pkey PRIMARY KEY (id);


--
-- TOC entry 4781 (class 2606 OID 19809)
-- Name: off_system_loads off_system_loads_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.off_system_loads
    ADD CONSTRAINT off_system_loads_pkey PRIMARY KEY (id);


--
-- TOC entry 4843 (class 2606 OID 77046)
-- Name: proposal_logs proposal_logs_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.proposal_logs
    ADD CONSTRAINT proposal_logs_pkey PRIMARY KEY (id);


--
-- TOC entry 4837 (class 2606 OID 77010)
-- Name: shipment_proposals shipment_proposals_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT shipment_proposals_pkey PRIMARY KEY (id);


--
-- TOC entry 4783 (class 2606 OID 19824)
-- Name: trip_exceptions trip_exceptions_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_exceptions
    ADD CONSTRAINT trip_exceptions_pkey PRIMARY KEY (id);


--
-- TOC entry 4871 (class 2606 OID 158963)
-- Name: trip_incidents trip_incidents_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_incidents
    ADD CONSTRAINT trip_incidents_pkey PRIMARY KEY (id);


--
-- TOC entry 4828 (class 2606 OID 76984)
-- Name: trip_posts trip_posts_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_posts
    ADD CONSTRAINT trip_posts_pkey PRIMARY KEY (id);


--
-- TOC entry 4777 (class 2606 OID 19782)
-- Name: trip_shipments trip_shipments_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_pkey PRIMARY KEY (id);


--
-- TOC entry 4772 (class 2606 OID 19753)
-- Name: trips trips_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trips
    ADD CONSTRAINT trips_pkey PRIMARY KEY (id);


--
-- TOC entry 4821 (class 2606 OID 44215)
-- Name: vehicles vehicles_pkey; Type: CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.vehicles
    ADD CONSTRAINT vehicles_pkey PRIMARY KEY (id);


--
-- TOC entry 4865 (class 2606 OID 158942)
-- Name: payments payments_pkey; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.payments
    ADD CONSTRAINT payments_pkey PRIMARY KEY (id);


--
-- TOC entry 4859 (class 2606 OID 158924)
-- Name: quotations quotations_pkey; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.quotations
    ADD CONSTRAINT quotations_pkey PRIMARY KEY (id);


--
-- TOC entry 4854 (class 2606 OID 142533)
-- Name: shipment_proposals shipment_proposals_pkey; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipment_proposals
    ADD CONSTRAINT shipment_proposals_pkey PRIMARY KEY (id);


--
-- TOC entry 4846 (class 2606 OID 93367)
-- Name: shipment_status_history shipment_status_history_pkey; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipment_status_history
    ADD CONSTRAINT shipment_status_history_pkey PRIMARY KEY (id);


--
-- TOC entry 4762 (class 2606 OID 19725)
-- Name: shipments shipments_pkey; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT shipments_pkey PRIMARY KEY (id);


--
-- TOC entry 4764 (class 2606 OID 19727)
-- Name: shipments shipments_qr_code_key; Type: CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT shipments_qr_code_key UNIQUE (qr_code);


--
-- TOC entry 4796 (class 1259 OID 19891)
-- Name: idx_fin_trans_user; Type: INDEX; Schema: finance; Owner: postgres
--

CREATE INDEX idx_fin_trans_user ON finance.financial_transactions USING btree (user_id, transaction_type);


--
-- TOC entry 4739 (class 1259 OID 19882)
-- Name: idx_hubs_geo; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX idx_hubs_geo ON identity.hubs USING gist (geo_location);


--
-- TOC entry 4740 (class 1259 OID 19885)
-- Name: idx_users_email; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX idx_users_email ON identity.users USING btree (email);


--
-- TOC entry 4741 (class 1259 OID 19886)
-- Name: idx_users_google_id; Type: INDEX; Schema: identity; Owner: postgres
--

CREATE INDEX idx_users_google_id ON identity.users USING btree (google_id);


--
-- TOC entry 4809 (class 1259 OID 27866)
-- Name: IX_trip_shipments_ShipmentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_trip_shipments_ShipmentId" ON public.trip_shipments USING btree ("ShipmentId");


--
-- TOC entry 4810 (class 1259 OID 27867)
-- Name: IX_trip_shipments_TripId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_trip_shipments_TripId" ON public.trip_shipments USING btree ("TripId");


--
-- TOC entry 4805 (class 1259 OID 27868)
-- Name: IX_trips_DriverId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_trips_DriverId" ON public.trips USING btree ("DriverId");


--
-- TOC entry 4806 (class 1259 OID 27869)
-- Name: IX_trips_VehicleId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_trips_VehicleId" ON public.trips USING btree ("VehicleId");


--
-- TOC entry 4801 (class 1259 OID 36010)
-- Name: ix_shipments_delivery_location_gist; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_shipments_delivery_location_gist ON public.shipments USING gist (delivery_location) WHERE (delivery_location IS NOT NULL);


--
-- TOC entry 4802 (class 1259 OID 36011)
-- Name: ix_shipments_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_shipments_status ON public.shipments USING btree ("Status");


--
-- TOC entry 4813 (class 1259 OID 36012)
-- Name: ix_trip_shipments_shipment_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_trip_shipments_shipment_status ON public.trip_shipments USING btree ("ShipmentId", "Status");


--
-- TOC entry 4874 (class 1259 OID 158981)
-- Name: idx_audit_log_created_at; Type: INDEX; Schema: shared; Owner: postgres
--

CREATE INDEX idx_audit_log_created_at ON shared.audit_log USING btree (created_at DESC);


--
-- TOC entry 4875 (class 1259 OID 158980)
-- Name: idx_audit_log_entity; Type: INDEX; Schema: shared; Owner: postgres
--

CREATE INDEX idx_audit_log_entity ON shared.audit_log USING btree (entity_type, entity_id);


--
-- TOC entry 4876 (class 1259 OID 183498)
-- Name: idx_notifications_created_at; Type: INDEX; Schema: shared; Owner: postgres
--

CREATE INDEX idx_notifications_created_at ON shared.notifications USING btree (created_at);


--
-- TOC entry 4877 (class 1259 OID 183497)
-- Name: idx_notifications_user_id; Type: INDEX; Schema: shared; Owner: postgres
--

CREATE INDEX idx_notifications_user_id ON shared.notifications USING btree (user_id);


--
-- TOC entry 4788 (class 1259 OID 19890)
-- Name: idx_gps_logs_trip_time; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_gps_logs_trip_time ON transport.gps_logs USING btree (trip_id, device_timestamp DESC);


--
-- TOC entry 4840 (class 1259 OID 77058)
-- Name: idx_proposal_logs_performed_by; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposal_logs_performed_by ON transport.proposal_logs USING btree (performed_by);


--
-- TOC entry 4841 (class 1259 OID 77057)
-- Name: idx_proposal_logs_proposal_time; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposal_logs_proposal_time ON transport.proposal_logs USING btree (proposal_id, created_at DESC);


--
-- TOC entry 4831 (class 1259 OID 77036)
-- Name: idx_proposals_created_at; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposals_created_at ON transport.shipment_proposals USING btree (created_at DESC);


--
-- TOC entry 4832 (class 1259 OID 77034)
-- Name: idx_proposals_customer_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposals_customer_status ON transport.shipment_proposals USING btree (customer_id, status);


--
-- TOC entry 4833 (class 1259 OID 77035)
-- Name: idx_proposals_shipment; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposals_shipment ON transport.shipment_proposals USING btree (shipment_id);


--
-- TOC entry 4834 (class 1259 OID 117948)
-- Name: idx_proposals_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposals_status ON transport.shipment_proposals USING btree (status);


--
-- TOC entry 4835 (class 1259 OID 77033)
-- Name: idx_proposals_trip_post_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_proposals_trip_post_status ON transport.shipment_proposals USING btree (trip_post_id, status);


--
-- TOC entry 4869 (class 1259 OID 158969)
-- Name: idx_trip_incidents_trip_id; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trip_incidents_trip_id ON transport.trip_incidents USING btree (trip_id);


--
-- TOC entry 4822 (class 1259 OID 76996)
-- Name: idx_trip_posts_status_accept_until; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trip_posts_status_accept_until ON transport.trip_posts USING btree (status, accept_until) WHERE (is_deleted = false);


--
-- TOC entry 4823 (class 1259 OID 76997)
-- Name: idx_trip_posts_trip; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trip_posts_trip ON transport.trip_posts USING btree (trip_id);


--
-- TOC entry 4773 (class 1259 OID 19899)
-- Name: idx_trip_shipments_trip_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trip_shipments_trip_status ON transport.trip_shipments USING btree (trip_id, status);


--
-- TOC entry 4765 (class 1259 OID 19884)
-- Name: idx_trips_route; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trips_route ON transport.trips USING gist (route_linestring);


--
-- TOC entry 4766 (class 1259 OID 19889)
-- Name: idx_trips_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX idx_trips_status ON transport.trips USING btree (status);


--
-- TOC entry 4789 (class 1259 OID 44236)
-- Name: ix_transport_gps_logs_trip_time; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_gps_logs_trip_time ON transport.gps_logs USING btree (trip_id, device_timestamp DESC);


--
-- TOC entry 4824 (class 1259 OID 85169)
-- Name: ix_transport_trip_posts_created_at; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trip_posts_created_at ON transport.trip_posts USING btree (created_at DESC) WHERE (is_deleted = false);


--
-- TOC entry 4825 (class 1259 OID 85168)
-- Name: ix_transport_trip_posts_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trip_posts_status ON transport.trip_posts USING btree (status) WHERE (is_deleted = false);


--
-- TOC entry 4826 (class 1259 OID 85167)
-- Name: ix_transport_trip_posts_trip_id; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trip_posts_trip_id ON transport.trip_posts USING btree (trip_id) WHERE (is_deleted = false);


--
-- TOC entry 4774 (class 1259 OID 44239)
-- Name: ix_transport_trip_shipments_shipment_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trip_shipments_shipment_status ON transport.trip_shipments USING btree (shipment_id, status);


--
-- TOC entry 4775 (class 1259 OID 44235)
-- Name: ix_transport_trip_shipments_trip_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trip_shipments_trip_status ON transport.trip_shipments USING btree (trip_id, status);


--
-- TOC entry 4767 (class 1259 OID 44225)
-- Name: ix_transport_trips_driver_id; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trips_driver_id ON transport.trips USING btree (driver_id);


--
-- TOC entry 4768 (class 1259 OID 44228)
-- Name: ix_transport_trips_route_gist; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trips_route_gist ON transport.trips USING gist (route_linestring) WHERE (is_deleted = false);


--
-- TOC entry 4769 (class 1259 OID 44227)
-- Name: ix_transport_trips_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trips_status ON transport.trips USING btree (status) WHERE (is_deleted = false);


--
-- TOC entry 4770 (class 1259 OID 44226)
-- Name: ix_transport_trips_vehicle_id; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_trips_vehicle_id ON transport.trips USING btree (vehicle_id);


--
-- TOC entry 4816 (class 1259 OID 44223)
-- Name: ix_transport_vehicles_hub_id; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_vehicles_hub_id ON transport.vehicles USING btree (hub_id) WHERE (is_deleted = false);


--
-- TOC entry 4817 (class 1259 OID 44224)
-- Name: ix_transport_vehicles_status; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE INDEX ix_transport_vehicles_status ON transport.vehicles USING btree (status) WHERE (is_deleted = false);


--
-- TOC entry 4838 (class 1259 OID 77032)
-- Name: ux_accepted_proposal_shipment; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_accepted_proposal_shipment ON transport.shipment_proposals USING btree (shipment_id) WHERE ((status)::text = 'Accepted'::text);


--
-- TOC entry 4778 (class 1259 OID 19798)
-- Name: ux_active_trip_shipment; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_active_trip_shipment ON transport.trip_shipments USING btree (shipment_id) WHERE ((status)::text = ANY ((ARRAY['Matched'::character varying, 'In_Transit'::character varying])::text[]));


--
-- TOC entry 4839 (class 1259 OID 77031)
-- Name: ux_pending_proposal_post_shipment; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_pending_proposal_post_shipment ON transport.shipment_proposals USING btree (trip_post_id, shipment_id) WHERE ((status)::text = 'Pending'::text);


--
-- TOC entry 4779 (class 1259 OID 44234)
-- Name: ux_transport_active_trip_shipment; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_transport_active_trip_shipment ON transport.trip_shipments USING btree (shipment_id) WHERE ((status)::text = ANY ((ARRAY['Suggested'::character varying, 'Matched'::character varying, 'In_Transit'::character varying])::text[]));


--
-- TOC entry 4829 (class 1259 OID 85170)
-- Name: ux_transport_trip_posts_open_per_trip; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_transport_trip_posts_open_per_trip ON transport.trip_posts USING btree (trip_id) WHERE (((status)::text = 'Open'::text) AND (is_deleted = false));


--
-- TOC entry 4818 (class 1259 OID 44221)
-- Name: ux_transport_vehicles_code; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_transport_vehicles_code ON transport.vehicles USING btree (code) WHERE (is_deleted = false);


--
-- TOC entry 4819 (class 1259 OID 44222)
-- Name: ux_transport_vehicles_license_plate; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_transport_vehicles_license_plate ON transport.vehicles USING btree (license_plate) WHERE (is_deleted = false);


--
-- TOC entry 4830 (class 1259 OID 76995)
-- Name: ux_trip_posts_open_trip; Type: INDEX; Schema: transport; Owner: postgres
--

CREATE UNIQUE INDEX ux_trip_posts_open_trip ON transport.trip_posts USING btree (trip_id) WHERE (((status)::text = 'Open'::text) AND (is_deleted = false));


--
-- TOC entry 4861 (class 1259 OID 167123)
-- Name: idx_payments_customer_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_payments_customer_id ON warehouse.payments USING btree (customer_id);


--
-- TOC entry 4862 (class 1259 OID 158954)
-- Name: idx_payments_quotation_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_payments_quotation_id ON warehouse.payments USING btree (quotation_id);


--
-- TOC entry 4863 (class 1259 OID 158953)
-- Name: idx_payments_shipment_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_payments_shipment_id ON warehouse.payments USING btree (shipment_id);


--
-- TOC entry 4855 (class 1259 OID 167108)
-- Name: idx_quotations_proposal_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_quotations_proposal_id ON warehouse.quotations USING btree (proposal_id);


--
-- TOC entry 4856 (class 1259 OID 158930)
-- Name: idx_quotations_shipment_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_quotations_shipment_id ON warehouse.quotations USING btree (shipment_id);


--
-- TOC entry 4857 (class 1259 OID 167109)
-- Name: idx_quotations_status; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_quotations_status ON warehouse.quotations USING btree (status);


--
-- TOC entry 4847 (class 1259 OID 142541)
-- Name: idx_shipment_proposals_customer_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipment_proposals_customer_id ON warehouse.shipment_proposals USING btree (customer_id);


--
-- TOC entry 4848 (class 1259 OID 142539)
-- Name: idx_shipment_proposals_shipment_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipment_proposals_shipment_id ON warehouse.shipment_proposals USING btree (shipment_id);


--
-- TOC entry 4849 (class 1259 OID 142542)
-- Name: idx_shipment_proposals_status; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipment_proposals_status ON warehouse.shipment_proposals USING btree (status);


--
-- TOC entry 4850 (class 1259 OID 142540)
-- Name: idx_shipment_proposals_trip_post_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipment_proposals_trip_post_id ON warehouse.shipment_proposals USING btree (trip_post_id);


--
-- TOC entry 4754 (class 1259 OID 19883)
-- Name: idx_shipments_geo; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipments_geo ON warehouse.shipments USING gist (dest_location);


--
-- TOC entry 4755 (class 1259 OID 117947)
-- Name: idx_shipments_pickup; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipments_pickup ON warehouse.shipments USING btree (picked_up_at);


--
-- TOC entry 4756 (class 1259 OID 19887)
-- Name: idx_shipments_qr; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipments_qr ON warehouse.shipments USING btree (qr_code);


--
-- TOC entry 4757 (class 1259 OID 19888)
-- Name: idx_shipments_status; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipments_status ON warehouse.shipments USING btree (status);


--
-- TOC entry 4758 (class 1259 OID 117946)
-- Name: idx_shipments_type; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX idx_shipments_type ON warehouse.shipments USING btree (shipment_type);


--
-- TOC entry 4851 (class 1259 OID 257205)
-- Name: ix_shipment_proposals_driver_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX ix_shipment_proposals_driver_id ON warehouse.shipment_proposals USING btree (driver_id) WHERE (driver_id IS NOT NULL);


--
-- TOC entry 4852 (class 1259 OID 257206)
-- Name: ix_shipment_proposals_proposal_source; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX ix_shipment_proposals_proposal_source ON warehouse.shipment_proposals USING btree (proposal_source);


--
-- TOC entry 4844 (class 1259 OID 93373)
-- Name: ix_shipment_status_history_shipment_id; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX ix_shipment_status_history_shipment_id ON warehouse.shipment_status_history USING btree (shipment_id, occurred_at DESC);


--
-- TOC entry 4759 (class 1259 OID 44237)
-- Name: ix_warehouse_shipments_destination_gist; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX ix_warehouse_shipments_destination_gist ON warehouse.shipments USING gist (dest_location) WHERE (dest_location IS NOT NULL);


--
-- TOC entry 4760 (class 1259 OID 44238)
-- Name: ix_warehouse_shipments_status; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE INDEX ix_warehouse_shipments_status ON warehouse.shipments USING btree (status);


--
-- TOC entry 4866 (class 1259 OID 167126)
-- Name: uq_payments_deposit_paid_per_quotation; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE UNIQUE INDEX uq_payments_deposit_paid_per_quotation ON warehouse.payments USING btree (quotation_id) WHERE ((payment_type = 'Deposit'::text) AND (status = 'Paid'::text) AND (is_deleted = false));


--
-- TOC entry 4867 (class 1259 OID 167125)
-- Name: uq_payments_idempotency_key; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE UNIQUE INDEX uq_payments_idempotency_key ON warehouse.payments USING btree (idempotency_key) WHERE (idempotency_key IS NOT NULL);


--
-- TOC entry 4868 (class 1259 OID 167124)
-- Name: uq_payments_transaction_ref; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE UNIQUE INDEX uq_payments_transaction_ref ON warehouse.payments USING btree (transaction_reference) WHERE (transaction_reference IS NOT NULL);


--
-- TOC entry 4860 (class 1259 OID 167110)
-- Name: uq_quotations_active_per_proposal; Type: INDEX; Schema: warehouse; Owner: postgres
--

CREATE UNIQUE INDEX uq_quotations_active_per_proposal ON warehouse.quotations USING btree (proposal_id) WHERE ((status = ANY (ARRAY['Draft'::text, 'Sent'::text])) AND (is_deleted = false));


--
-- TOC entry 4924 (class 2620 OID 93375)
-- Name: shipment_status_history trg_prevent_shipment_history_modification; Type: TRIGGER; Schema: warehouse; Owner: postgres
--

CREATE TRIGGER trg_prevent_shipment_history_modification BEFORE DELETE OR UPDATE ON warehouse.shipment_status_history FOR EACH ROW EXECUTE FUNCTION warehouse.prevent_shipment_history_modification();


--
-- TOC entry 4901 (class 2606 OID 19864)
-- Name: driver_salary_periods driver_salary_periods_driver_id_fkey; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.driver_salary_periods
    ADD CONSTRAINT driver_salary_periods_driver_id_fkey FOREIGN KEY (driver_id) REFERENCES identity.users(id);


--
-- TOC entry 4902 (class 2606 OID 19877)
-- Name: financial_transactions financial_transactions_user_id_fkey; Type: FK CONSTRAINT; Schema: finance; Owner: postgres
--

ALTER TABLE ONLY finance.financial_transactions
    ADD CONSTRAINT financial_transactions_user_id_fkey FOREIGN KEY (user_id) REFERENCES identity.users(id);


--
-- TOC entry 4880 (class 2606 OID 19685)
-- Name: users users_hub_id_fkey; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_hub_id_fkey FOREIGN KEY (hub_id) REFERENCES identity.hubs(id) ON DELETE SET NULL;


--
-- TOC entry 4881 (class 2606 OID 19703)
-- Name: vehicles vehicles_hub_id_fkey; Type: FK CONSTRAINT; Schema: identity; Owner: postgres
--

ALTER TABLE ONLY identity.vehicles
    ADD CONSTRAINT vehicles_hub_id_fkey FOREIGN KEY (hub_id) REFERENCES identity.hubs(id);


--
-- TOC entry 4904 (class 2606 OID 27856)
-- Name: trip_shipments FK_trip_shipments_shipments_ShipmentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trip_shipments
    ADD CONSTRAINT "FK_trip_shipments_shipments_ShipmentId" FOREIGN KEY ("ShipmentId") REFERENCES public.shipments("Id") ON DELETE RESTRICT;


--
-- TOC entry 4905 (class 2606 OID 27861)
-- Name: trip_shipments FK_trip_shipments_trips_TripId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trip_shipments
    ADD CONSTRAINT "FK_trip_shipments_trips_TripId" FOREIGN KEY ("TripId") REFERENCES public.trips("Id") ON DELETE CASCADE;


--
-- TOC entry 4903 (class 2606 OID 27844)
-- Name: trips FK_trips_vehicles_VehicleId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trips
    ADD CONSTRAINT "FK_trips_vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES public.vehicles("Id") ON DELETE RESTRICT;


--
-- TOC entry 4910 (class 2606 OID 117936)
-- Name: shipment_proposals fk_proposal_accept; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT fk_proposal_accept FOREIGN KEY (accepted_by) REFERENCES identity.users(id);


--
-- TOC entry 4911 (class 2606 OID 117941)
-- Name: shipment_proposals fk_proposal_reject; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT fk_proposal_reject FOREIGN KEY (rejected_by) REFERENCES identity.users(id);


--
-- TOC entry 4886 (class 2606 OID 265416)
-- Name: trips fk_transport_trips_vehicle; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trips
    ADD CONSTRAINT fk_transport_trips_vehicle FOREIGN KEY (vehicle_id) REFERENCES transport.vehicles(id);


--
-- TOC entry 4907 (class 2606 OID 183481)
-- Name: trip_posts fk_trip_posts_hub; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_posts
    ADD CONSTRAINT fk_trip_posts_hub FOREIGN KEY (created_by_staff_hub) REFERENCES identity.hubs(id) ON DELETE SET NULL;


--
-- TOC entry 4900 (class 2606 OID 19844)
-- Name: gps_logs gps_logs_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.gps_logs
    ADD CONSTRAINT gps_logs_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id);


--
-- TOC entry 4897 (class 2606 OID 19810)
-- Name: off_system_loads off_system_loads_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.off_system_loads
    ADD CONSTRAINT off_system_loads_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id);


--
-- TOC entry 4916 (class 2606 OID 77052)
-- Name: proposal_logs proposal_logs_performed_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.proposal_logs
    ADD CONSTRAINT proposal_logs_performed_by_fkey FOREIGN KEY (performed_by) REFERENCES identity.users(id);


--
-- TOC entry 4917 (class 2606 OID 77047)
-- Name: proposal_logs proposal_logs_proposal_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.proposal_logs
    ADD CONSTRAINT proposal_logs_proposal_id_fkey FOREIGN KEY (proposal_id) REFERENCES transport.shipment_proposals(id) ON DELETE CASCADE;


--
-- TOC entry 4912 (class 2606 OID 77021)
-- Name: shipment_proposals shipment_proposals_customer_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT shipment_proposals_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES identity.users(id);


--
-- TOC entry 4913 (class 2606 OID 77026)
-- Name: shipment_proposals shipment_proposals_responded_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT shipment_proposals_responded_by_fkey FOREIGN KEY (responded_by) REFERENCES identity.users(id);


--
-- TOC entry 4914 (class 2606 OID 77016)
-- Name: shipment_proposals shipment_proposals_shipment_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT shipment_proposals_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id);


--
-- TOC entry 4915 (class 2606 OID 77011)
-- Name: shipment_proposals shipment_proposals_trip_post_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.shipment_proposals
    ADD CONSTRAINT shipment_proposals_trip_post_id_fkey FOREIGN KEY (trip_post_id) REFERENCES transport.trip_posts(id) ON DELETE CASCADE;


--
-- TOC entry 4898 (class 2606 OID 19830)
-- Name: trip_exceptions trip_exceptions_shipment_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_exceptions
    ADD CONSTRAINT trip_exceptions_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id);


--
-- TOC entry 4899 (class 2606 OID 19825)
-- Name: trip_exceptions trip_exceptions_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_exceptions
    ADD CONSTRAINT trip_exceptions_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id);


--
-- TOC entry 4923 (class 2606 OID 158964)
-- Name: trip_incidents trip_incidents_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_incidents
    ADD CONSTRAINT trip_incidents_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id) ON DELETE RESTRICT;


--
-- TOC entry 4908 (class 2606 OID 76990)
-- Name: trip_posts trip_posts_created_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_posts
    ADD CONSTRAINT trip_posts_created_by_fkey FOREIGN KEY (created_by) REFERENCES identity.users(id);


--
-- TOC entry 4909 (class 2606 OID 76985)
-- Name: trip_posts trip_posts_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_posts
    ADD CONSTRAINT trip_posts_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id) ON DELETE CASCADE;


--
-- TOC entry 4890 (class 2606 OID 117962)
-- Name: trip_shipments trip_shipments_accepted_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_accepted_by_fkey FOREIGN KEY (accepted_by) REFERENCES identity.users(id);


--
-- TOC entry 4891 (class 2606 OID 117972)
-- Name: trip_shipments trip_shipments_cancelled_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_cancelled_by_fkey FOREIGN KEY (cancelled_by) REFERENCES identity.users(id);


--
-- TOC entry 4892 (class 2606 OID 117967)
-- Name: trip_shipments trip_shipments_rejected_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_rejected_by_fkey FOREIGN KEY (rejected_by) REFERENCES identity.users(id);


--
-- TOC entry 4893 (class 2606 OID 19894)
-- Name: trip_shipments trip_shipments_responded_by_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_responded_by_fkey FOREIGN KEY (responded_by) REFERENCES identity.users(id);


--
-- TOC entry 4894 (class 2606 OID 19788)
-- Name: trip_shipments trip_shipments_shipment_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id);


--
-- TOC entry 4895 (class 2606 OID 19793)
-- Name: trip_shipments trip_shipments_transferred_from_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_transferred_from_trip_id_fkey FOREIGN KEY (transferred_from_trip_id) REFERENCES transport.trips(id);


--
-- TOC entry 4896 (class 2606 OID 19783)
-- Name: trip_shipments trip_shipments_trip_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trip_shipments
    ADD CONSTRAINT trip_shipments_trip_id_fkey FOREIGN KEY (trip_id) REFERENCES transport.trips(id);


--
-- TOC entry 4887 (class 2606 OID 19769)
-- Name: trips trips_dest_hub_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trips
    ADD CONSTRAINT trips_dest_hub_id_fkey FOREIGN KEY (dest_hub_id) REFERENCES identity.hubs(id);


--
-- TOC entry 4888 (class 2606 OID 19754)
-- Name: trips trips_driver_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trips
    ADD CONSTRAINT trips_driver_id_fkey FOREIGN KEY (driver_id) REFERENCES identity.users(id);


--
-- TOC entry 4889 (class 2606 OID 19764)
-- Name: trips trips_origin_hub_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.trips
    ADD CONSTRAINT trips_origin_hub_id_fkey FOREIGN KEY (origin_hub_id) REFERENCES identity.hubs(id);


--
-- TOC entry 4906 (class 2606 OID 44216)
-- Name: vehicles vehicles_hub_id_fkey; Type: FK CONSTRAINT; Schema: transport; Owner: postgres
--

ALTER TABLE ONLY transport.vehicles
    ADD CONSTRAINT vehicles_hub_id_fkey FOREIGN KEY (hub_id) REFERENCES identity.hubs(id);


--
-- TOC entry 4882 (class 2606 OID 117931)
-- Name: shipments fk_shipment_pickedupby; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT fk_shipment_pickedupby FOREIGN KEY (picked_up_by) REFERENCES identity.users(id);


--
-- TOC entry 4919 (class 2606 OID 142534)
-- Name: shipment_proposals fk_shipment_proposals_shipment; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipment_proposals
    ADD CONSTRAINT fk_shipment_proposals_shipment FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id) ON DELETE RESTRICT;


--
-- TOC entry 4921 (class 2606 OID 158948)
-- Name: payments payments_quotation_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.payments
    ADD CONSTRAINT payments_quotation_id_fkey FOREIGN KEY (quotation_id) REFERENCES warehouse.quotations(id);


--
-- TOC entry 4922 (class 2606 OID 158943)
-- Name: payments payments_shipment_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.payments
    ADD CONSTRAINT payments_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id) ON DELETE RESTRICT;


--
-- TOC entry 4920 (class 2606 OID 158925)
-- Name: quotations quotations_shipment_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.quotations
    ADD CONSTRAINT quotations_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id) ON DELETE RESTRICT;


--
-- TOC entry 4918 (class 2606 OID 93368)
-- Name: shipment_status_history shipment_status_history_shipment_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipment_status_history
    ADD CONSTRAINT shipment_status_history_shipment_id_fkey FOREIGN KEY (shipment_id) REFERENCES warehouse.shipments(id);


--
-- TOC entry 4883 (class 2606 OID 19733)
-- Name: shipments shipments_current_hub_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT shipments_current_hub_id_fkey FOREIGN KEY (current_hub_id) REFERENCES identity.hubs(id);


--
-- TOC entry 4884 (class 2606 OID 19728)
-- Name: shipments shipments_customer_id_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT shipments_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES identity.users(id);


--
-- TOC entry 4885 (class 2606 OID 36061)
-- Name: shipments shipments_intake_confirmed_by_fkey; Type: FK CONSTRAINT; Schema: warehouse; Owner: postgres
--

ALTER TABLE ONLY warehouse.shipments
    ADD CONSTRAINT shipments_intake_confirmed_by_fkey FOREIGN KEY (intake_confirmed_by) REFERENCES identity.users(id);


-- Completed on 2026-08-07 15:24:17

--
-- PostgreSQL database dump complete
--

