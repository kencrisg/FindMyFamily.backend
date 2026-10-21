namespace FindMyFamily.Core.Entities;

public class Family
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
}
