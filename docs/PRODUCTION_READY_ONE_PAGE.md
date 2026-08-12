# RadaTik Production One-Page (Deployment Window)

Use this quick sheet during the deployment window.

## Before Deploy (Mandatory)

- Build: `PASS`
- Tests: `PASS`
- DB backup: `DONE`
- Rollback package: `READY`
- Rollback owner assigned: `YES`

If any item above is not complete: **NO-GO**.

## Deploy Steps (T0)

1. Announce deploy start.
2. Deploy release package.
3. Run smoke checks:
   - `/health` = `200`
   - login works
   - one critical business flow works

## Immediate Checks (First 15 min)

- No unhandled exception spikes.
- No DB timeout/deadlock spikes.
- MikroTik connectivity healthy.
- Wallet/billing writes are correct.

## Go/No-Go Decision

- `GO` when all mandatory and smoke checks pass.
- `NO-GO` when any mandatory item fails or smoke checks fail.

## Rollback Trigger (Immediate)

Rollback now if any of the following occurs:

- Health endpoint unstable.
- Login or critical flow broken.
- Financial data inconsistency detected.
- Repeated critical errors in logs.

## Quick Record

```text
Release ID:
Deploy Start (UTC):
Deploy End (UTC):
Decision: GO / NO-GO
Approved By:
Rollback Used: YES / NO
Notes:
```
