using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.RateLimiting;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.PortalSignIn;
using Crm.Modules.Identity.Application.Commands.PortalSignOut;
using Crm.Modules.Identity.Application.Commands.RefreshPortalSession;
using Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;
using Crm.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IdentityCodes = Crm.Modules.Identity.Presentation.Controllers.Agent.IdentityCodes;

namespace Crm.Modules.Identity.Presentation.Controllers.Portal;

/// <summary>Customer portal registration, sign-in, refresh and sign-out (Story 47). The anonymous actions of the Portal
/// Area. The refresh token travels in the JSON body and in the HttpOnly crm_portal_refresh cookie.</summary>
[Area(CrmAreas.Portal)]
public sealed class AuthController : ApiControllerBase
{
    private const string SessionEnded = "Your session has ended. Sign in again.";

    /// <summary>Always 204 (unless 400/429): the response never says whether the email belongs to a customer (D5).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(CrmRateLimitPolicies.PortalSignIn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Register([FromBody] PortalRegisterRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new RegisterPortalAccountCommand(request.Email, request.Password, request.PreferredLocale), cancellationToken));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(CrmRateLimitPolicies.PortalSignIn)]
    [ProducesResponseType<PortalSignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PortalSignInResponse>> Login([FromBody] PortalLoginRequest request, CancellationToken cancellationToken)
    {
        Result<SignInOutcome> result = await Sender.Send(new PortalSignInCommand(request.Email, request.Password), cancellationToken);

        return ToSessionResponse(result, "Invalid email or password.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(CrmRateLimitPolicies.PortalSignIn)]
    [ProducesResponseType<PortalSignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PortalSignInResponse>> Refresh([FromBody] PortalRefreshRequest? request, CancellationToken cancellationToken)
    {
        string? token = request?.RefreshToken ?? Request.Cookies[PortalRefreshCookie.Name];

        if (string.IsNullOrWhiteSpace(token))
        {
            DeleteCookie();
            return Problem(Error.Unauthorized(IdentityCodes.InvalidRefreshToken, SessionEnded));
        }

        Result<SignInOutcome> result = await Sender.Send(new RefreshPortalSessionCommand(token), cancellationToken);

        return ToSessionResponse(result, SessionEnded);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout([FromBody] PortalRefreshRequest? request, CancellationToken cancellationToken)
    {
        string? token = request?.RefreshToken ?? Request.Cookies[PortalRefreshCookie.Name];

        Result result = await Sender.Send(new PortalSignOutCommand(token), cancellationToken);
        DeleteCookie();

        return ToActionResult(result);
    }

    private ActionResult<PortalSignInResponse> ToSessionResponse(Result<SignInOutcome> result, string failureDescription)
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
            PortalRefreshCookie.Name, tokens.RefreshToken, PortalRefreshCookie.Options(tokens.RefreshTokenExpiresAtUtc, Secure()));

        return Ok(new PortalSignInResponse(
            tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc));
    }

    // Secure always (Story 07, R6): browsers accept a Secure cookie on http://localhost, and it never travels in the clear elsewhere.
    private static bool Secure() => true;

    private void DeleteCookie() => Response.Cookies.Delete(PortalRefreshCookie.Name, PortalRefreshCookie.DeleteOptions(Secure()));
}

public sealed record PortalRegisterRequest(string Email, string Password, string? PreferredLocale = null);

public sealed record PortalLoginRequest(string Email, string Password);

/// <summary>Optional: the browser sends an empty body and the cookie carries the token.</summary>
public sealed record PortalRefreshRequest(string? RefreshToken);

public sealed record PortalSignInResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    string TokenType = "Bearer");
