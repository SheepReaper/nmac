# Task Completion Checklist

When finishing an implementation task in NMAC, do the following:

## 1. Build Check
```powershell
dotnet build nmac.slnx
```
Must produce **0 errors, 0 warnings**.

## 2. Check Errors in VS Code
Use the VS Code diagnostics tool to also inspect any opened files.

## 3. DI Registration
If new services were added:
- Register in `ServerCompositionExtensions.cs` → `AddNmacDomainServices()`
- Choose correct lifetime (Scoped for DB-touching or per-request, Singleton for thread-safe shared state, Transient rarely used)

## 4. EF Migration (if schema changed)
```powershell
dotnet ef migrations add <DescriptiveName> --project src/server/NMAC.csproj
```
Then verify the generated migration file before applying.

## 5. Blazor Lifecycle Safety
If new JS interop calls are added:
- Only call from `OnAfterRenderAsync` gated by `RendererInfo.IsInteractive`
- Catch `JSDisconnectedException` in `DisposeAsync`
- Never call JS interop from `OnInitializedAsync` (runs during prerender)

## 6. Assembly Scanning
New `IEndpoint` or `IUseCase` implementations are auto-registered via `DIExtensions.AddAssemblyEndpoints/UseCases` — no manual registration needed.

## 7. Background Workers
New background services must be added to `AddNmacDomainServices()`:
```csharp
builder.Services.AddHostedService<MyNewWorker>();
```
