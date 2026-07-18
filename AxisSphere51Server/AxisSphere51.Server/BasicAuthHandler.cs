using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AxisSphere51.Server;

/// <summary>
/// HTTP Basic authentication against Sphere accounts. A valid login+password yields an
/// identity carrying the account's PLEVEL as a claim; the "Staff" authorization policy
/// then checks that PLEVEL is above Player. Bad credentials → 401; valid-but-too-low → 403.
/// </summary>
public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Basic";
    public const string PlevelClaim = "plevel";

    private readonly AccountService _accounts;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AccountService accounts)
        : base(options, logger, encoder)
    {
        _accounts = accounts;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var value = header.ToString();
        if (!value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        string login, password;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value["Basic ".Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials"));
            login = decoded[..sep];
            password = decoded[(sep + 1)..];
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials"));
        }

        var ip = Context.Connection.RemoteIpAddress?.ToString() ?? "-";
        var account = _accounts.Validate(login, password);
        if (account is null)
        {
            FileLog.Write("AUTH", $"{ip} login FAILED for '{login}' (bad login or password)");
            return Task.FromResult(AuthenticateResult.Fail("Invalid login or password"));
        }

        FileLog.Write("AUTH", $"{ip} login OK: {account.Name} (plevel {account.Plevel})");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, account.Name),
            new Claim(PlevelClaim, account.Plevel.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Basic realm=\"Axis Sphere51\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }
}
