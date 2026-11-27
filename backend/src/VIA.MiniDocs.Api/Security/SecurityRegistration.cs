using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

internal static class SecurityRegistration
{
  internal static void AddMiniDocsSecurity(this WebApplicationBuilder builder, bool localOperation)
  {
    var services = builder.Services;
    services.AddAuthorizationBuilder()
        .AddPolicy(WorkspacePolicies.Session, policy => policy.RequireAuthenticatedUser())
        .AddPolicy(WorkspacePolicies.Administrator, policy => policy.RequireAuthenticatedUser()
            .RequireRole(AdministratorAccounts.Role))
        .AddPolicy(WorkspacePolicies.ActiveWorkspace, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveWorkspaceRequirement()));
    services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ActiveWorkspaceHandler>();
    services.AddScoped<AdministratorInvitations>();
    services.AddScoped<Registration>();
    services.AddSingleton<RegistrationRateLimits>();
    services.AddSingleton<InvitationIssueLimits>();
    services.AddSingleton<RecoveryRateLimits>();
    services.AddSingleton<AdministratorCursor>();
    // Database exceptions can contain input values supplied by the server.
    // HTTP reports a safe status and trace identifier after catching the failure.
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.None);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
    services.AddDbContext<MiniDocsDbContext>(options => options.UseNpgsql(
        builder.Configuration.GetConnectionString("MiniDocs"), postgres => postgres.SetPostgresVersion(18, 7)));
    services.AddIdentity<MiniDocsUser, IdentityRole<Guid>>(options =>
    {
      options.Password.RequiredLength = 12;
      options.Password.RequiredUniqueChars = 1;
      options.Password.RequireDigit = false;
      options.Password.RequireUppercase = false;
      options.Password.RequireLowercase = false;
      options.Password.RequireNonAlphanumeric = false;
      options.Lockout.AllowedForNewUsers = true;
      options.Lockout.MaxFailedAccessAttempts = 5;
      options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<MiniDocsDbContext>()
    .AddPasswordValidator<MiniDocsPasswordValidator>();
    services.ConfigureApplicationCookie(options =>
    {
      options.Cookie.Name = SessionHttp.SessionCookie;
      options.Cookie.Path = "/";
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.Cookie.HttpOnly = true;
      options.SlidingExpiration = false;
      options.EventsType = typeof(MiniDocsCookieEvents);
    });
    services.AddOptions<SessionLifetime>().BindConfiguration("Security:Session")
        .Validate(static limits => limits.IdleMinutes is > 0 and <= 30 &&
            limits.AbsoluteMinutes >= limits.IdleMinutes && limits.AbsoluteMinutes <= 480)
        .ValidateOnStart();
    services.AddScoped<MiniDocsCookieEvents>();
    services.AddScoped<SessionStore>();
    services.AddScoped<AdministratorAccounts>();
    services.AddScoped<AdministratorBootstrap>();
    services.AddSingleton<DummyPassword>();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<LoginRateLimits>();
    services.AddSingleton<IAntiforgeryAdditionalDataProvider, SessionAntiforgeryBinding>();
    services.AddAntiforgery(options =>
    {
      options.Cookie.Name = SessionHttp.AntiforgeryCookie;
      options.Cookie.Path = "/";
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Strict;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.HeaderName = SessionHttp.AntiforgeryHeader;
      options.SuppressReadingTokenFromFormBody = true;
    });
    services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.KnownProxies.Clear();
      options.KnownIPNetworks.Clear();
      foreach (var proxy in builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
      // Empty trust list means forwarded headers are entirely disabled.
      options.ForwardedHeaders = options.KnownProxies.Count == 0 ? ForwardedHeaders.None :
          ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.ForwardLimit = 1;
      options.RequireHeaderSymmetry = true;
    });
    if (!localOperation && !builder.Environment.IsEnvironment("ContractTests"))
      ProtectedKeyRing.Configure(services);
  }
}

internal sealed class SessionLifetime
{
  public int IdleMinutes { get; set; } = 30;
  public int AbsoluteMinutes { get; set; } = 480;
  internal TimeSpan Idle => TimeSpan.FromMinutes(IdleMinutes);
  internal TimeSpan Absolute => TimeSpan.FromMinutes(AbsoluteMinutes);
}

internal sealed class DummyPassword(IOptions<PasswordHasherOptions> options)
{
  private static readonly MiniDocsUser Dummy = new();
  private readonly string hash = new PasswordHasher<MiniDocsUser>(options)
      .HashPassword(Dummy, Guid.NewGuid().ToString("N"));

  internal void Verify(IPasswordHasher<MiniDocsUser> hasher, string password) =>
      _ = hasher.VerifyHashedPassword(Dummy, hash, password);
}
