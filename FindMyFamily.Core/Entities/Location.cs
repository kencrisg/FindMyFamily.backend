namespace FindMyFamily.Core.Entities;

public class Location
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime CapturedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
