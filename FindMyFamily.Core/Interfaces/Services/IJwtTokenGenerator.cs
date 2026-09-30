using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, Device device);
    string GenerateRefreshToken();
}
