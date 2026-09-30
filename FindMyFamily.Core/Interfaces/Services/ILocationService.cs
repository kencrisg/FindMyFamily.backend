using FindMyFamily.Core.DTOs.Locations;

namespace FindMyFamily.Core.Interfaces.Services;

public interface ILocationService
{
    Task<LocationRequestResponseDto> RequestLocationAsync(Guid requesterId, RequestLocationDto request, CancellationToken cancellationToken = default);
    Task<LocationDto> ReportLocationAsync(Guid reportingUserId, ReportLocationDto request, CancellationToken cancellationToken = default);
    Task<LocationDto?> GetLatestLocationAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationDto>> GetLocationHistoryAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, int limit = 50, CancellationToken cancellationToken = default);
}
