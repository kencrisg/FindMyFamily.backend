using System.Security.Claims;
using FindMyFamily.Core.DTOs.Families;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FamiliesController : ControllerBase
{
    private readonly IFamilyService _familyService;

    public FamiliesController(IFamilyService familyService)
    {
        _familyService = familyService;
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }
            return userId;
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.CreateFamilyAsync(CurrentUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFamilies(CancellationToken cancellationToken)
    {
        var response = await _familyService.GetUserFamiliesAsync(CurrentUserId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FamilyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFamilyById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _familyService.GetFamilyByIdAsync(CurrentUserId, id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(FamilyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddFamilyMemberRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.AddMemberByPhoneAsync(CurrentUserId, id, request, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await _familyService.RemoveMemberAsync(CurrentUserId, id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleRequestDto request, CancellationToken cancellationToken)
    {
        await _familyService.UpdateMemberRoleAsync(CurrentUserId, id, userId, request, cancellationToken);
        return NoContent();
    }
}
