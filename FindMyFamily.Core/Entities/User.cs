namespace FindMyFamily.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
