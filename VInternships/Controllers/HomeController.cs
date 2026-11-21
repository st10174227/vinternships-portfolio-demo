using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VInternships.Demo;
using VInternships.Models;

namespace VInternships.Controllers;
public sealed class HomeController(PlacementStore store) : Controller
{
    [HttpGet] public IActionResult Index() => View(new Dashboard(store.Data.Students.Count,store.Data.Companies.Count,store.Data.Internships.Count,store.Data.Internships.Sum(x=>x.HoursLogged),store.Rows().Take(4).ToList()));
    [HttpGet] public IActionResult Privacy() => View();
    public IActionResult Error() => View(new ErrorViewModel {RequestId=Activity.Current?.Id ?? HttpContext.TraceIdentifier});
}
