using System.Collections.Concurrent;

namespace SiteWeb.Api.Auth;

public class RefreshTokenStore
{
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    public void Store(string refreshToken, string username)
    {
        _tokens[refreshToken] = username;
    }

    public bool TryConsume(string refreshToken, out string username)
    {
        if (_tokens.TryRemove(refreshToken, out var value))
        {
            username = value;
            return true;
        }

        username = string.Empty;
        return false;
    }

    public void RevokeAll(string username)
    {
        foreach (var token in _tokens.Where(kvp => kvp.Value == username).ToList())
        {
            _tokens.TryRemove(token.Key, out _);
        }
    }
}
