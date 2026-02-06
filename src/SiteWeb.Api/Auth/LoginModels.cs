namespace SiteWeb.Api.Auth;

public record LoginRequest(string Username, string Password, string? MfaCode);
public record TokenResponse(string AccessToken, string RefreshToken);
public record RefreshRequest(string RefreshToken);
