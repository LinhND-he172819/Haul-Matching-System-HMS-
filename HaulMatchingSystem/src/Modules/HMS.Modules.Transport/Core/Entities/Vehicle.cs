namespace HMS.Modules.Transport.Core.Entities;

public sealed class Vehicle
{
    public const string AvailableStatus = "Available";
    public const string InMaintenanceStatus = "InMaintenance";
    public const string InactiveStatus = "Inactive";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        AvailableStatus,
        InMaintenanceStatus,
        InactiveStatus
    };

    private Vehicle(
        Guid id,
        string code,
        string licensePlate,
        Guid hubId,
        string vehicleType,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Code = code;
        LicensePlate = licensePlate;
        HubId = hubId;
        VehicleType = vehicleType;
        MaxWeightKg = maxWeightKg;
        MaxVolumeCbm = maxVolumeCbm;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string Code { get; private set; }
    public string LicensePlate { get; private set; }
    public Guid HubId { get; private set; }
    public string VehicleType { get; private set; }
    public decimal MaxWeightKg { get; private set; }
    public decimal MaxVolumeCbm { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Vehicle Create(
        string code,
        string licensePlate,
        Guid hubId,
        string vehicleType,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        string status)
    {
        Validate(code, licensePlate, hubId, vehicleType, maxWeightKg, maxVolumeCbm, status);
        var now = DateTimeOffset.UtcNow;

        return new Vehicle(
            Guid.NewGuid(),
            code.Trim(),
            licensePlate.Trim().ToUpperInvariant(),
            hubId,
            vehicleType.Trim(),
            maxWeightKg,
            maxVolumeCbm,
            NormalizeStatus(status),
            now,
            now);
    }

    public static Vehicle Rehydrate(
        Guid id,
        string code,
        string licensePlate,
        Guid hubId,
        string vehicleType,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Vehicle(
            id,
            code,
            licensePlate,
            hubId,
            vehicleType,
            maxWeightKg,
            maxVolumeCbm,
            status,
            createdAt,
            updatedAt);
    }

    public void Update(
        string code,
        string licensePlate,
        Guid hubId,
        string vehicleType,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        string status)
    {
        Validate(code, licensePlate, hubId, vehicleType, maxWeightKg, maxVolumeCbm, status);

        Code = code.Trim();
        LicensePlate = licensePlate.Trim().ToUpperInvariant();
        HubId = hubId;
        VehicleType = vehicleType.Trim();
        MaxWeightKg = maxWeightKg;
        MaxVolumeCbm = maxVolumeCbm;
        Status = NormalizeStatus(status);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(
        string code,
        string licensePlate,
        Guid hubId,
        string vehicleType,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        string status)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Vehicle code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            throw new ArgumentException("License plate is required.", nameof(licensePlate));
        }

        if (hubId == Guid.Empty)
        {
            throw new ArgumentException("Hub is required.", nameof(hubId));
        }

        if (string.IsNullOrWhiteSpace(vehicleType))
        {
            throw new ArgumentException("Vehicle type is required.", nameof(vehicleType));
        }

        if (maxWeightKg <= 0)
        {
            throw new ArgumentException("Max weight must be greater than 0.", nameof(maxWeightKg));
        }

        if (maxVolumeCbm <= 0)
        {
            throw new ArgumentException("Max volume must be greater than 0.", nameof(maxVolumeCbm));
        }

        if (!AllowedStatuses.Contains(status))
        {
            throw new ArgumentException("Vehicle status is invalid.", nameof(status));
        }
    }

    private static string NormalizeStatus(string status)
    {
        return AllowedStatuses.First(allowed => allowed.Equals(status, StringComparison.OrdinalIgnoreCase));
    }
}
