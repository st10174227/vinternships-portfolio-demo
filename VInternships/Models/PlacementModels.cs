using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VInternships.Models;
public sealed class Student
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string DegreeProgram { get; set; } = "";
    public int YearOfStudy { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Location { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public int Version { get; set; } = 1;
}
public sealed class Internship
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StudentId { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string Position { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int HoursLogged { get; set; }
    public string SupervisorFeedback { get; set; } = "";
    public int Version { get; set; } = 1;
}
public sealed record PlacementRow(Internship Placement, Student Student, Company Company)
{
    public string Status => DateTime.UtcNow.Date < Placement.StartDate.Date ? "Upcoming" : DateTime.UtcNow.Date > Placement.EndDate.Date ? "Finished" : "Active";
}
public sealed record Dashboard(int Students, int Companies, int Placements, int Hours, IReadOnlyList<PlacementRow> Recent);
public sealed class StudentForm
{
    [ValidateNever] public string Id { get; set; } = "";
    public int Version { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [Required, StringLength(100), Display(Name="Degree programme")] public string DegreeProgram { get; set; } = "";
    [BindRequired, Range(1,6), Display(Name="Year of study")] public int YearOfStudy { get; set; } = 1;
}
public sealed class CompanyForm
{
    [ValidateNever] public string Id { get; set; } = "";
    public int Version { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(80)] public string Industry { get; set; } = "";
    [Required, StringLength(100)] public string Location { get; set; } = "";
    [Required, EmailAddress, StringLength(160), Display(Name="Contact email")] public string ContactEmail { get; set; } = "";
}
public sealed class PlacementForm
{
    [ValidateNever] public string Id { get; set; } = "";
    public int Version { get; set; }
    [Required, StringLength(32), Display(Name="Student")] public string StudentId { get; set; } = "";
    [Required, StringLength(32), Display(Name="Company")] public string CompanyId { get; set; } = "";
    [Required, StringLength(100)] public string Position { get; set; } = "";
    [BindRequired, DataType(DataType.Date), Display(Name="Start date")] public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    [BindRequired, DataType(DataType.Date), Display(Name="End date")] public DateTime EndDate { get; set; } = DateTime.UtcNow.Date.AddDays(60);
    [BindNever, ValidateNever] public IReadOnlyList<Student> Students { get; set; } = [];
    [BindNever, ValidateNever] public IReadOnlyList<Company> Companies { get; set; } = [];
}
public sealed class HoursForm
{
    [ValidateNever] public string Id { get; set; } = "";
    public int Version { get; set; }
    [BindRequired, Range(1,24), Display(Name="Hours to add")] public int HoursToAdd { get; set; } = 1;
    [BindNever, ValidateNever] public PlacementRow Placement { get; set; } = null!;
}
public sealed class FeedbackForm
{
    [ValidateNever] public string Id { get; set; } = "";
    public int Version { get; set; }
    [StringLength(1000), Display(Name="Supervisor feedback")] public string? Feedback { get; set; }
    [BindNever, ValidateNever] public PlacementRow Placement { get; set; } = null!;
}
public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
