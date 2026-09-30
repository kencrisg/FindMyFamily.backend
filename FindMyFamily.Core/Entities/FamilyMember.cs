namespace FindMyFamily.Core.Entities;

public class FamilyMember
{
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public Family Family { get; set; } = null!;
    public User User { get; set; } = null!;
}
