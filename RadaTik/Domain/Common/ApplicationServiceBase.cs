using RadaTik.Data;

namespace RadaTik.Domain.Common;

/// <summary>قاعدة وراثية لخدمات التطبيق التي تعتمد على قاعدة البيانات (تكوين + تغليف DbContext).</summary>
public abstract class ApplicationServiceBase
{
    protected ApplicationDbContext Db { get; }

    protected ApplicationServiceBase(ApplicationDbContext db)
    {
        Db = db;
    }
}
