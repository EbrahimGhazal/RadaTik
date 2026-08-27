using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
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

    [Fact]
    public async Task SavedChanges_PersonalFieldsOnly_DoesNotEnqueueClientUpdate()
    {
        RecordingQueue queue = new();
        MikroTikSaveChangesInterceptor interceptor = new(queue);
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"interceptor-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using ApplicationDbContext db = new(options);
        Client client = new()
        {
            Name = "Old",
            SID = "1",
            UserName = "mt-user",
            Password = "secret",
            ProfileId = 1,
            PhoneNumber = "099",
            MikroTikServerId = 8
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        queue.Jobs.Clear();

        client.Name = "New Personal Name";
        client.PhoneNumber = "0988888888";
        await db.SaveChangesAsync();

        Assert.Empty(queue.Jobs);
    }

    [Fact]
    public async Task SavedChanges_PasswordChange_EnqueuesClientUpdate()
    {
        RecordingQueue queue = new();
        MikroTikSaveChangesInterceptor interceptor = new(queue);
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"interceptor-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using ApplicationDbContext db = new(options);
        Client client = new()
        {
            Name = "User",
            SID = "1",
            UserName = "mt-user",
            Password = "secret",
            ProfileId = 1,
            PhoneNumber = "099",
            MikroTikServerId = 8
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        queue.Jobs.Clear();

        client.Password = "new-secret";
        await db.SaveChangesAsync();

        MikroTikSyncJob job = Assert.Single(queue.Jobs);
        Assert.Equal(MikroTikSyncAction.Update, job.Action);
        Assert.Equal("mt-user", job.UserName);
    }

    [Fact]
    public async Task SavedChanges_PendingManagerApproval_DoesNotEnqueueClientAdd()
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
            Name = "Pending",
            SID = "1",
            UserName = "pending-user",
            Password = "secret",
            ProfileId = 1,
            PhoneNumber = "099",
            MikroTikServerId = 8,
            IsActive = false,
            ConnectionStatus = EmployeeApprovalStates.PendingClientConnectionStatus
        });

        await db.SaveChangesAsync();

        Assert.Empty(queue.Jobs);
    }

    [Fact]
    public async Task SavedChanges_AfterPendingApprovalActivated_EnqueuesClientUpdate()
    {
        RecordingQueue queue = new();
        MikroTikSaveChangesInterceptor interceptor = new(queue);
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"interceptor-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using ApplicationDbContext db = new(options);
        Client client = new()
        {
            Name = "Pending",
            SID = "1",
            UserName = "pending-user",
            Password = "secret",
            ProfileId = 1,
            PhoneNumber = "099",
            MikroTikServerId = 8,
            IsActive = false,
            ConnectionStatus = EmployeeApprovalStates.PendingClientConnectionStatus
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        queue.Jobs.Clear();

        client.IsActive = true;
        client.ConnectionStatus = "مفعل";
        await db.SaveChangesAsync();

        MikroTikSyncJob job = Assert.Single(queue.Jobs);
        Assert.Equal(MikroTikSyncAction.Update, job.Action);
        Assert.Equal("pending-user", job.UserName);
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
