CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE shipments (
    "Id" uuid NOT NULL,
    "ReceiverName" text,
    "ReceiverPhone" text,
    "DestAddress" text,
    "WeightKg" numeric NOT NULL,
    "VolumeCbm" numeric NOT NULL,
    "CargoType" text,
    "SpecialHandlingNote" text,
    "Status" text,
    CONSTRAINT "PK_shipments" PRIMARY KEY ("Id")
);

CREATE TABLE vehicles (
    "Id" uuid NOT NULL,
    "MaxWeightKg" numeric NOT NULL,
    "MaxVolumeCbm" numeric NOT NULL,
    CONSTRAINT "PK_vehicles" PRIMARY KEY ("Id")
);

CREATE TABLE trips (
    "Id" uuid NOT NULL,
    "DriverId" uuid NOT NULL,
    "VehicleId" uuid NOT NULL,
    "CurrentLoadWeight" numeric NOT NULL,
    "CurrentLoadVolume" numeric NOT NULL,
    "Status" text,
    "Version" bytea,
    CONSTRAINT "PK_trips" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_trips_vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES vehicles ("Id") ON DELETE RESTRICT
);

CREATE TABLE trip_shipments (
    "Id" uuid NOT NULL,
    "TripId" uuid NOT NULL,
    "ShipmentId" uuid NOT NULL,
    "DeliverySequence" integer NOT NULL,
    "Status" text,
    "SuggestedAt" timestamp with time zone,
    "RespondedAt" timestamp with time zone,
    "RespondedBy" uuid,
    CONSTRAINT "PK_trip_shipments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_trip_shipments_shipments_ShipmentId" FOREIGN KEY ("ShipmentId") REFERENCES shipments ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_trip_shipments_trips_TripId" FOREIGN KEY ("TripId") REFERENCES trips ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_trip_shipments_ShipmentId" ON trip_shipments ("ShipmentId");

CREATE INDEX "IX_trip_shipments_TripId" ON trip_shipments ("TripId");

CREATE INDEX "IX_trips_DriverId" ON trips ("DriverId");

CREATE INDEX "IX_trips_VehicleId" ON trips ("VehicleId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('202606030001_InitialCreate', '8.0.11');

COMMIT;

