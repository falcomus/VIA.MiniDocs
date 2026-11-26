using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

public static class ViaDocsRegistration
{
    internal const string ClientName = "ViaDocs";
    internal const string AuthClientName = "ViaDocs.Auth";
    internal const string WriteClientName = "ViaDocs.Write";

    public static IServiceCollection AddViaDocs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ViaDocsOptions>().Bind(configuration.GetSection("ViaDocs"))
            .Validate(options => options.IsValid(), "Invalid VIA.Docs configuration.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ViaDocsTokenCache>();
        services.AddSingleton<ViaDocsAuthTransport>();
        services.AddScoped<ViaDocsAuthentication>();
        services.AddScoped<IViaDocsAuthentication>(provider => provider.GetRequiredService<ViaDocsAuthentication>());
        services.AddScoped<IViaDocsFiles, ViaDocsFiles>();
        AddTransport(services, ClientName, true);
        AddTransport(services, AuthClientName, false);
        AddTransport(services, WriteClientName, true);
        return services;
    }

    private static void AddTransport(IServiceCollection services, string name, bool sensitiveTarget) =>
        services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(provider => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
                MaxResponseDrainSize = 0,
                ConnectTimeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<ViaDocsOptions>>().Value.ConnectSeconds),
                ActivityHeadersPropagator = sensitiveTarget ? null : DistributedContextPropagator.CreateNoOutputPropagator(),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
}
