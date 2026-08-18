using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.MikroTikSync;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MikroTikSaveChangesInterceptorTests
{
    [Fact]
    public async Task SavedChanges_EnqueuesClientAddWithGeneratedId()
    {
        RecordingQueue queue = new();
        MikroTikSaveChangesInterceptor interceptor = new(queue);
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"interceptor-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using ApplicationDbContext db = new(options);
        db.Clients.Add(new Client
        {
            Name = "New",
            SID = "1",
            UserName = "mt-user",
            Password = "secret",
            ProfileId = 1,
            PhoneNumber = "099",
            MikroTikServerId = 8
        });

        await db.SaveChangesAsync();

        MikroTikSyncJob job = Assert.Single(queue.Jobs);
        Assert.Equal(nameof(Client), job.EntityType);
        Assert.Equal(MikroTikSyncAction.Add, job.Action);
        Assert.Equal(8, job.ServerId);
        Assert.Equal("mt-user", job.UserName);
        Assert.True(job.EntityId > 0);
    }

    private sealed class RecordingQueue : IMikroTikSyncQueue
    {
        public List<MikroTikSyncJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(MikroTikSyncJob job, CancellationToken cancellationToken = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public ValueTask<MikroTikSyncJob> DequeueAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
