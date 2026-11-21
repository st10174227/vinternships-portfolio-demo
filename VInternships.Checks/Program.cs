using VInternships.Demo;
using VInternships.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

var fixture=Path.Combine(Path.GetTempPath(),"vinternships-checks-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture,"wwwroot"));
var passed=0;
static void Need(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
async Task Check(string name,Func<Task> run) { await run();Console.WriteLine("PASS "+name);passed++; }
static string Attr(string tag,string name)
{
    var match=Regex.Match(tag,"\\b"+Regex.Escape(name)+"=\"([^\"]*)\"",RegexOptions.IgnoreCase);
    return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : "";
}
static string Hidden(string html,string name) => Regex.Matches(html,"<input\\b[^>]*>",RegexOptions.IgnoreCase).Select(m=>m.Value)
    .Where(tag=>Attr(tag,"name").Equals(name,StringComparison.OrdinalIgnoreCase)).Select(tag=>Attr(tag,"value")).FirstOrDefault() ?? throw new InvalidOperationException("Missing form field: "+name);
static HashSet<string> Ids(string html,string controller) => Regex.Matches(html,"/"+Regex.Escape(controller)+"/Edit/([a-f0-9]{32})",RegexOptions.IgnoreCase).Select(m=>m.Groups[1].Value).ToHashSet();
static FormUrlEncodedContent Form(Dictionary<string,string> fields)=>new(fields);
var tokens=new Dictionary<HttpClient,string>();
async Task<string> Token(HttpClient client)
{
    if(!tokens.TryGetValue(client,out var token)) { token=Hidden(await client.GetStringAsync("/Demo"),"__RequestVerificationToken");tokens[client]=token; }
    return token;
}
async Task<HttpResponseMessage> Post(HttpClient client,string route,Dictionary<string,string> fields)
{ fields["__RequestVerificationToken"]=await Token(client);return await client.PostAsync(route,Form(fields)); }
async Task<string> Good(HttpClient client,string route,Dictionary<string,string> fields)
{
    using var response=await Post(client,route,fields);var body=await response.Content.ReadAsStringAsync();
    Need(response.StatusCode==HttpStatusCode.Redirect,$"Expected redirect from {route}; HTTP {(int)response.StatusCode}; {body[..Math.Min(body.Length,800)]}");
    return response.Headers.Location?.ToString() ?? throw new InvalidOperationException("Redirect address missing.");
}
async Task<string> Invalid(HttpClient client,string route,Dictionary<string,string> fields)
{
    using var response=await Post(client,route,fields);var body=await response.Content.ReadAsStringAsync();
    Need(response.StatusCode==HttpStatusCode.OK && body.Contains("validation-summary-errors"),$"Expected readable form errors at {route}; HTTP {(int)response.StatusCode}");return body;
}
async Task<Dictionary<string,string>> Version(HttpClient client,string controller,string id,string action="Edit")
{
    var html=await client.GetStringAsync($"/{controller}/{action}/{id}");return new(){["Id"]=id,["Version"]=Hidden(html,"Version")};
}
Dictionary<string,string> StudentFields(string name="Visitor Student",string email="visitor@example.invalid",string year="3")=>new(){["Name"]=name,["Email"]=email,["DegreeProgram"]="Software Development",["YearOfStudy"]=year};
Dictionary<string,string> CompanyFields(string name="Visitor Demo Company",string email="company@example.invalid")=>new(){["Name"]=name,["Industry"]="Software",["Location"]="Remote",["ContactEmail"]=email};
var studentId="";var companyId="";var placementId="";
var today=DateTime.UtcNow.Date;
Dictionary<string,string> PlacementFields(string position="Visitor developer placement")=>new(){["StudentId"]=studentId,["CompanyId"]=companyId,["Position"]=position,["StartDate"]=today.ToString("yyyy-MM-dd"),["EndDate"]=today.AddDays(60).ToString("yyyy-MM-dd")};
static void Merge(Dictionary<string,string> target,Dictionary<string,string> source) { foreach(var pair in source)target[pair.Key]=pair.Value; }
try
{
    await using var app=VInternships.Program.BuildApplication(["--environment=Testing","--contentRoot="+fixture],builder=>
    {
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["Demo:DataDirectory"]=Path.Combine(fixture,"Private")});
        builder.Logging.ClearProviders();builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
    });
    await app.StartAsync();
    HttpClient Client()=>new(new CookieJar(app.GetTestServer().CreateHandler())){BaseAddress=new Uri("https://localhost")};
    using var one=Client();using var two=Client();
    var registry=app.Services.GetRequiredService<WorkspaceRegistry>();
    await Check("Health and unmatched URLs do not allocate visitor workspaces",async()=>
    {
        using var health=await one.GetAsync("/health");using var missing=await one.GetAsync("/not-a-route");
        Need(health.StatusCode==HttpStatusCode.OK && (await health.Content.ReadAsStringAsync()).Contains("vinternships-demo") && missing.StatusCode==HttpStatusCode.NotFound && registry.Count==0,"Probe or missing URL allocated records.");
        Need(!health.Headers.Contains("Set-Cookie"),"Health allocated a session cookie.");
    });
    await Check("Production HTTP redirects before rendering secure forms",async()=>
    {
        using var response=await one.GetAsync("http://localhost/Internship/Create");
        Need(response.StatusCode==HttpStatusCode.TemporaryRedirect && response.Headers.Location?.Scheme=="https" && !response.Headers.Contains("Set-Cookie") && registry.Count==0,"An HTTP form was rendered before HTTPS redirection.");
    });
    await Check("Fresh samples and every primary page load without SQL or account settings",async()=>
    {
        foreach(var route in new[]{"/","/Student","/Student/Create","/Company","/Company/Create","/Internship","/Internship/Create","/Home/Privacy","/Demo"})
        { using var response=await one.GetAsync(route);Need(response.StatusCode==HttpStatusCode.OK,$"Page failed: {route}; HTTP {(int)response.StatusCode}"); }
        Need((await one.GetStringAsync("/Student")).Contains("Amina Example"),"Sample students missing.");
        await two.GetStringAsync("/");Need(registry.Count==2,"Browser sessions did not receive separate workspaces.");
    });
    await Check("Seeded detail, edit, delete, hours and feedback pages all render",async()=>
    {
        foreach(var controller in new[]{"Student","Company","Internship"})
        {
            var id=Ids(await one.GetStringAsync("/"+controller),controller).First();
            foreach(var action in controller=="Internship"?new[]{"Details","Edit","Delete","LogHours","Feedback"}:new[]{"Details","Edit","Delete"})
            { using var response=await one.GetAsync($"/{controller}/{action}/{id}");Need(response.StatusCode==HttpStatusCode.OK,$"Seed page failed: {controller}/{action}; HTTP {(int)response.StatusCode}"); }
        }
    });
    await Check("Every state-changing workflow requires an antiforgery token",async()=>
    {
        foreach(var controller in new[]{"Student","Company","Internship"})foreach(var action in new[]{"Create","Edit","Delete"})
        { using var response=await one.PostAsync($"/{controller}/{action}",Form(new()));Need(response.StatusCode==HttpStatusCode.BadRequest,"Tokenless mutation accepted: "+controller+action); }
        foreach(var route in new[]{"/Internship/LogHours","/Internship/Feedback","/Demo/Reset"})
        { using var response=await one.PostAsync(route,Form(new()));Need(response.StatusCode==HttpStatusCode.BadRequest,"Tokenless mutation accepted: "+route); }
    });
    await Check("New students persist only in their browser workspace",async()=>
    {
        var before=Ids(await one.GetStringAsync("/Student"),"Student");await Good(one,"/Student/Create",StudentFields());
        var html=await one.GetStringAsync("/Student");studentId=Ids(html,"Student").Except(before).Single();
        Need(html.Contains("Visitor Student") && !(await two.GetStringAsync("/Student")).Contains("Visitor Student"),"Student data crossed browser sessions.");
    });
    await Check("Invalid years, missing values and duplicate student emails cannot create records",async()=>
    {
        var count=Ids(await one.GetStringAsync("/Student"),"Student").Count;
        foreach(var year in new[]{"0","7","bad"})await Invalid(one,"/Student/Create",StudentFields(year:year));
        var fields=StudentFields();fields.Remove("YearOfStudy");await Invalid(one,"/Student/Create",fields);
        await Invalid(one,"/Student/Create",StudentFields(" ","bad"));await Invalid(one,"/Student/Create",StudentFields("Duplicate","VISITOR@EXAMPLE.INVALID"));
        Need(Ids(await one.GetStringAsync("/Student"),"Student").Count==count,"An invalid student was saved.");
    });
    await Check("Student edits reject stale versions and display user content as text",async()=>
    {
        var fields=await Version(one,"Student",studentId);Merge(fields,StudentFields("<script>alert(1)</script>"));
        await Good(one,"/Student/Edit",fields);fields["Name"]="Stale student";await Invalid(one,"/Student/Edit",fields);
        var html=await one.GetStringAsync("/Student/Details/"+studentId);
        Need(html.Contains("&lt;script&gt;") && !html.Contains("<script>alert(1)</script>") && !html.Contains("Stale student"),"Student encoding or concurrent editing failed.");
        fields=await Version(one,"Student",studentId);Merge(fields,StudentFields());await Good(one,"/Student/Edit",fields);
    });
    await Check("Company creation, details and guarded editing work",async()=>
    {
        var before=Ids(await one.GetStringAsync("/Company"),"Company");await Good(one,"/Company/Create",CompanyFields());
        companyId=Ids(await one.GetStringAsync("/Company"),"Company").Except(before).Single();
        var fields=await Version(one,"Company",companyId);Merge(fields,CompanyFields("Updated Demo Company"));await Good(one,"/Company/Edit",fields);
        fields["Name"]="Stale company";await Invalid(one,"/Company/Edit",fields);
        var html=await one.GetStringAsync("/Company/Details/"+companyId);Need(html.Contains("Updated Demo Company") && !html.Contains("Stale company"),"Stale company edit overwrote current data.");
    });
    await Check("Invalid company forms and duplicate names at the same location are rejected",async()=>
    {
        var count=Ids(await one.GetStringAsync("/Company"),"Company").Count;
        await Invalid(one,"/Company/Create",CompanyFields("","bad"));await Invalid(one,"/Company/Create",CompanyFields("UPDATED DEMO COMPANY"));
        Need(Ids(await one.GetStringAsync("/Company"),"Company").Count==count,"Invalid company was saved.");
    });
    await Check("Placement creation uses saved identities and ignores forged hours and feedback",async()=>
    {
        var before=Ids(await one.GetStringAsync("/Internship"),"Internship");var fields=PlacementFields();fields["HoursLogged"]="900";fields["SupervisorFeedback"]="forged-feedback";
        await Good(one,"/Internship/Create",fields);placementId=Ids(await one.GetStringAsync("/Internship"),"Internship").Except(before).Single();
        var html=await one.GetStringAsync("/Internship/Details/"+placementId);
        Need(html.Contains("Visitor Student") && html.Contains("Updated Demo Company") && html.Contains("data-hours=\"0\"") && !html.Contains("forged-feedback"),"Placement accepted fields outside its form.");
    });
    await Check("Invalid placements preserve both dropdown lists and create no records",async()=>
    {
        var count=Ids(await one.GetStringAsync("/Internship"),"Internship").Count;
        var cases=new List<Dictionary<string,string>>();var fields=PlacementFields();fields["EndDate"]=today.AddDays(-1).ToString("yyyy-MM-dd");cases.Add(fields);
        fields=PlacementFields();fields.Remove("StartDate");cases.Add(fields);
        fields=PlacementFields();fields["StartDate"]="bad";cases.Add(fields);
        fields=PlacementFields();fields["EndDate"]=today.AddDays(731).ToString("yyyy-MM-dd");cases.Add(fields);
        fields=PlacementFields();fields["StudentId"]=Guid.NewGuid().ToString("N");cases.Add(fields);
        fields=PlacementFields();fields["CompanyId"]=Guid.NewGuid().ToString("N");cases.Add(fields);
        cases.Add(PlacementFields());
        foreach(var invalid in cases)
        {
            var html=await Invalid(one,"/Internship/Create",invalid);
            Need(html.Contains("value=\""+studentId+"\"") && html.Contains("value=\""+companyId+"\""),"Invalid submission lost a dropdown list.");
        }
        Need(Ids(await one.GetStringAsync("/Internship"),"Internship").Count==count,"Invalid placement persisted.");
    });
    await Check("Hour entries add to the total and replayed forms cannot add twice",async()=>
    {
        var fields=await Version(one,"Internship",placementId,"LogHours");fields["HoursToAdd"]="6";await Good(one,"/Internship/LogHours",fields);
        await Invalid(one,"/Internship/LogHours",fields);
        fields=await Version(one,"Internship",placementId,"LogHours");fields["HoursToAdd"]="4";fields["HoursLogged"]="999";fields["CompanyId"]=Guid.NewGuid().ToString("N");await Good(one,"/Internship/LogHours",fields);
        Need((await one.GetStringAsync("/Internship/Details/"+placementId)).Contains("data-hours=\"10\""),"Hours replaced the total or accepted forged fields.");
    });
    await Check("Missing, fractional, zero and excessive hour entries leave the total unchanged",async()=>
    {
        foreach(var value in new[]{"0","-1","25","1.5","bad","2147483648"})
        { var fields=await Version(one,"Internship",placementId,"LogHours");fields["HoursToAdd"]=value;await Invalid(one,"/Internship/LogHours",fields); }
        var missing=await Version(one,"Internship",placementId,"LogHours");await Invalid(one,"/Internship/LogHours",missing);
        Need((await one.GetStringAsync("/Internship/Details/"+placementId)).Contains("data-hours=\"10\""),"Invalid hours changed the total.");
    });
    await Check("Feedback updates only feedback, detects stale edits and supports clearing",async()=>
    {
        var fields=await Version(one,"Internship",placementId,"Feedback");fields["Feedback"]="Fictional supervisor note";fields["HoursLogged"]="999";await Good(one,"/Internship/Feedback",fields);
        fields["Feedback"]="Stale feedback";await Invalid(one,"/Internship/Feedback",fields);
        var html=await one.GetStringAsync("/Internship/Details/"+placementId);Need(html.Contains("Fictional supervisor note") && !html.Contains("Stale feedback") && html.Contains("data-hours=\"10\""),"Feedback changed unrelated fields or lost the newest update.");
        fields=await Version(one,"Internship",placementId,"Feedback");fields["Feedback"]=new string('x',1001);await Invalid(one,"/Internship/Feedback",fields);
        fields=await Version(one,"Internship",placementId,"Feedback");fields["Feedback"]="";await Good(one,"/Internship/Feedback",fields);
        Need((await one.GetStringAsync("/Internship/Details/"+placementId)).Contains("No feedback recorded yet."),"Blank feedback did not clear it.");
    });
    await Check("Placement edits preserve hours and feedback and reject stale forms",async()=>
    {
        var feedback=await Version(one,"Internship",placementId,"Feedback");feedback["Feedback"]="Retained sample feedback";await Good(one,"/Internship/Feedback",feedback);
        var fields=await Version(one,"Internship",placementId);Merge(fields,PlacementFields("Updated visitor placement"));fields["HoursLogged"]="0";fields["SupervisorFeedback"]="forged";await Good(one,"/Internship/Edit",fields);
        fields["Position"]="Stale position";await Invalid(one,"/Internship/Edit",fields);
        var html=await one.GetStringAsync("/Internship/Details/"+placementId);Need(html.Contains("Updated visitor placement") && html.Contains("Retained sample feedback") && html.Contains("data-hours=\"10\"") && !html.Contains("Stale position"),"Placement edit lost hours, feedback or version protection.");
    });
    await Check("GET confirmation pages are read-only and linked parents cannot be deleted",async()=>
    {
        foreach(var pair in new[]{("Student",studentId),("Company",companyId)})
        {
            var fields=await Version(one,pair.Item1,pair.Item2,"Delete");
            Need((await one.GetStringAsync("/"+pair.Item1)).Contains(pair.Item2),"GET deleted a record.");await Invalid(one,"/"+pair.Item1+"/Delete",fields);
        }
    });
    await Check("Placements show current parent names and filtered lists keep matching records",async()=>
    {
        var fields=await Version(one,"Student",studentId);Merge(fields,StudentFields("Renamed Student"));await Good(one,"/Student/Edit",fields);
        var html=await one.GetStringAsync("/Internship/Details/"+placementId);Need(html.Contains("Renamed Student") && !html.Contains("Visitor Student"),"Placement kept an obsolete copied student name.");
        var ids=Ids(await one.GetStringAsync("/Internship?studentId="+studentId+"&companyId="+companyId),"Internship");Need(ids.SetEquals([placementId]),"Placement filtering returned unrelated records.");
    });
    await Check("Another browser cannot read, edit, delete, add hours or replace feedback",async()=>
    {
        foreach(var pair in new[]{("Student",studentId),("Company",companyId),("Internship",placementId)})
        {
            foreach(var action in pair.Item1=="Internship"?new[]{"Details","Edit","Delete","LogHours","Feedback"}:new[]{"Details","Edit","Delete"})
            { using var response=await two.GetAsync($"/{pair.Item1}/{action}/{pair.Item2}");Need(response.StatusCode==HttpStatusCode.NotFound,"A private record was exposed."); }
            foreach(var action in pair.Item1=="Internship"?new[]{"Edit","Delete","LogHours","Feedback"}:new[]{"Edit","Delete"})
            { using var response=await Post(two,$"/{pair.Item1}/{action}",new(){["Id"]=pair.Item2,["Version"]="1"});Need(response.StatusCode==HttpStatusCode.NotFound,"Cross-browser mutation succeeded."); }
        }
        await Invalid(two,"/Internship/Create",PlacementFields("Foreign references"));
        using var filtered=await two.GetAsync("/Internship?studentId="+studentId);Need(filtered.StatusCode==HttpStatusCode.NotFound,"Foreign identity filter exposed a record.");
    });
    await Check("Deleting a placement releases parents while stale deletions remain blocked",async()=>
    {
        var fields=await Version(one,"Internship",placementId,"Delete");fields["Version"]="0";await Invalid(one,"/Internship/Delete",fields);
        foreach(var pair in new[]{("Internship",placementId),("Student",studentId),("Company",companyId)})
        {
            fields=await Version(one,pair.Item1,pair.Item2,"Delete");await Good(one,"/"+pair.Item1+"/Delete",fields);
            using var gone=await one.GetAsync($"/{pair.Item1}/Details/{pair.Item2}");Need(gone.StatusCode==HttpStatusCode.NotFound,"A deleted record survived.");
        }
    });
    await Check("Reset replaces only one browser's records and invalidates its old forms",async()=>
    {
        await Good(one,"/Student/Create",StudentFields("Reset Me","reset@example.invalid"));await Good(two,"/Student/Create",StudentFields("Second Browser","second@example.invalid"));
        var old=Ids(await one.GetStringAsync("/Student"),"Student");await Good(one,"/Demo/Reset",new());var html=await one.GetStringAsync("/Student");
        Need(!html.Contains("Reset Me") && html.Contains("Amina Example") && !Ids(html,"Student").Overlaps(old) && (await two.GetStringAsync("/Student")).Contains("Second Browser"),"Reset leaked across sessions or reused record identities.");
        using var oldPage=await one.GetAsync("/Student/Edit/"+old.First());Need(oldPage.StatusCode==HttpStatusCode.NotFound,"A pre-reset identity remained accessible.");
    });
    await Check("Cumulative hour bounds and shortened date ranges cannot misrepresent work",()=>
    {
        var workspace=new Workspace();workspace.Reset();var store=new PlacementStore(new DemoContext{Current=workspace});var placement=workspace.Internships[0];
        placement.HoursLogged=1990;placement.StartDate=today;placement.EndDate=today.AddDays(100);
        try { store.AddHours(placement,new(){Version=placement.Version,HoursToAdd=24});throw new InvalidOperationException("Cumulative hour cap was not enforced."); } catch(WorkspaceValidationException) { }
        Need(placement.HoursLogged==1990,"Rejected addition partially changed the total.");
        try { store.SavePlacement(new(){StudentId=placement.StudentId,CompanyId=placement.CompanyId,Position=placement.Position,StartDate=today,EndDate=today,Version=placement.Version},placement);throw new InvalidOperationException("Shortened dates accepted impossible hours."); } catch(WorkspaceValidationException) { }
        placement.HoursLogged=20;placement.EndDate=today;
        try { store.AddHours(placement,new(){Version=placement.Version,HoursToAdd=5});throw new InvalidOperationException("Date-based hour cap was not enforced."); } catch(WorkspaceValidationException) { }
        Need(placement.HoursLogged==20,"Date-based rejection changed the total.");return Task.CompletedTask;
    });
    await Check("Student, company and placement limits reject overflow without partial records",()=>
    {
        var workspace=new Workspace();workspace.Reset();var store=new PlacementStore(new DemoContext{Current=workspace});
        while(workspace.Students.Count<50)workspace.Students.Add(new(){Name="Capacity sample",Email=Guid.NewGuid().ToString("N")+"@example.invalid"});
        while(workspace.Companies.Count<25)workspace.Companies.Add(new(){Name=Guid.NewGuid().ToString("N"),Location="Remote"});
        while(workspace.Internships.Count<75)workspace.Internships.Add(new(){StudentId=workspace.Students[0].Id,CompanyId=workspace.Companies[0].Id,Position=Guid.NewGuid().ToString("N"),StartDate=today,EndDate=today});
        try { store.SaveStudent(new(){Name="Overflow",Email="overflow@example.invalid",DegreeProgram="IT",YearOfStudy=1},null);throw new InvalidOperationException("Student cap missing."); } catch(WorkspaceValidationException) { }
        try { store.SaveCompany(new(){Name="Overflow",Industry="IT",Location="Remote",ContactEmail="overflow@example.invalid"},null);throw new InvalidOperationException("Company cap missing."); } catch(WorkspaceValidationException) { }
        try { store.SavePlacement(new(){StudentId=workspace.Students[0].Id,CompanyId=workspace.Companies[0].Id,Position="Overflow",StartDate=today,EndDate=today},null);throw new InvalidOperationException("Placement cap missing."); } catch(WorkspaceValidationException) { }
        Need(workspace.Students.Count==50 && workspace.Companies.Count==25 && workspace.Internships.Count==75,"Rejected overflow left partial records.");return Task.CompletedTask;
    });
    await Check("Workspace capacity and absolute expiry remain bounded without reusing data",()=>
    {
        var clock=new TestClock();var bounded=new WorkspaceRegistry(clock);var first="";
        for(var i=0;i<20;i++) { using var lease=bounded.Enter(null,out var workspace);Need(lease is not null && workspace is not null,"Capacity too small.");if(i==0)first=workspace!.Id; }
        using(var full=bounded.Enter(null,out _))Need(full is null,"Workspace limit missing.");
        clock.Now=clock.Now.AddMinutes(44);using(var reused=bounded.Enter(first,out var same))Need(reused is not null && same?.Id==first,"Workspace expired early.");
        clock.Now=clock.Now.AddMinutes(2);using var fresh=bounded.Enter(first,out var replacement);Need(fresh is not null && replacement?.Id!=first && bounded.Count==1,"Activity extended absolute expiry or exposed old data.");return Task.CompletedTask;
    });
    await Check("In-flight leases preserve records until the request finishes",()=>
    {
        var clock=new TestClock();var bounded=new WorkspaceRegistry(clock);var active=bounded.Enter(null,out var held);Need(active is not null && held is not null,"Lease missing.");
        clock.Now=clock.Now.AddMinutes(46);using(var replacement=bounded.Enter(held!.Id,out var fresh))Need(replacement is not null && fresh?.Id!=held.Id && held.Students.Count==3,"Expiry erased a leased request's records.");
        active!.Dispose();using var reaped=bounded.Enter(null,out _);Need(held.Students.Count==0,"Expired records were not retired after release.");return Task.CompletedTask;
    });
    Console.WriteLine($"VInternships checks passed: {passed}. Browser and Docker acceptance remain separate.");
    await app.StopAsync();
}
catch(Exception ex) { Console.Error.WriteLine($"FAIL after {passed} checks: {ex}");Environment.ExitCode=1; }
finally { try { Directory.Delete(fixture,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
sealed class TestClock:TimeProvider
{
    public DateTimeOffset Now{get;set;}=DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow()=>Now;
}
sealed class CookieJar(HttpMessageHandler inner):DelegatingHandler(inner)
{
    private readonly CookieContainer _cookies=new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        var uri=request.RequestUri ?? throw new InvalidOperationException("Request URI missing.");var header=_cookies.GetCookieHeader(uri);
        if(header.Length>0)request.Headers.TryAddWithoutValidation("Cookie",header);
        var response=await base.SendAsync(request,cancellationToken);
        if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values)_cookies.SetCookies(uri,value);
        return response;
    }
}
