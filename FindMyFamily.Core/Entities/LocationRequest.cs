namespace FindMyFamily.Core.Entities;

/// <summary>
/// Registro de auditoría de una petición de ubicación On-Demand.
/// Existe para dos cosas: dejar rastro de quién pidió la ubicación de quién
/// (responsabilidad) y acotar a qué familia se difunde la coordenada reportada.
/// </summary>
public class LocationRequest
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public Guid? LocationId { get; set; }

    // Navigation properties
    public User Requester { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
    public Family Family { get; set; } = null!;
}
