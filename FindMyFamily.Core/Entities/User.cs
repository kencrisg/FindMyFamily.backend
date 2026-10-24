namespace FindMyFamily.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "admin", "user"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Marca de borrado lógico. Mientras no sea null la cuenta se considera eliminada:
    /// no puede iniciar sesión ni aparece en ninguna consulta. Se conserva la fila para
    /// no perder el histórico de ubicaciones ni romper las referencias de auditoría.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt != null;

    // Navigation properties
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
