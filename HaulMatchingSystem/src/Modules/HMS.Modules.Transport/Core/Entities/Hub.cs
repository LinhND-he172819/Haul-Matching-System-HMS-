namespace HMS.Modules.Transport.Core.Entities;

public sealed class Hub
{
    private Hub(
        Guid id,
        string name,
        string address,
        double latitude,
        double longitude,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Hub Create(string name, string address, double latitude, double longitude)
    {
        Validate(name, address, latitude, longitude);
        var now = DateTimeOffset.UtcNow;

        return new Hub(
            Guid.NewGuid(),
            name.Trim(),
            address.Trim(),
            latitude,
            longitude,
            now,
            now);
    }

    public static Hub Rehydrate(
        Guid id,
        string name,
        string address,
        double latitude,
        double longitude,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Hub(id, name, address, latitude, longitude, createdAt, updatedAt);
    }

    public void Update(string name, string address, double latitude, double longitude)
    {
        Validate(name, address, latitude, longitude);

        Name = name.Trim();
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(string name, string address, double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Hub name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Hub address is required.", nameof(address));
        }

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentException("Latitude must be between -90 and 90.", nameof(latitude));
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentException("Longitude must be between -180 and 180.", nameof(longitude));
        }
    }
}
