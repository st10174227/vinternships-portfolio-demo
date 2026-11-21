using Microsoft.AspNetCore.Mvc;
using VInternships.Demo;
using VInternships.Models;

namespace VInternships.Controllers;
public sealed class StudentController(PlacementStore store) : Controller
{
    [HttpGet] public IActionResult Index() => View(store.Data.Students);
    [HttpGet] public IActionResult Create() => View("Form",new StudentForm());
    [HttpPost] public IActionResult Create(StudentForm form)
    {
        form.Id="";
        if(ModelState.IsValid)try { store.SaveStudent(form,null);TempData["Success"]="Student added to your sample workspace.";return RedirectToAction(nameof(Index)); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",form);
    }
    [HttpGet] public IActionResult Details(string id)
    {
        var record=store.Student(id);if(record is null)return NotFound();
        ViewData["PlacementCount"]=store.Data.Internships.Count(x=>x.StudentId==record.Id);return View(record);
    }
    [HttpGet] public IActionResult Edit(string id)
    {
        var record=store.Student(id);return record is null ? NotFound() : View("Form",new StudentForm { Id=record.Id,Version=record.Version,Name=record.Name,Email=record.Email,DegreeProgram=record.DegreeProgram,YearOfStudy=record.YearOfStudy });
    }
    [HttpPost] public IActionResult Edit(StudentForm form)
    {
        var record=store.Student(form.Id);if(record is null)return NotFound();
        if(ModelState.IsValid)try { store.SaveStudent(form,record);TempData["Success"]="Student updated.";return RedirectToAction(nameof(Index)); }
            catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message); }
        return View("Form",form);
    }
    [HttpGet] public IActionResult Delete(string id) { var record=store.Student(id);return record is null ? NotFound() : View(record); }
    [HttpPost,ActionName("Delete")] public IActionResult DeleteConfirmed(string id,int version)
    {
        var record=store.Student(id);if(record is null)return NotFound();
        try { store.DeleteStudent(record,version);TempData["Success"]="Student removed.";return RedirectToAction(nameof(Index)); }
        catch(WorkspaceValidationException ex) { ModelState.AddModelError("",ex.Message);return View("Delete",record); }
    }
}
