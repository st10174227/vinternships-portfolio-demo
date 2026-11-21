using Microsoft.AspNetCore.Mvc;
using VInternships.Demo;
using VInternships.Models;

namespace VInternships.Controllers;
public sealed class CompanyController(PlacementStore store) : Controller
{
    [HttpGet] public IActionResult Index() => View(store.Data.Companies);
    [HttpGet] public IActionResult Create() => View("Form",new CompanyForm());
    [HttpPost] public IActionResult Create(CompanyForm form)
    {
        form.Id="";
        if(ModelState.IsValid)try { store.SaveCompany(form,null);TempData["Success"]="Company added to your sample workspace.";return RedirectToAction(nameof(Index)); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",form);
    }
    [HttpGet] public IActionResult Details(string id)
    {
        var record=store.Company(id);if(record is null)return NotFound();
        ViewData["PlacementCount"]=store.Data.Internships.Count(x=>x.CompanyId==record.Id);return View(record);
    }
    [HttpGet] public IActionResult Edit(string id)
    {
        var record=store.Company(id);return record is null ? NotFound() : View("Form",new CompanyForm { Id=record.Id,Version=record.Version,Name=record.Name,Industry=record.Industry,Location=record.Location,ContactEmail=record.ContactEmail });
    }
    [HttpPost] public IActionResult Edit(CompanyForm form)
    {
        var record=store.Company(form.Id);if(record is null)return NotFound();
        if(ModelState.IsValid)try { store.SaveCompany(form,record);TempData["Success"]="Company updated.";return RedirectToAction(nameof(Index)); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",form);
    }
    [HttpGet] public IActionResult Delete(string id) { var record=store.Company(id);return record is null ? NotFound() : View(record); }
    [HttpPost,ActionName("Delete")] public IActionResult DeleteConfirmed(string id,int version)
    {
        var record=store.Company(id);if(record is null)return NotFound();
        try { store.DeleteCompany(record,version);TempData["Success"]="Company removed.";return RedirectToAction(nameof(Index)); }
        catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message);return View("Delete",record); }
    }
}
