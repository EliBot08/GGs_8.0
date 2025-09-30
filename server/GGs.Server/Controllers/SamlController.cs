using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GGs.Server.Models;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace GGs.Server.Controllers;

[ApiController]
[Route("saml")]
[ApiExplorerSettings(GroupName = "saml")]
public sealed class SamlController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SamlController> _logger;

    public SamlController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        ILogger<SamlController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("metadata")]
    [AllowAnonymous]
    public IActionResult GetMetadata()
    {
        try
        {
            var entityId = _configuration["Saml:EntityId"] ?? $"{Request.Scheme}://{Request.Host}";
            var acsUrl = $"{Request.Scheme}://{Request.Host}/saml/acs";
            var sloUrl = $"{Request.Scheme}://{Request.Host}/saml/slo";
            
            var metadata = GenerateMetadata(entityId, acsUrl, sloUrl);
            
            return Content(metadata, "application/samlmetadata+xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAML metadata");
            return StatusCode(500, "Error generating SAML metadata");
        }
    }

    [HttpPost("acs")]
    [AllowAnonymous]
    public async Task<IActionResult> AssertionConsumerService()
    {
        try
        {
            if (!Request.HasFormContentType)
            {
                return BadRequest("Invalid content type");
            }

            var samlResponse = Request.Form["SAMLResponse"].ToString();
            if (string.IsNullOrEmpty(samlResponse))
            {
                return BadRequest("Missing SAMLResponse");
            }

            var decodedResponse = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
            _logger.LogDebug("Received SAML Response: {Response}", decodedResponse);

            var samlDoc = XDocument.Parse(decodedResponse);
            var ns = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

            // Extract user information from SAML assertion
            var assertion = samlDoc.Descendants(ns + "Assertion").FirstOrDefault();
            if (assertion == null)
            {
                return BadRequest("Invalid SAML assertion");
            }

            var subject = assertion.Descendants(ns + "Subject").FirstOrDefault();
            var nameId = subject?.Descendants(ns + "NameID").FirstOrDefault()?.Value;

            if (string.IsNullOrEmpty(nameId))
            {
                return BadRequest("Missing NameID in SAML assertion");
            }

            // Extract attributes
            var attributes = new Dictionary<string, string>();
            var attributeStatements = assertion.Descendants(ns + "AttributeStatement");
            foreach (var attributeStatement in attributeStatements)
            {
                foreach (var attribute in attributeStatement.Descendants(ns + "Attribute"))
                {
                    var name = attribute.Attribute("Name")?.Value;
                    var value = attribute.Descendants(ns + "AttributeValue").FirstOrDefault()?.Value;
                    
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                    {
                        attributes[name] = value;
                    }
                }
            }

            // Map common SAML attributes
            var email = attributes.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") 
                       ?? attributes.GetValueOrDefault("email") 
                       ?? nameId;

            var groups = attributes.GetValueOrDefault("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups")
                        ?? attributes.GetValueOrDefault("groups")
                        ?? "";

            // Find or create user
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SamlNameId = nameId,
                        Groups = groups.Split(',', StringSplitOptions.RemoveEmptyEntries),
                        CreatedViaSaml = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    })
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to create user via SAML: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    return StatusCode(500, "Failed to create user");
                }

                // Assign role based on SAML groups or default to BasicUser
                var roleToAssign = MapSamlGroupToRole(groups) ?? "BasicUser";
                await _userManager.AddToRoleAsync(user, roleToAssign);

                _logger.LogInformation("Created new user via SAML: {Email} with role {Role}", email, roleToAssign);
            }

            // Sign in the user
            await _signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "SAML");

            var tenantId = HttpContext.Items["TenantId"]?.ToString() ?? "default";
            _logger.LogInformation("SAML SSO successful for user {Email} in tenant {TenantId}", email, tenantId);

            // Redirect to application
            var relayState = Request.Form["RelayState"].ToString();
            var redirectUrl = !string.IsNullOrEmpty(relayState) ? relayState : "/";
            
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAML assertion");
            return StatusCode(500, "Error processing SAML assertion");
        }
    }

    private string GenerateMetadata(string entityId, string acsUrl, string sloUrl)
    {
        var cert = GetSamlCertificate();
        var certData = cert != null ? Convert.ToBase64String(cert.RawData) : "";

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<md:EntityDescriptor 
    xmlns:md=""urn:oasis:names:tc:SAML:2.0:metadata""
    xmlns:ds=""http://www.w3.org/2000/09/xmldsig#""
    entityID=""{entityId}"">
    
    <md:SPSSODescriptor 
        protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol""
        AuthnRequestsSigned=""false""
        WantAssertionsSigned=""true"">
        
        <md:KeyDescriptor use=""signing"">
            <ds:KeyInfo>
                <ds:X509Data>
                    <ds:X509Certificate>{certData}</ds:X509Certificate>
                </ds:X509Data>
            </ds:KeyInfo>
        </md:KeyDescriptor>
        
        <md:AssertionConsumerService 
            Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST""
            Location=""{acsUrl}""
            index=""0""
            isDefault=""true""/>
            
    </md:SPSSODescriptor>
    
</md:EntityDescriptor>";
    }

    private X509Certificate2? GetSamlCertificate()
    {
        try
        {
            var certPath = _configuration["Saml:CertificatePath"];
            if (!string.IsNullOrEmpty(certPath) && System.IO.File.Exists(certPath))
            {
                return X509CertificateLoader.LoadCertificateFromFile(certPath);
            }

            // Fallback: generate self-signed certificate
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=GGs SAML", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading SAML certificate");
            return null;
        }
    }

    private string? MapSamlGroupToRole(string groups)
    {
        if (string.IsNullOrEmpty(groups)) return null;

        var groupList = groups.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(g => g.Trim().ToLowerInvariant())
                             .ToList();

        if (groupList.Any(g => g.Contains("admin"))) return "Admin";
        if (groupList.Any(g => g.Contains("moderator"))) return "Moderator";
        if (groupList.Any(g => g.Contains("enterprise"))) return "EnterpriseUser";
        if (groupList.Any(g => g.Contains("pro"))) return "ProUser";

        return "BasicUser";
    }
}