using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.RateLimiting;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.RefreshSession;
using Crm.Modules.Identity.Application.Commands.SignIn;
using Crm.Modules.Identity.Application.Commands.SignOut;
using Crm.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Modules.Identity.Presentation.Controllers.Agent;

/// <summary>Staff sign-in, refresh, and sign-out. The only anonymous actions in the Agent Area. The refresh token
/// travels in the JSON body and in the HttpOnly crm_refresh cookie; refresh and logout accept either.</summary>
[Area(CrmAreas.Agent)]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(CrmRateLimitPolicies.SignIn)]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SignInResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        Result<SignInOutcome> result = await Sender.Send(new SignInCommand(request.Email, request.Password), cancellationToken);

        return ToSessionResponse(result, "Invalid email or password.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(CrmRateLimitPolicies.SignIn)]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SignInResponse>> Refresh([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        string? token = request?.RefreshToken ?? Request.Cookies[RefreshCookie.Name];

        if (string.IsNullOrWhiteSpace(token))
        {
            DeleteCookie();
            return Problem(Error.Unauthorized(IdentityCodes.InvalidRefreshToken, SessionEnded));
        }

        Result<SignInOutcome> result = await Sender.Send(new RefreshSessionCommand(token), cancellationToken);

        return ToSessionResponse(result, SessionEnded);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        string? token = request?.RefreshToken ?? Request.Cookies[RefreshCookie.Name];

        Result result = await Sender.Send(new SignOutCommand(token), cancellationToken);
        DeleteCookie();

        return ToActionResult(result);
    }

    private const string SessionEnded = "Your session has ended. Sign in again.";

    private ActionResult<SignInResponse> ToSessionResponse(Result<SignInOutcome> result, string failureDescription)
    {
        if (result.IsFailure)
        {
            return Problem(result.Error);
        }

        SignInOutcome outcome = result.Value;

        if (outcome.Tokens is not { } tokens)
        {
            DeleteCookie();
            return Problem(Error.Unauthorized(outcome.FailureCode ?? IdentityCodes.InvalidCredentials, failureDescription));
        }

        Response.Cookies.Append(
            RefreshCookie.Name, tokens.RefreshToken, RefreshCookie.Options(tokens.RefreshTokenExpiresAtUtc, Secure()));

        return Ok(new SignInResponse(
            tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc));
    }

    // Secure always (Story 07, R6): browsers accept a Secure cookie on http://localhost, and it never travels in the clear elsewhere.
    private static bool Secure() => true;

    private void DeleteCookie() => Response.Cookies.Delete(RefreshCookie.Name, RefreshCookie.DeleteOptions(Secure()));
}

public sealed record LoginRequest(string Email, string Password);

/// <summary>Both fields optional: the browser sends an empty body and the cookie carries the token.</summary>
public sealed record RefreshRequest(string? RefreshToken);

public sealed record SignInResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    string TokenType = "Bearer");

internal static class IdentityCodes
{
    public const string InvalidCredentials = "Identity.InvalidCredentials";
    public const string InvalidRefreshToken = "Identity.InvalidRefreshToken";
}
