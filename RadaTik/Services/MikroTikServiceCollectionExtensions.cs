using Microsoft.Extensions.DependencyInjection;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services;

public static class MikroTikServiceCollectionExtensions
{
    public static IServiceCollection AddMikroTikServices(this IServiceCollection services)
    {
        services.AddScoped<MikroTikConnectionSupport>();
        services.AddScoped<IMikroTikProbeService, MikroTikProbeService>();
        services.AddScoped<MikroTikService>();
        services.AddScoped<MikroTikUserService>();
        services.AddScoped<MikroTikUserImportService>();
        services.AddScoped<MikroTikUsersFacade>();

        services.AddScoped<IMikroTikPppoeUserService>(sp => sp.GetRequiredService<MikroTikUserService>());
        services.AddScoped<IMikroTikUserImportService>(sp => sp.GetRequiredService<MikroTikUserImportService>());
        services.AddScoped<IMikroTikProfilesService>(sp => sp.GetRequiredService<MikroTikService>());
        services.AddScoped<IMikroTikProfileSyncService>(sp => sp.GetRequiredService<MikroTikService>());
        services.AddScoped<IMikroTikUsersService>(sp => sp.GetRequiredService<MikroTikUsersFacade>());
        services.AddScoped<IMikroTikSectorService>(sp => sp.GetRequiredService<MikroTikService>());

        return services;
    }
}
