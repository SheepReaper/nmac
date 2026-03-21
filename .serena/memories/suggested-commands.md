# Suggested Commands

## Running the Application
```powershell
# Run via Aspire AppHost (starts PostgreSQL container + API + dashboard)
dotnet run --project src/apphost.cs

# Run the server directly (requires PostgreSQL already running)
dotnet run --project src/server/NMAC.csproj
```

## Building
```powershell
# Build full solution
dotnet build nmac.slnx

# Build individual projects
dotnet build src/server/NMAC.csproj
dotnet build src/web/NMAC.Ui.csproj
```

## EF Core Migrations
```powershell
# Add a new migration (run from repo root)
dotnet ef migrations add <MigrationName> --project src/server/NMAC.csproj

# Apply migrations
dotnet ef database update --project src/server/NMAC.csproj
```

## Testing (no automated tests currently)
- Manual HTTP tests in `tests.http`

## Linting / Formatting
- No dedicated linter/formatter configured (C# with standard dotnet conventions)
- `dotnet build` surfaces all compiler warnings/errors

## Utility
```powershell
# Check for errors across solution
dotnet build nmac.slnx --no-incremental

# View Aspire dashboard (auto-opens when running AppHost)
# Available at http://localhost:18888 (default Aspire port)

# Git
git status
git log --oneline -10
git diff
```
