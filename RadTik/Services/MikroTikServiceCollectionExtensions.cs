using Microsoft.Extensions.DependencyInjection;

namespace RadTik.Services;

public static class MikroTikServiceCollectionExtensions
{
    public static IServiceCollection AddMikroTikServices(this IServiceCollection services)
    {
        // Register a single concrete implementation behind both contracts.
        services.AddScoped<MikroTikService>();
        services.AddScoped<IMikroTikProfilesService>(sp => sp.GetRequiredService<MikroTikService>());
        services.AddScoped<IMikroTikUsersService>(sp => sp.GetRequiredService<MikroTikService>());

        return services;
    }
}
