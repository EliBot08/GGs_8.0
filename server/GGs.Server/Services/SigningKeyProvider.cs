using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GGs.Server.Services;

public interface ISigningKeyProvider
{
    SigningCredentials GetCurrentSigningCredentials();
    SecurityKey GetCurrentValidationKey();
    IEnumerable<SecurityKey> GetAllValidationKeys();
    string GetCurrentKid();
    string GetIssuer();
    string GetAudience();
    IEnumerable<object> GetJwks();
}

public sealed class SigningKeyProvider : ISigningKeyProvider
{
    private readonly IConfiguration _cfg;
    private readonly List<(string kid, SymmetricSecurityKey key)> _keys = new();
    private readonly string _issuer;
    private readonly string _audience;

    public SigningKeyProvider(IConfiguration cfg)
    {
        _cfg = cfg;
        _issuer = cfg["Auth:Issuer"] ?? "GGs.Server";
        _audience = cfg["Auth:Audience"] ?? "GGs.Clients";

        // Load multiple keys if configured
        var section = cfg.GetSection("Auth:SigningKeys");
        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                var kid = child["kid"];
                var key = child["key"];
                if (!string.IsNullOrWhiteSpace(kid) && !string.IsNullOrWhiteSpace(key))
                {
                    var bytes = Encoding.UTF8.GetBytes(key);
                    var secKey = new SymmetricSecurityKey(bytes) { KeyId = kid };
                    _keys.Add((kid, secKey));
                }
            }
        }

        if (_keys.Count == 0)
        {
            var kid = cfg["Auth:Kid"] ?? "default";
            var key = cfg["Auth:JwtKey"] ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes(key);
            var secKey = new SymmetricSecurityKey(bytes) { KeyId = kid };
            _keys.Add((kid, secKey));
        }
    }

    public SigningCredentials GetCurrentSigningCredentials()
    {
        var current = _keys[0];
        return new SigningCredentials(current.key, SecurityAlgorithms.HmacSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }

    public SecurityKey GetCurrentValidationKey() => _keys[0].key;

    public IEnumerable<SecurityKey> GetAllValidationKeys() => _keys.Select(k => (SecurityKey)k.key);

    public string GetCurrentKid() => _keys[0].kid;

    public string GetIssuer() => _issuer;

    public string GetAudience() => _audience;

    public IEnumerable<object> GetJwks()
    {
        // For HMAC, expose octet JWKs (dev/staging only). In prod, prefer asymmetric.
        return _keys.Select(k => new
        {
            kty = "oct",
            k = Base64UrlEncoder.Encode(((SymmetricSecurityKey)k.key).Key),
            alg = "HS256",
            use = "sig",
            kid = k.kid
        });
    }
}
