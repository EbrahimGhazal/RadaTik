# Radio Control Engineering Study (RadaTik)

## Objective
- Build a safe, high-performance workflow to monitor sector RF metrics and control frequency changes from the app.
- Keep operations auditable and aligned with current visual identity and role-based access.

## Current baseline in project
- Sector management exists (CRUD, permissions, network scoping).
- Queue/worker baseline exists and can be reused for radio jobs:
  - `MikroTikSyncQueue`
  - `MikroTikSyncBackgroundService`
- Feature/permission model is already in place for controlled rollout.

## Scope recommendation
1. **Phase 0: Discovery/PoC**
   - Validate sector reachability through available MikroTik servers.
   - Test metrics read against representative device families.
2. **Phase 1: Monitoring Only**
   - Periodic metrics polling (read-only), dashboard, trend, alert thresholds.
3. **Phase 2: Controlled Write Operations**
   - Frequency change request workflow with approval, verification, rollback.

## Key scenarios
1. **Live monitoring**
   - Poll frequency/noise/signal/SNR/CCQ with graceful retry and timeout.
2. **Controlled frequency change**
   - Request -> approve -> queue execution -> verify -> rollback if required.
3. **Failure containment**
   - Automatic alerting and operation logs when device unreachable or command fails.

## Data model to introduce (proposed)
- `SectorDeviceBinding`
- `SectorRadioMetricSample`
- `SectorRadioChangeRequest`
- `SectorRadioOperationLog`

## Performance principles
- Staggered polling windows to avoid burst load.
- `AsNoTracking()` on analytical reads.
- Bounded retries and per-sector execution lock.
- Retention policy for metric samples and raw payloads.

## Risk controls (mandatory)
- No direct write from UI, execution only via worker jobs.
- Pre-change snapshot and deterministic rollback path.
- Full audit trail (who/when/what/result).
- Separate permissions for monitoring vs control.

## UX / visual identity
- Dedicated engineering study page for decision and rollout readiness.
- Compact status cards + phase roadmap + scenario matrix.
- Consistent style with CompanyAdmin Area theme and dark-mode-safe contrast.

## Deliverables for execution
- Backend job contracts for monitoring/control.
- Adapter resolver for sector device family.
- Dashboard pages for health + operation timeline.
- Alerts and reporting package for operations team.
