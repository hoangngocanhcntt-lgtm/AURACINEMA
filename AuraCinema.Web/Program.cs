using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Infrastructure.Seed;
using AuraCinema.Services.Auth;
using AuraCinema.Services.Booking;
using AuraCinema.Services.Email;
using AuraCinema.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ===== Serilog =====
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/auracinema-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ===== DbContext =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("AuraCinema.Infrastructure")));

// ===== Cookie Authentication =====
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ===== DI — Services =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.IChatTool, AuraCinema.Services.Chat.Tools.MovieSearchTool>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.IChatTool, AuraCinema.Services.Chat.Tools.ShowtimeQueryTool>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.IChatTool, AuraCinema.Services.Chat.Tools.PromotionListTool>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.IChatTool, AuraCinema.Services.Chat.Tools.PriceInfoTool>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.IChatTool, AuraCinema.Services.Chat.Tools.FaqTool>();
builder.Services.AddHostedService<BookingCleanupService>();

builder.Services.AddHttpClient();
builder.Services.Configure<AuraCinema.Domain.Models.Chat.LlmOptions>(builder.Configuration.GetSection("Llm"));

// ===== Chat anti-overload =====
builder.Services.AddSingleton<AuraCinema.Services.Chat.ApiKeyRotator>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AuraCinema.Domain.Models.Chat.LlmOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<AuraCinema.Services.Chat.ApiKeyRotator>>();
    return new AuraCinema.Services.Chat.ApiKeyRotator(options.GetAllKeys(), logger);
});
builder.Services.AddSingleton<AuraCinema.Services.Chat.ChatRateLimiter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AuraCinema.Domain.Models.Chat.LlmOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<AuraCinema.Services.Chat.ChatRateLimiter>>();
    return new AuraCinema.Services.Chat.ChatRateLimiter(options.RateLimitMaxRequests, options.RateLimitWindowSeconds, logger);
});

builder.Services.AddScoped<ILlmClient, AuraCinema.Services.Chat.GeminiClient>();
builder.Services.AddScoped<IChatService, AuraCinema.Services.Chat.ChatService>();
builder.Services.AddScoped<AuraCinema.Services.Chat.Tools.ToolRegistry>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ===== Seed DB =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try { await DbInitializer.SeedAsync(db); }
    catch (Exception ex) { Log.Warning(ex, "Seed skipped — DB may not be ready"); }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Area route (Admin / Staff)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
