public partial class APS
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _nickname;
    private readonly string _bucket;
    private readonly string _alias;

    public APS(string clientId, string clientSecret, string bucket = null)
    {
        _clientId = _nickname = clientId;
        _clientSecret = clientSecret;
        _bucket = _nickname.ToLower() + "-designautomation";
        _alias = "dev";
    }
}