using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Data;
using RustDeskApiServer.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var databaseType = builder.Configuration.GetValue<string>("DatabaseType", "SQLITE");
    if (databaseType.ToUpper() == "MYSQL")
    {
        var connectionString = builder.Configuration.GetConnectionString("MySQL");
        if (!string.IsNullOrEmpty(connectionString))
        {
            options.UseMySQL(connectionString);
        }
        else
        {
            // Fallback to SQLite if MySQL connection string is not provided
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                "Data Source=db/db.sqlite3");
        }
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Data Source=db/db.sqlite3");
    }
});

builder.Services.AddIdentity<UserProfile, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Home/Login";
    options.AccessDeniedPath = "/Home/Login";
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        // For development, we'll recreate the database if it doesn't exist
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        
        // Create default admin user if it doesn't exist
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserProfile>>();
        
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new UserProfile
            {
                UserName = "admin",
                Email = "admin@rustdesk.local",
                IsAdmin = true
            };
            var result = await userManager.CreateAsync(adminUser, "admin123");
            if (!result.Succeeded)
            {
                Console.WriteLine($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            else
            {
                Console.WriteLine("Default admin user created: admin/admin123");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error setting up database: {ex.Message}");
    }
}

app.Run();
