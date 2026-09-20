using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services.Calibration;

public interface ICalibrationScenarioService
{
    Task<IReadOnlyList<CalibrationScenario>> ListAsync(int networkId, CancellationToken ct = default);
    Task<CalibrationScenario?> GetAsync(int networkId, int id, CancellationToken ct = default);
    Task EnsureDefaultsAsync(int networkId, CancellationToken ct = default);
    Task<CalibrationScenario> CreateAsync(CalibrationScenario scenario, CancellationToken ct = default);
    Task<CalibrationScenario?> UpdateAsync(CalibrationScenario scenario, CancellationToken ct = default);
    Task<bool> DeleteAsync(int networkId, int id, CancellationToken ct = default);
    Task<bool> SetDefaultAsync(int networkId, int id, CancellationToken ct = default);
}

public sealed class CalibrationScenarioService(ApplicationDbContext db) : ICalibrationScenarioService
{
    public async Task EnsureDefaultsAsync(int networkId, CancellationToken ct = default)
    {
        bool any = await db.CalibrationScenarios.AsNoTracking()
            .AnyAsync(s => s.NetworkId == networkId, ct);
        if (any)
        {
            return;
        }

        db.CalibrationScenarios.AddRange(CalibrationScenarioTemplates.BuildDefaults(networkId));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CalibrationScenario>> ListAsync(int networkId, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(networkId, ct);
        return await db.CalibrationScenarios.AsNoTracking()
            .Where(s => s.NetworkId == networkId && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);
    }

    public Task<CalibrationScenario?> GetAsync(int networkId, int id, CancellationToken ct = default) =>
        db.CalibrationScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.NetworkId == networkId && s.Id == id, ct);

    public async Task<CalibrationScenario> CreateAsync(CalibrationScenario scenario, CancellationToken ct = default)
    {
        Normalize(scenario);
        scenario.CreatedAtUtc = DateTime.UtcNow;
        scenario.UpdatedAtUtc = scenario.CreatedAtUtc;
        if (scenario.IsDefault)
        {
            await ClearDefaultsAsync(scenario.NetworkId, ct);
        }

        db.CalibrationScenarios.Add(scenario);
        await db.SaveChangesAsync(ct);
        return scenario;
    }

    public async Task<CalibrationScenario?> UpdateAsync(CalibrationScenario scenario, CancellationToken ct = default)
    {
        CalibrationScenario? row = await db.CalibrationScenarios
            .FirstOrDefaultAsync(s => s.NetworkId == scenario.NetworkId && s.Id == scenario.Id, ct);
        if (row == null)
        {
            return null;
        }

        Normalize(scenario);
        if (scenario.IsDefault)
        {
            await ClearDefaultsAsync(scenario.NetworkId, ct);
        }

        row.Name = scenario.Name;
        row.Description = scenario.Description;
        row.IsDefault = scenario.IsDefault;
        row.IsActive = scenario.IsActive;
        row.CrewMode = scenario.CrewMode;
        row.AimMode = scenario.AimMode;
        row.AuthMode = scenario.AuthMode;
        row.DisplayMode = scenario.DisplayMode;
        row.SuccessMode = scenario.SuccessMode;
        row.MinSignalDbm = scenario.MinSignalDbm;
        row.PeakHoldSeconds = scenario.PeakHoldSeconds;
        row.RequireIpOrMac = scenario.RequireIpOrMac;
        row.ShowSnrCcq = scenario.ShowSnrCcq;
        row.ShowLosHint = scenario.ShowLosHint;
        row.ShowGeometryTargets = scenario.ShowGeometryTargets;
        row.SortOrder = scenario.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteAsync(int networkId, int id, CancellationToken ct = default)
    {
        CalibrationScenario? row = await db.CalibrationScenarios
            .FirstOrDefaultAsync(s => s.NetworkId == networkId && s.Id == id, ct);
        if (row == null)
        {
            return false;
        }

        bool wasDefault = row.IsDefault;
        db.CalibrationScenarios.Remove(row);
        await db.SaveChangesAsync(ct);
        if (wasDefault)
        {
            CalibrationScenario? next = await db.CalibrationScenarios
                .Where(s => s.NetworkId == networkId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync(ct);
            if (next != null)
            {
                next.IsDefault = true;
                next.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    public async Task<bool> SetDefaultAsync(int networkId, int id, CancellationToken ct = default)
    {
        CalibrationScenario? row = await db.CalibrationScenarios
            .FirstOrDefaultAsync(s => s.NetworkId == networkId && s.Id == id, ct);
        if (row == null)
        {
            return false;
        }

        await ClearDefaultsAsync(networkId, ct);
        row.IsDefault = true;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ClearDefaultsAsync(int networkId, CancellationToken ct)
    {
        List<CalibrationScenario> defaults = await db.CalibrationScenarios
            .Where(s => s.NetworkId == networkId && s.IsDefault)
            .ToListAsync(ct);
        foreach (CalibrationScenario item in defaults)
        {
            item.IsDefault = false;
        }
    }

    private static void Normalize(CalibrationScenario scenario)
    {
        scenario.Name = string.IsNullOrWhiteSpace(scenario.Name) ? "سيناريو" : scenario.Name.Trim();
        scenario.Description = string.IsNullOrWhiteSpace(scenario.Description) ? null : scenario.Description.Trim();
        scenario.CrewMode = CalibrationOptionValues.NormalizeCrew(scenario.CrewMode);
        scenario.AimMode = CalibrationOptionValues.NormalizeAim(scenario.AimMode);
        scenario.AuthMode = CalibrationOptionValues.NormalizeAuth(scenario.AuthMode);
        scenario.DisplayMode = CalibrationOptionValues.NormalizeDisplay(scenario.DisplayMode);
        scenario.SuccessMode = CalibrationOptionValues.NormalizeSuccess(scenario.SuccessMode);
        scenario.PeakHoldSeconds = Math.Clamp(scenario.PeakHoldSeconds <= 0 ? 3 : scenario.PeakHoldSeconds, 1, 15);
        if (scenario.AimMode is CalibrationOptionValues.AimGeometry or CalibrationOptionValues.AimCompass or CalibrationOptionValues.AimPro)
        {
            scenario.ShowGeometryTargets = true;
        }

        if (scenario.AimMode == CalibrationOptionValues.AimPro)
        {
            scenario.ShowSnrCcq = true;
            if (scenario.SuccessMode == CalibrationOptionValues.SuccessPeak)
            {
                scenario.SuccessMode = CalibrationOptionValues.SuccessHold;
            }
        }
    }
}
