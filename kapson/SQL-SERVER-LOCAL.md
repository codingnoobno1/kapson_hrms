# Local SQL Server Express

The development Host uses the named Windows SQL Server Express instance:

```text
localhost\SQLEXPRESS
```

The connection is configured in `Vispl.HRManagement.Host/appsettings.Development.json` with Windows Authentication. No username or password is stored in source control.

When `Vispl.HRManagement.Host` starts in Development, it:

1. Connects to `localhost\SQLEXPRESS`.
2. Creates the `KapsonHRManagement` database when it does not exist.
3. Applies pending EF Core migrations.
4. Imports `SeedData/*.json` only when the corresponding tables are empty.
5. Starts the single application process on `http://localhost:7000`.

Run from the solution directory:

```powershell
dotnet run --project Vispl.HRManagement.Host
```

For deployment, do not copy the development connection string. Supply the production connection through:

```text
ConnectionStrings__HrManagement
```

The application tables are separated by SQL schema:

- `platform.CareerSites`
- `recruitment.JobOpenings`
- `dbo.__EFMigrationsHistory`
