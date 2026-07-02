public partial class APS
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _nickname;
    private readonly string _bucket;
    private readonly string _alias;

    public APS(string clientId, string clientSecret, string? nickname = null, string? bucket = null)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _nickname = (nickname != null) ? nickname : _clientId;
        _bucket = (bucket != null) ? bucket : _nickname.ToLower() + "-designautomation";
        _alias = "dev";
    }
}