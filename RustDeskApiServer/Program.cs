using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using RustDeskApiServer.Components;
using RustDeskApiServer.Data;
using RustDeskApiServer.Endpoints;
using RustDeskApiServer.Models;
using RustDeskApiServer.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
var dbType   = builder.Configuration.GetValue("DatabaseType", "SQLITE");
var mysqlDb  = builder.Configuration.GetValue<string>("MySql:Database");
var mysqlUsr = builder.Configuration.GetValue<string>("MySql:User");
var mysqlPwd = builder.Configuration.GetValue<string>("MySql:Password");
var mysqlHost = builder.Configuration.GetValue("MySql:Host", "127.0.0.1");
var mysqlPort = builder.Configuration.GetValue("MySql:Port", "3306");

if (dbType == "MYSQL"
    && !string.IsNullOrEmpty(mysqlDb)
    && !string.IsNullOrEmpty(mysqlUsr)
    && !string.IsNullOrEmpty(mysqlPwd))
{
    var conn = $"Server={mysqlHost};Port={mysqlPort};Database={mysqlDb};User={mysqlUsr};Password={mysqlPwd};CharSet=utf8;";
    builder.Services.AddDbContextFactory<AppDbContext>(o =>
        o.UseMySql(conn, ServerVersion.AutoDetect(conn)));
}
else
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "..", "db", "db.sqlite3");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    builder.Services.AddDbContextFactory<AppDbContext>(o =>
        o.UseSqlite($"Data Source={dbPath}"));
}

// ── Identity ─────────────────────────────────────────────────────────────────
builder.Services
    .AddIdentity<UserProfile, IdentityRole<int>>(o =>
    {
        o.Password.RequireDigit           = false;
        o.Password.RequireLowercase       = false;
        o.Password.RequireUppercase       = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredLength         = 8;
        o.SignIn.RequireConfirmedAccount  = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath       = "/login";
    o.LogoutPath      = "/account/logout";
    o.AccessDeniedPath = "/login";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan  = TimeSpan.FromHours(8);
});

// ── Blazor + MudBlazor ───────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddCascadingAuthenticationState();

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService,    TokenService>();
builder.Services.AddScoped<IFileSizeService, FileSizeService>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Migrate/create database on startup
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Backward-compat redirects ─────────────────────────────────────────────────
app.MapGet("/", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/work")
        : Results.Redirect("/login"));

app.MapGet("/api/user_action", (string? action) => action switch
{
    "login"    => Results.Redirect("/login"),
    "register" => Results.Redirect("/register"),
    "logout"   => Results.Redirect("/account/logout"),
    _          => Results.Redirect("/work")
});

app.MapGet("/api/work",     () => Results.Redirect("/work"));
app.MapGet("/api/share",    () => Results.Redirect("/share"));
app.MapGet("/api/conn_log", () => Results.Redirect("/conn-log"));
app.MapGet("/api/file_log", () => Results.Redirect("/file-log"));

// ── Logout endpoint ───────────────────────────────────────────────────────────
app.MapGet("/account/logout", async (SignInManager<UserProfile> sm) =>
{
    await sm.SignOutAsync();
    return Results.Redirect("/login");
});

// ── RustDesk Minimal API endpoints ───────────────────────────────────────────
app.MapRustDeskApi();

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

