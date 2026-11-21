using VInternships.Models;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace VInternships.Demo;
public sealed class DemoContext { public Workspace Current { get; set; } = null!; }
public sealed class Workspace
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ExpiresAt { get; init; }
    public int Leases { get; set; }
    public SemaphoreSlim Gate { get; } = new(1,1);
    public List<Student> Students { get; } = [];
    public List<Company> Companies { get; } = [];
    public List<Internship> Internships { get; } = [];
    public void Reset()
    {
        Students.Clear(); Companies.Clear(); Internships.Clear();
        Students.AddRange([
            new() { Name="Amina Example", Email="amina@example.invalid", DegreeProgram="Information Technology", YearOfStudy=3 },
            new() { Name="Luca Example", Email="luca@example.invalid", DegreeProgram="Software Development", YearOfStudy=2 },
            new() { Name="Sam Example", Email="sam@example.invalid", DegreeProgram="Business Information Systems", YearOfStudy=3 }
        ]);
        Companies.AddRange([
            new() { Name="Demo Northstar Digital", Industry="Software", Location="Cape Town", ContactEmail="northstar@example.invalid" },
            new() { Name="Demo Harbour Studio", Industry="Digital services", Location="Remote", ContactEmail="harbour@example.invalid" }
        ]);
        var today=DateTime.UtcNow.Date;
        Internships.AddRange([
            new() { StudentId=Students[0].Id, CompanyId=Companies[0].Id, Position="Software development intern", StartDate=today.AddDays(-14), EndDate=today.AddDays(42), HoursLogged=40 },
            new() { StudentId=Students[1].Id, CompanyId=Companies[1].Id, Position="Web development intern", StartDate=today.AddDays(7), EndDate=today.AddDays(70) },
            new() { StudentId=Students[2].Id, CompanyId=Companies[0].Id, Position="Systems support intern", StartDate=today.AddDays(-90), EndDate=today.AddDays(-30), HoursLogged=80, SupervisorFeedback="Fictional example: clear communication and reliable preparation." }
        ]);
    }
}

public sealed class WorkspaceRegistry(TimeProvider clock)
{
    private readonly object _sync = new();
    private readonly Dictionary<string,Workspace> _items = new(StringComparer.Ordinal);
    public int Count { get { lock (_sync) return _items.Count; } }
    public IDisposable? Enter(string? id, out Workspace? workspace)
    {
        lock (_sync)
        {
            foreach (var stale in _items.Values.Where(w => w.ExpiresAt <= clock.GetUtcNow() && w.Leases == 0).ToArray())
            { _items.Remove(stale.Id); stale.Students.Clear(); stale.Companies.Clear(); stale.Internships.Clear(); }
            if (id is null || !_items.TryGetValue(id, out workspace) || workspace.ExpiresAt <= clock.GetUtcNow())
            {
                if (_items.Count >= 20) { workspace = null; return null; }
                workspace = new() { ExpiresAt = clock.GetUtcNow().AddMinutes(45) }; workspace.Reset(); _items.Add(workspace.Id,workspace);
            }
            workspace.Leases++;
            return new Lease(this,workspace);
        }
    }
    private void Release(Workspace workspace) { lock (_sync) workspace.Leases--; }
    private sealed class Lease(WorkspaceRegistry owner, Workspace workspace) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; owner.Release(workspace); } }
    }
}
public sealed class WorkspaceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, WorkspaceRegistry registry, DemoContext context)
    {
        // Static assets, health probes and unmatched URLs do not consume visitor capacity.
        if (http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is null) { await next(http); return; }
        await http.Session.LoadAsync(http.RequestAborted);
        using var lease = registry.Enter(http.Session.GetString("Workspace"),out var workspace);
        if (lease is null || workspace is null) { http.Response.StatusCode = 503; await http.Response.WriteAsync("The demo is busy. Please try again later."); return; }
        context.Current = workspace;
        http.Session.SetString("Workspace",workspace.Id);
        http.Response.Headers.CacheControl = "no-store";
        await workspace.Gate.WaitAsync(http.RequestAborted);
        try { await next(http); }
        finally { workspace.Gate.Release(); }
    }
}
