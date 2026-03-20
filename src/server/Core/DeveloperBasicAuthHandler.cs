using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NMAC.Core;

public class DeveloperBasicAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<DeveloperBasicAuthOptions> basicOptions
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authHeader = authHeaderValues.ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var encodedCredentials = authHeader["Basic ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(encodedCredentials))
            return Task.FromResult(AuthenticateResult.Fail("Missing Basic credentials."));

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic credentials format."));
        }

        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex <= 0)
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic credentials payload."));

        var username = decoded[..separatorIndex];
        var password = decoded[(separatorIndex + 1)..];

        var configured = basicOptions.Value;
        var usernameMatches = string.Equals(username, configured.Username, StringComparison.Ordinal);
        var passwordMatches = string.Equals(password, configured.Password, StringComparison.Ordinal);

        if (!usernameMatches || !passwordMatches)
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.AuthenticationMethod, "Basic")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Developer\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }
}