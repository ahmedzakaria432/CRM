using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.CreateUser;
using Crm.Modules.Identity.Application.Commands.DeactivateUser;
using Crm.Modules.Identity.Application.Commands.ReactivateUser;
using Crm.Modules.Identity.Application.Commands.ResetUserPassword;
using Crm.Modules.Identity.Application.Commands.UpdateUserProfile;
using Crm.Modules.Identity.Application.Queries.GetUser;
using Crm.Modules.Identity.Application.Queries.ListUsers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Identity.Presentation.Controllers.Admin;

/// <summary>Basic user administration; every request requires admin.users. Memberships are managed through the
/// Organisation module's Memberships endpoints.</summary>
[Area(CrmAreas.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new ListUsersQuery(search, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetUserQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new CreateUserCommand(request.Email, request.DisplayName, request.PreferredLocale, request.InitialPassword), cancellationToken));

    [HttpPut("{id:guid}/profile")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateProfile(
        Guid id, [FromBody] UpdateUserProfileRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new UpdateUserProfileCommand(id, request.DisplayName, request.PreferredLocale, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Deactivate(
        Guid id, [FromBody] UserRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeactivateUserCommand(id, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Reactivate(
        Guid id, [FromBody] UserRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReactivateUserCommand(id, request.RowVersion), cancellationToken));

    [HttpPut("{id:guid}/password")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ResetUserPasswordCommand(id, request.NewPassword, request.RowVersion), cancellationToken));
}

public sealed record CreateUserRequest(string Email, string DisplayName, string PreferredLocale, string InitialPassword);

/// <summary>RowVersion is base64 over the wire - send back exactly what the GET returned.</summary>
public sealed record UpdateUserProfileRequest(string DisplayName, string PreferredLocale, byte[] RowVersion);

public sealed record UserRowVersionRequest(byte[] RowVersion);

public sealed record ResetPasswordRequest(string NewPassword, byte[] RowVersion);
