using AxisSphere51.Server;

// Anchor the content root to the executable's folder so appsettings.json is always
// found, no matter what the current working directory is (e.g. double-clicked exe).
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Human-readable axisserver.ini overrides any defaults.
builder.Configuration.AddAxisIni(AppContext.BaseDirectory);

// Quiet, production-looking console: drop the framework's noisy per-message logging; we print a
// clean status banner ourselves and keep the full detail in the log file.
builder.Logging.ClearProviders();

builder.Services.Configure<ScriptOptions>(builder.Configuration.GetSection("Scripts"));
builder.Services.Configure<AccountOptions>(builder.Configuration.GetSection("Accounts"));
builder.Services.AddSingleton<ScriptRepository>();
builder.Services.AddSingleton<AccountService>();

// Login/password auth against Sphere accounts; only PLEVEL above Player may receive data.
int minPlevel = builder.Configuration.GetValue("Accounts:MinPlevel", 2);
builder.Services.AddAuthentication(BasicAuthHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BasicAuthHandler>(
        BasicAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Staff", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
    {
        var c = ctx.User.FindFirst(BasicAuthHandler.PlevelClaim)?.Value;
        return int.TryParse(c, out var lvl) && lvl >= minPlevel;
    }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new()
    {
        Title = "Axis Sphere51 Data Server",
        Version = "v1",
        Description = "Serves parsed Sphere 0.51a script data (items, NPCs, areas) to the Axis Sphere51 tool."
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Load scripts + accounts once at startup (cache-and-serve).
app.Services.GetRequiredService<ScriptRepository>().Reload();
app.Services.GetRequiredService<AccountService>().Reload();

// File logging (logs/server-YYYY-MM-DD.log next to the exe): startup, connections, requests, auth.
FileLog.Init(AppContext.BaseDirectory);
{
    var repo = app.Services.GetRequiredService<ScriptRepository>();
    var stats = repo.Stats();
    var regions = repo.Regions().Count;
    var accounts = app.Services.GetRequiredService<AccountService>();
    var url = builder.Configuration["Urls"] ?? "http://0.0.0.0:5099";
    var localUrl = url.Replace("0.0.0.0", "localhost").Split(';')[0];
    var logName = $"logs/server-{DateTime.Now:yyyy-MM-dd}.log";

    FileLog.Write("START", $"Server started on {url} — {stats.Items} items, {stats.Npcs} npcs, " +
        $"{regions} regions; {accounts.Count} accounts (min plevel {accounts.MinPlevel}); files: {string.Join(", ", stats.Files)}");

    ConsoleUi.Banner(url, localUrl, stats.Items, stats.Npcs, regions, accounts.Count, accounts.MinPlevel, logName);
}
app.Lifetime.ApplicationStopping.Register(() =>
{
    FileLog.Write("STOP", "Server stopping.");
    ConsoleUi.Stopped();
});

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Axis Sphere51 Data Server v1");
    o.RoutePrefix = "swagger";
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Log every request: client IP, authenticated user, method + route, and resulting status.
app.Use(async (ctx, next) =>
{
    await next();
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
    var user = ctx.User?.Identity?.Name;
    user = string.IsNullOrEmpty(user) ? "-" : user;
    var status = ctx.Response.StatusCode;
    var verdict = status == 401 ? " (unauthorized)" : status == 403 ? " (forbidden: plevel too low)" : "";
    FileLog.Write("REQ", $"{ip} user={user} {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString} -> {status}{verdict}");
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// All data endpoints require a Sphere login with PLEVEL above Player.
var api = app.MapGroup("/api").RequireAuthorization("Staff");

// Credential check / whoami — succeeds only for an authorised GM login.
api.MapGet("/me", (System.Security.Claims.ClaimsPrincipal user) => Results.Ok(new
{
    name = user.Identity?.Name,
    plevel = int.TryParse(user.FindFirst(BasicAuthHandler.PlevelClaim)?.Value, out var p) ? p : 0
}));

api.MapGet("/stats", (ScriptRepository repo) => Results.Ok(repo.Stats()));

api.MapPost("/reload", (ScriptRepository repo) =>
{
    repo.Reload();
    return Results.Ok(repo.Stats());
});

api.MapGet("/items", (ScriptRepository repo, string? search, string? category,
        string? subsection, int offset = 0, int limit = 200) =>
    Results.Ok(repo.Query("item", search, category, subsection, offset, limit <= 0 ? 200 : limit)));

api.MapGet("/items/{id}", (ScriptRepository repo, string id) =>
    repo.ById("item", id) is { } o ? Results.Ok(o) : Results.NotFound());

api.MapGet("/npcs", (ScriptRepository repo, string? search, string? category,
        string? subsection, int offset = 0, int limit = 200) =>
    Results.Ok(repo.Query("npc", search, category, subsection, offset, limit <= 0 ? 200 : limit)));

api.MapGet("/npcs/{id}", (ScriptRepository repo, string id) =>
    repo.ById("npc", id) is { } o ? Results.Ok(o) : Results.NotFound());

api.MapGet("/areas", (ScriptRepository repo, string? search, int offset = 0, int limit = 200) =>
    Results.Ok(repo.Query("area", search, null, null, offset, limit <= 0 ? 200 : limit)));

api.MapGet("/categories", (ScriptRepository repo, string? kind) =>
    Results.Ok(repo.Categories(kind == "npc" ? "npc" : "item")));

// Travel regions (areas with nested rooms + standalone rooms), optionally filtered by
// free-text search (name/group) and/or map number.
api.MapGet("/regions", (ScriptRepository repo, string? search, int? map) =>
{
    IEnumerable<RegionDto> q = repo.Regions();
    if (map is int m)
        q = q.Where(r => r.Map == m);
    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        q = q.Where(r =>
            r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
            r.Group.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
    return Results.Ok(q.ToList());
});

api.MapGet("/spells", (ScriptRepository repo, string? search) =>
{
    IEnumerable<SpellDto> q = repo.Spells();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        q = q.Where(sp =>
            sp.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
            sp.DefName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
    return Results.Ok(q.ToList());
});

app.Run();

// Exposed so the integration-test WebApplicationFactory can boot the same host.
public partial class Program { }
