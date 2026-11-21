using Microsoft.AspNetCore.Mvc;
using VInternships.Demo;

namespace VInternships.Controllers;
public sealed class DemoController(PlacementStore store) : Controller
{
    [HttpGet] public IActionResult Index() => View();
    [HttpPost] public IActionResult Reset()
    {
        store.Data.Reset();TempData.Clear();TempData["Success"]="Your demo has been reset to fresh fictional samples.";return RedirectToAction("Index","Home");
    }
}
