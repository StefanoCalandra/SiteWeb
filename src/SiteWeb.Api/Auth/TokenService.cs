using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SiteWeb.Api.Auth;

public class TokenService
{
    public const string Issuer = "SiteWeb";
    public const string Audience = "SiteWebClients";
    public const string SecretKey = "super-secret-key-please-change";

    public TokenResponse CreateTokens(string username, string role, RefreshTokenStore store)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Guid.NewGuid().ToString("N");
        store.Store(refreshToken, username);

        return new TokenResponse(accessToken, refreshToken);
    }
}
