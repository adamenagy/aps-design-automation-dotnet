using Autodesk.Authentication;
using Autodesk.Authentication.Model;

public record Token(string AccessToken, DateTime ExpiresAt);

public partial class APS
{
    private Token _internalTokenCache;

    private async Task<Token> GetToken(List<Scopes> scopes)
    {
        var authenticationClient = new AuthenticationClient();
        var auth = await authenticationClient.GetTwoLeggedTokenAsync(_clientId, _clientSecret, scopes);
        return new Token(auth.AccessToken, DateTime.UtcNow.AddSeconds((double)auth.ExpiresIn));
    }

    private async Task<Token> GetInternalToken()
    {
        if (_internalTokenCache == null || _internalTokenCache.ExpiresAt < DateTime.UtcNow)
            _internalTokenCache = await GetToken([Scopes.CodeAll, Scopes.BucketCreate, Scopes.BucketRead, Scopes.DataRead, Scopes.DataWrite, Scopes.DataCreate]);
        return _internalTokenCache;
    }
}