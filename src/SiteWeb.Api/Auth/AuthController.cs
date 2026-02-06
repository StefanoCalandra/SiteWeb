using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SiteWeb.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly Dictionary<string, (string Password, string Role, string MfaCode)> Users = new()
    {
        ["admin"] = ("password", "Admin", "123456"),
        ["user"] = ("password", "User", "123456")
    };

    private readonly TokenService _tokenService;
    private readonly RefreshTokenStore _refreshTokenStore;

    public AuthController(TokenService tokenService, RefreshTokenStore refreshTokenStore)
    {
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    [HttpPost("login")]
    public ActionResult<TokenResponse> Login(LoginRequest request)
    {
        // Verifica credenziali e MFA.
        if (!Users.TryGetValue(request.Username, out var data) || data.Password != request.Password)
        {
            return Unauthorized();
        }

        if (request.MfaCode != data.MfaCode)
        {
            return Unauthorized("MFA code required");
        }

        return Ok(_tokenService.CreateTokens(request.Username, data.Role, _refreshTokenStore));
    }

    [HttpPost("refresh")]
    public ActionResult<TokenResponse> Refresh(RefreshRequest request)
    {
        // Refresh token monouso: se valido, emette nuovi token.
        if (!_refreshTokenStore.TryConsume(request.RefreshToken, out var username))
        {
            return Unauthorized();
        }

        var role = Users.TryGetValue(username, out var data) ? data.Role : "User";
        return Ok(_tokenService.CreateTokens(username, role, _refreshTokenStore));
    }

    [Authorize]
    [HttpPost("revoke")]
    public IActionResult Revoke()
    {
        // Revoca tutti i refresh token per l'utente.
        var username = User.Identity?.Name;
        if (username is null)
        {
            return Unauthorized();
        }

        _refreshTokenStore.RevokeAll(username);
        return NoContent();
    }
}
