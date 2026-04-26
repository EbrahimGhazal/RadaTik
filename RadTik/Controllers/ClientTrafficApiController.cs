using System.Globalization;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Dtos.Traffic;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.Traffic;
using tik4net;

namespace RadTik.Controllers;

[ApiController]
[Route("api/client/traffic")]
[Authorize(Roles = RoleNames.Client)]
public sealed class ClientTrafficApiController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, DateTime> LastRequestUtcByUser = new();
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TrafficRateTracker _rateTracker;
    private readonly int _minIntervalMs;
    private readonly int _testDurationSeconds;
    private readonly int _testCooldownHours;
    private readonly decimal _testChargeAmount;

    public ClientTrafficApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        TrafficRateTracker rateTracker,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _rateTracker = rateTracker;
        _minIntervalMs = Math.Clamp(configuration.GetValue("Traffic:ClientLiveMinIntervalMs", 2000), 250, 60_000);
        _testDurationSeconds = Math.Clamp(configuration.GetValue("Traffic:ClientTestDurationSeconds", 10), 5, 120);
        _testCooldownHours = Math.Clamp(configuration.GetValue("Traffic:ClientTestCooldownHours", 4), 1, 168);
        _testChargeAmount = configuration.GetValue<decimal>("Traffic:ClientTestChargeAmount", 50m);
        if (_testChargeAmount < 0)
        {
            _testChargeAmount = 0m;
        }
    }

    /// <summary>
    /// Returns only the logged-in client's live PPP session traffic.
    /// </summary>
    [HttpGet("live")]
    public async Task<ActionResult<ClientLiveTrafficDto>> GetLive(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.ClientId is not int clientId)
        {
            return NotFound();
        }

        if (!AllowRequestNow(user.Id, out var retryAfterSeconds))
        {
            Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Too many requests. Please retry after a moment."
            });
        }

        var client = await _context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client == null || !client.IsActive || !client.MikroTikServerId.HasValue || string.IsNullOrWhiteSpace(client.UserName))
        {
            return NotFound();
        }

        var testStatus = await GetCurrentTestStatusAsync(clientId, cancellationToken);
        if (!testStatus.TestActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Traffic test is not active. Start a new 10-second test first."
            });
        }

        var server = await _context.MikroTikServers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == client.MikroTikServerId.Value && s.IsActive, cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        var utcNow = DateTime.UtcNow;
        var dto = new ClientLiveTrafficDto
        {
            Connected = false,
            UtcIso = utcNow.ToString("o"),
            UserName = client.UserName,
            ClientName = client.Name ?? client.UserName,
            ServerName = server.Name,
        };

        using var connection = ConnectionFactory.OpenConnection(
            TikConnectionType.Api,
            server.Host,
            server.Port,
            server.User,
            server.Pass);

        var sessions = connection.CreateCommand("/ppp/active/print").ExecuteList();
        var row = sessions.FirstOrDefault(x =>
            string.Equals(GetWordCi(x, "name"), client.UserName, StringComparison.OrdinalIgnoreCase));

        if (row == null)
        {
            return Ok(dto);
        }

        var rxBytes = ParseLong(GetWordCi(row, "rx-byte"));
        var txBytes = ParseLong(GetWordCi(row, "tx-byte"));
        var rxPackets = ParseLong(CoalesceWordCi(row, "rx-packet", "rx-packets"));
        var txPackets = ParseLong(CoalesceWordCi(row, "tx-packet", "tx-packets"));
        var (rxBps, txBps) = _rateTracker.UpdateAndComputeRates(
            server.Id,
            $"ppp:{client.UserName}",
            rxBytes,
            txBytes,
            utcNow,
            streamKey: "client-live");

        dto.Connected = true;
        dto.Address = CoalesceWordCi(row, "address", "remote-address");
        dto.MacAddress = CoalesceWordCi(row, "caller-id", "mac-address");
        dto.Uptime = GetWordCi(row, "uptime");
        dto.RxBytes = rxBytes;
        dto.TxBytes = txBytes;
        dto.RxPackets = rxPackets;
        dto.TxPackets = txPackets;
        dto.RxBps = rxBps;
        dto.TxBps = txBps;
        return Ok(dto);
    }

    [HttpGet("test-status")]
    public async Task<ActionResult<ClientTrafficTestStatusDto>> GetTestStatus(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.ClientId is not int clientId)
        {
            return NotFound();
        }

        var client = await _context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null)
        {
            return NotFound();
        }

        var status = await GetCurrentTestStatusAsync(clientId, cancellationToken);
        status.CurrentBalance = client.Balance;
        return Ok(status);
    }

    [HttpPost("start-test")]
    public async Task<ActionResult<ClientTrafficTestStatusDto>> StartTest(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.ClientId is not int clientId)
        {
            return NotFound();
        }

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null || !client.IsActive || !client.MikroTikServerId.HasValue || string.IsNullOrWhiteSpace(client.UserName))
        {
            return BadRequest(new { message = "Client account is not ready for traffic test." });
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        var currentStatus = await GetCurrentTestStatusAsync(clientId, cancellationToken);
        if (!currentStatus.CanStartTest)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                message = "Test cannot be started yet.",
                nextEligibleUtcIso = currentStatus.NextEligibleUtcIso,
                secondsRemaining = currentStatus.SecondsRemaining
            });
        }

        if (client.Balance < _testChargeAmount)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                message = "Insufficient balance for traffic test.",
                requiredAmount = _testChargeAmount,
                currentBalance = client.Balance
            });
        }

        var utcNow = DateTime.UtcNow;
        var previous = client.Balance;
        client.Balance -= _testChargeAmount;
        client.LastUpdated = DateTime.Now;

        _context.ClientTrafficTestSessions.Add(new ClientTrafficTestSession
        {
            ClientId = client.Id,
            StartedAtUtc = utcNow,
            DurationSeconds = _testDurationSeconds,
            ChargeAmount = _testChargeAmount,
            PreviousBalance = previous,
            NewBalance = client.Balance,
            CreatedByUserId = user.Id
        });

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var status = await GetCurrentTestStatusAsync(clientId, cancellationToken);
        status.CurrentBalance = client.Balance;
        return Ok(status);
    }

    private async Task<ClientTrafficTestStatusDto> GetCurrentTestStatusAsync(int clientId, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var last = await _context.ClientTrafficTestSessions.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (last == null)
        {
            return new ClientTrafficTestStatusDto
            {
                TestActive = false,
                CanStartTest = true,
                DurationSeconds = _testDurationSeconds,
                CooldownHours = _testCooldownHours,
                ChargeAmount = _testChargeAmount,
                SecondsRemaining = 0
            };
        }

        var activeUntil = last.StartedAtUtc.AddSeconds(last.DurationSeconds);
        var testActive = utcNow < activeUntil;
        var nextEligible = last.StartedAtUtc.AddHours(_testCooldownHours);
        var canStart = utcNow >= nextEligible;

        return new ClientTrafficTestStatusDto
        {
            TestActive = testActive,
            CanStartTest = canStart,
            ActiveUntilUtcIso = activeUntil.ToString("o"),
            NextEligibleUtcIso = canStart ? null : nextEligible.ToString("o"),
            DurationSeconds = _testDurationSeconds,
            CooldownHours = _testCooldownHours,
            ChargeAmount = _testChargeAmount,
            SecondsRemaining = canStart
                ? 0
                : Math.Max(1, (int)Math.Ceiling((nextEligible - utcNow).TotalSeconds))
        };
    }

    private bool AllowRequestNow(string userId, out int retryAfterSeconds)
    {
        retryAfterSeconds = 1;
        var now = DateTime.UtcNow;
        if (!LastRequestUtcByUser.TryGetValue(userId, out var prev))
        {
            LastRequestUtcByUser[userId] = now;
            return true;
        }

        var elapsedMs = (now - prev).TotalMilliseconds;
        if (elapsedMs < _minIntervalMs)
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((_minIntervalMs - elapsedMs) / 1000d));
            return false;
        }

        LastRequestUtcByUser[userId] = now;
        return true;
    }

    private static string GetWordCi(ITikReSentence row, string key)
    {
        foreach (var kv in row.Words)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value ?? "";
            }
        }

        return "";
    }

    private static string CoalesceWordCi(ITikReSentence row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetWordCi(row, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static long ParseLong(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0;
        }

        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
