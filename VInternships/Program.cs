using VInternships.Demo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace VInternships;
public static class Program
{
    public static void Main(string[] args) => BuildApplication(args).Run();
    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(Program).Assembly.GetName().Name });
        configure?.Invoke(builder);
        var root = Path.GetFullPath(builder.Configuration["Demo:DataDirectory"] ?? Environment.GetEnvironmentVariable("VINTERNSHIPS_DEMO_DATA_DIR")
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "PersonalDemo"));
        var publicRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
        if ((root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(publicRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new InvalidOperationException("Demo keys must be outside public assets.");
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var protection = builder.Services.AddDataProtection().SetApplicationName("vinternships-personal-demo").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(root, "Keys")));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(45); options.Cookie.Name = "vinternships-demo.session";
            options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.SecurePolicy = secure; options.Cookie.IsEssential = true;
        });
        builder.Services.AddAntiforgery(options => { options.Cookie.Name = "vinternships-demo.antiforgery"; options.Cookie.SecurePolicy = secure; });
        builder.Services.Configure<CookieTempDataProviderOptions>(options => { options.Cookie.Name = "vinternships-demo.messages"; options.Cookie.SecurePolicy = secure; });
        builder.Services.AddControllersWithViews(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });
        builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddScoped<DemoContext>(); builder.Services.AddScoped<PlacementStore>();
        builder.Services.Configure<FormOptions>(options => { options.MultipartBodyLengthLimit = 64 * 1024; options.ValueLengthLimit = 4096; options.ValueCountLimit = 64; });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
        builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 64 * 1024; options.Limits.MaxConcurrentConnections = 100; });
        var app = builder.Build();
        if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
        app.Use(async (http, next) =>
        {
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            http.Response.Headers["X-Frame-Options"] = "DENY";
            await next(http);
        });
        app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseSession();
        app.UseMiddleware<WorkspaceMiddleware>();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", application = "vinternships-demo" }));
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        return app;
    }
}
