# RadaTik Production Ready Checklist

This checklist is used before and after deploying `RadaTik` to production.

## 1) Pre-Deploy (T-24h to T-1h)

- Confirm clean CI status:
  - `dotnet build RadaTik/RadaTik.csproj -c Release`
  - `dotnet test RadaTik.Tests/RadaTik.Tests.csproj`
- Validate production configuration:
  - `ConnectionStrings:MyDBConnection`
  - `RADATIK_BOOTSTRAP_ADMIN_PASSWORD` (only if bootstrap operation is needed)
  - `RADATIK_INSECURE_HTTP` is **false** in production unless behind trusted internal TLS termination policy
- Verify database migration safety:
  - Review pending migrations and expected schema changes.
  - Backup production database before release.
- Confirm external dependencies availability:
  - SQL Server reachable.
  - MikroTik API endpoints reachable from app host.
- Verify static assets:
  - Frontend build artifacts under `wwwroot/app` are present for release package.

## 2) Deploy Window (T0)

- Put deployment notice for operations team.
- Deploy application package to target environment.
- Run smoke checks immediately:
  - `GET /health` returns `200`.
  - Login flow works for system admin and company manager.
  - Dashboard and critical pages load without server errors.

## 3) Post-Deploy Validation (First 15 minutes)

- Validate core business flows:
  - Create subscriber (wizard path) success.
  - Import users from MikroTik (preview + import) success.
  - Wallet and billing operations post correctly.
- Validate data correctness:
  - New records in `Clients`, `NetworkWalletTransactions`, and invoice tables are consistent.
  - No duplicated subscriber records in company scope.
- Validate UI/UX/accessibility quick checks:
  - Sidebar keyboard toggle (Enter/Space) works.
  - Alerts render and dismiss correctly.
  - Focus visible on key form fields.

## 4) Monitoring (First 24h)

- Track error rates and application logs every 1-2 hours:
  - `Unhandled exceptions`
  - `MikroTik connectivity failures`
  - `Database timeout/deadlock patterns`
- Monitor performance baseline:
  - API latency for subscriber creation/import endpoints.
  - Background job execution stability.
- Monitor business safety indicators:
  - Failed billing actions.
  - Unexpected wallet balance changes.
  - Approval/request flow failures.

## 5) Rollback Plan (If Critical Issue)

- Roll back application binary/package to previous stable version.
- If migration is reversible and needed, execute approved rollback script; otherwise restore DB backup.
- Re-run smoke checks:
  - `/health`
  - login
  - one core business flow
- Publish incident summary with:
  - root trigger,
  - impact scope,
  - rollback timestamp,
  - next mitigation action.

## 6) Release Sign-Off

- Mark release as complete only when:
  - Build/tests green,
  - health checks stable,
  - core flows validated,
  - no critical alerts in first 24h,
  - operations sign-off recorded.

## 7) Go/No-Go Quick Gate

Use this as a final 2-minute decision gate before deployment:

- `GO` only if all are true:
  - Build and tests are green on the candidate commit.
  - Database backup is completed and validated.
  - Rollback package and rollback owner are confirmed.
  - Health endpoint is reachable on target after deployment.
  - One critical business flow passes smoke test.
- `NO-GO` if any of the following happens:
  - Unknown migration impact.
  - Unresolved P1/P2 production bug.
  - Missing backup or rollback owner.
  - Failed health check or login smoke test.

## 8) Release Approval Form (Fill Before Deploy)

Use this form as a release record for each production deployment:

```text
Release ID:
Release Date/Time (UTC):
Release Owner:
Rollback Owner:

Commit/Tag:
Environment:

Build Status: PASS / FAIL
Test Status: PASS / FAIL
DB Backup Verified: YES / NO
Migration Impact Reviewed: YES / NO
Rollback Package Ready: YES / NO

Smoke Test (/health): PASS / FAIL
Smoke Test (login): PASS / FAIL
Smoke Test (critical flow): PASS / FAIL

Go/No-Go Decision: GO / NO-GO
Decision By:
Decision Time (UTC):

Notes:
```
