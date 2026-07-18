using System.Globalization;

namespace AxisSphere51.Server;

/// <summary>Options bound from the "Accounts" configuration section.</summary>
public class AccountOptions
{
    public string BaseDirectory { get; set; } = "";
    /// <summary>Sphere account files to read (relative to BaseDirectory or absolute).</summary>
    public List<string> Files { get; set; } = new() { "accounts/sphereaccu.scp" };
    /// <summary>Minimum PLEVEL allowed to receive data. 2 = above Player (Counselor+).</summary>
    public int MinPlevel { get; set; } = 2;
}

public record Account(string Name, string Password, int Plevel);

/// <summary>
/// Reads Sphere account scripts ([login] blocks with PASSWORD= and optional PLEVEL=)
/// and authenticates GM logins. Accounts without a PLEVEL line are treated as Player (1).
/// Passwords never leave the server — they are only used to verify a login.
/// </summary>
public class AccountService
{
    private readonly AccountOptions _options;
    private readonly ILogger<AccountService> _logger;
    private readonly object _gate = new();
    private Dictionary<string, Account> _byName = new(StringComparer.OrdinalIgnoreCase);

    public AccountService(Microsoft.Extensions.Options.IOptions<AccountOptions> options, ILogger<AccountService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public int MinPlevel => _options.MinPlevel;
    public int Count { get { lock (_gate) return _byName.Count; } }

    public void Reload()
    {
        var map = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _options.Files)
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(_options.BaseDirectory, name);
            if (!File.Exists(path))
            {
                _logger.LogWarning("Account file not found: {Path}", path);
                continue;
            }
            foreach (var acc in ParseFile(path))
                map[acc.Name] = acc; // later files/blocks win
        }
        lock (_gate) _byName = map;
        _logger.LogInformation("Loaded {Count} accounts (min plevel {Min}).", map.Count, _options.MinPlevel);
    }

    private static IEnumerable<Account> ParseFile(string path)
    {
        string? current = null;
        string? password = null;
        int plevel = 1;

        Account? Flush()
        {
            if (current != null && password != null)
                return new Account(current, password, plevel);
            return null;
        }

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var done = Flush();
                if (done != null) yield return done;
                current = line[1..^1].Trim();
                password = null;
                plevel = 1;
                continue;
            }
            if (current == null) continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim().ToUpperInvariant();
            var value = line[(eq + 1)..].Trim();
            if (key == "PASSWORD") password = value;
            else if (key == "PLEVEL" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                plevel = lvl;
        }
        var last = Flush();
        if (last != null) yield return last;
    }

    /// <summary>Verifies login + password. Returns the account (with its plevel) or null.</summary>
    public Account? Validate(string login, string password)
    {
        lock (_gate)
        {
            if (_byName.TryGetValue(login, out var acc) && acc.Password == password)
                return acc;
        }
        return null;
    }

    public bool IsAuthorized(int plevel) => plevel >= _options.MinPlevel;
}
