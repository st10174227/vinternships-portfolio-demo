using Microsoft.AspNetCore.Mvc;
using VInternships.Demo;
using VInternships.Models;

namespace VInternships.Controllers;
public sealed class InternshipController(PlacementStore store) : Controller
{
    private PlacementForm Choices(PlacementForm form) { form.Students=store.Data.Students;form.Companies=store.Data.Companies;return form; }
    [HttpGet] public IActionResult Index(string? studentId=null,string? companyId=null)
    {
        if(studentId is not null && store.Student(studentId) is null)return NotFound();
        if(companyId is not null && store.Company(companyId) is null)return NotFound();
        var rows=store.Rows().Where(x=>(studentId is null || x.Student.Id==studentId) && (companyId is null || x.Company.Id==companyId)).ToList();
        return View(rows);
    }
    [HttpGet] public IActionResult Create() => View("Form",Choices(new()));
    [HttpPost] public IActionResult Create(PlacementForm form)
    {
        form.Id="";
        if(ModelState.IsValid)try { store.SavePlacement(form,null);TempData["Success"]="Sample placement created.";return RedirectToAction(nameof(Index)); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",Choices(form));
    }
    [HttpGet] public IActionResult Details(string id) { var record=store.Internship(id);return record is null ? NotFound() : View(store.Row(record)); }
    [HttpGet] public IActionResult Edit(string id)
    {
        var record=store.Internship(id);return record is null ? NotFound() : View("Form",Choices(new PlacementForm {
            Id=record.Id,Version=record.Version,StudentId=record.StudentId,CompanyId=record.CompanyId,Position=record.Position,StartDate=record.StartDate,EndDate=record.EndDate }));
    }
    [HttpPost] public IActionResult Edit(PlacementForm form)
    {
        var record=store.Internship(form.Id);if(record is null)return NotFound();
        if(ModelState.IsValid)try { store.SavePlacement(form,record);TempData["Success"]="Placement details updated.";return RedirectToAction(nameof(Details),new {id=record.Id}); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",Choices(form));
    }
    [HttpGet] public IActionResult LogHours(string id)
    {
        var record=store.Internship(id);return record is null ? NotFound() : View(new HoursForm { Id=record.Id,Version=record.Version,Placement=store.Row(record) });
    }
    [HttpPost] public IActionResult LogHours(HoursForm form)
    {
        var record=store.Internship(form.Id);if(record is null)return NotFound();form.Placement=store.Row(record);
        if(ModelState.IsValid)try { store.AddHours(record,form);TempData["Success"]="Hours added to this sample placement.";return RedirectToAction(nameof(Details),new {id=record.Id}); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View(form);
    }
    [HttpGet] public IActionResult Feedback(string id)
    {
        var record=store.Internship(id);return record is null ? NotFound() : View(new FeedbackForm { Id=record.Id,Version=record.Version,Feedback=record.SupervisorFeedback,Placement=store.Row(record) });
    }
    [HttpPost] public IActionResult Feedback(FeedbackForm form)
    {
        var record=store.Internship(form.Id);if(record is null)return NotFound();form.Placement=store.Row(record);
        if(ModelState.IsValid)try { store.SaveFeedback(record,form);TempData["Success"]="Sample feedback saved.";return RedirectToAction(nameof(Details),new {id=record.Id}); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View(form);
    }
    [HttpGet] public IActionResult Delete(string id) { var record=store.Internship(id);return record is null ? NotFound() : View(store.Row(record)); }
    [HttpPost,ActionName("Delete")] public IActionResult DeleteConfirmed(string id,int version)
    {
        var record=store.Internship(id);if(record is null)return NotFound();
        try { store.DeletePlacement(record,version);TempData["Success"]="Sample placement removed.";return RedirectToAction(nameof(Index)); }
        catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message);return View("Delete",store.Row(record)); }
    }
}
