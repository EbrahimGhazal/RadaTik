using Microsoft.EntityFrameworkCore;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data;

public partial class ApplicationDbContext
{
    /// <summary>عند true لا يُطبَّق عزل الشبكة (افتراضي: معطّل لمهام الخلفية والهجرات).</summary>
    public bool NetworkQueryFilterDisabled { get; private set; } = true;

    private readonly List<int> _networkFilterIds = [];

    public IReadOnlyList<int> NetworkFilterIds => _networkFilterIds;

    public void ApplyNetworkScope(ICurrentNetworkScope scope)
    {
        _networkFilterIds.Clear();
        NetworkQueryFilterDisabled = !scope.IsFilterActive || scope.BypassAllNetworks;
        if (!NetworkQueryFilterDisabled)
        {
            _networkFilterIds.AddRange(scope.AccessibleNetworkIds);
        }
    }

    private void ConfigureNetworkTenantQueryFilters(ModelBuilder modelBuilder)
    {
        NetworkQueryFilterConfigurator.Apply(this, modelBuilder);
        DependentEntityQueryFilterConfigurator.Apply(this, modelBuilder);
    }

    internal void ConfigureNetworkEntityFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Network>().HasQueryFilter(n =>
            NetworkQueryFilterDisabled || NetworkFilterIds.Contains(n.Id));
    }

    internal void ConfigureTenantEntityFilter<TEntity>(
        ModelBuilder modelBuilder,
        string propertyName,
        bool isNullable)
        where TEntity : class
    {
        if (isNullable)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                NetworkQueryFilterDisabled
                || EF.Property<int?>(e, propertyName) == null
                || NetworkFilterIds.Contains(EF.Property<int?>(e, propertyName)!.Value));
        }
        else
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                NetworkQueryFilterDisabled
                || NetworkFilterIds.Contains(EF.Property<int>(e, propertyName)));
        }
    }
}
