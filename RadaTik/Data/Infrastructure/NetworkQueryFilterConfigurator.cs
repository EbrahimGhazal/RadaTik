using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RadaTik.Models;

namespace RadaTik.Data.Infrastructure;

internal static class NetworkQueryFilterConfigurator
{
    private static readonly HashSet<string> TenantPropertyNames = new(StringComparer.Ordinal)
    {
        "NetworkId",
        "CompanyNetworkId",
        "TargetNetworkId"
    };

    public static void Apply(ApplicationDbContext db, ModelBuilder modelBuilder)
    {
        db.ConfigureNetworkEntityFilter(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clrType = entityType.ClrType;
            if (clrType == typeof(Network))
            {
                continue;
            }

            PropertyInfo? tenantProperty = clrType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => TenantPropertyNames.Contains(p.Name)
                    && (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)));

            if (tenantProperty == null)
            {
                continue;
            }

            MethodInfo method = typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplicationDbContext.ConfigureTenantEntityFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(clrType);

            method.Invoke(db, [modelBuilder, tenantProperty.Name, tenantProperty.PropertyType == typeof(int?)]);
        }
    }
}
