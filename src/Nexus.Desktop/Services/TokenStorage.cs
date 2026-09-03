namespace Nexus.Desktop.Services;

public interface ITokenStorage
{
    string? GetToken();
    void SaveToken(string token);
    void ClearToken();
}

public class TokenStorage : ITokenStorage
{
    private string? _cachedToken;

    public string? GetToken() => _cachedToken;

    public void SaveToken(string token)
    {
        _cachedToken = token;
    }

    public void ClearToken()
    {
        _cachedToken = null;
    }
}
