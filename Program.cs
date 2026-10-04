using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FypMatchTracker.Data;
using FypMatchTracker.Services;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Bind to cloud provider PORT if specified (e.g. Render, Railway, Azure)
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(cloudPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudPort}");
}

// 1. Add MVC Controllers and Razor Views
builder.Services.AddControllersWithViews();

// 2. Configure Entity Framework Core with SQLite Relational Database (Code-First)
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                          ?? "Data Source=fyp_match_tracker.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 3. Configure Lightweight Custom Cookie Authentication with Role Claims
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "FypMatchTracker.Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

// 4. Register Core Services (As defined in Class Diagram)
builder.Services.AddHttpClient<CrossrefService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<KeywordService>();
builder.Services.AddSingleton<DocumentExtractionService>();
builder.Services.AddScoped<SimilarityService>();

var app = builder.Build();

// 5. Initialize & Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database initialization.");
    }
}

// 6. Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map REST API Controllers
app.MapControllers();

// Map Default Route: First visits go to Splash Screen, which transitions to Home
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Splash}/{id?}");

app.Run();
