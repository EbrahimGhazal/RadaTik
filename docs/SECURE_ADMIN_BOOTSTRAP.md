# Secure System Administrator Bootstrap

`Program.cs` no longer creates a default system administrator automatically.

Create the first admin explicitly with a secure command:

```powershell
dotnet run --project RadTik/RadTik.csproj -- --bootstrap-admin --username admin --email admin@radtik.com --full-name "System Admin" --password "<STRONG_PASSWORD>"
```

You can also pass the password via environment variable:

```powershell
$env:RADTIK_BOOTSTRAP_ADMIN_PASSWORD="<STRONG_PASSWORD>"
dotnet run --project RadTik/RadTik.csproj -- --bootstrap-admin --username admin --email admin@radtik.com
```

Notes:
- No default/fallback password is used.
- The command is idempotent: it creates the user if missing and ensures the `SystemAdministrator` role is assigned.

## One-time re-encryption for legacy plaintext records

After deploying migration `SecureSensitiveCredentialsAtRest`, run:

```powershell
dotnet run --project RadTik/RadTik.csproj -- --reencrypt-sensitive-fields
```

This rewrites `Clients.Password` and `MikroTikServers.Pass` through the encryption converter.
