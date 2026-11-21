using VInternships.Models;

namespace VInternships.Demo;
public sealed class WorkspaceValidationException(string message) : Exception(message);
public sealed class PlacementStore(DemoContext context)
{
    public Workspace Data => context.Current;
    public Student? Student(string id) => Data.Students.FirstOrDefault(x=>x.Id==id);
    public Company? Company(string id) => Data.Companies.FirstOrDefault(x=>x.Id==id);
    public Internship? Internship(string id) => Data.Internships.FirstOrDefault(x=>x.Id==id);
    public PlacementRow Row(Internship placement) => new(placement,Data.Students.Single(x=>x.Id==placement.StudentId),Data.Companies.Single(x=>x.Id==placement.CompanyId));
    public IReadOnlyList<PlacementRow> Rows() => Data.Internships.Select(Row).ToList();
    private static void Current(int actual,int submitted)
    { if(actual!=submitted)throw new WorkspaceValidationException("This record changed in another tab. Reload the page before trying again."); }
    public void SaveStudent(StudentForm form,Student? existing)
    {
        var email=form.Email.Trim();
        if(Data.Students.Any(x=>x.Id!=existing?.Id && string.Equals(x.Email,email,StringComparison.OrdinalIgnoreCase)))
            throw new WorkspaceValidationException("That email already belongs to a student in your demo.");
        if(existing is null && Data.Students.Count>=50)throw new WorkspaceValidationException("Your demo allows 50 students. Remove an unused student or reset it.");
        if(existing is not null)Current(existing.Version,form.Version);
        var student=existing ?? new Student();student.Name=form.Name.Trim();student.Email=email;student.DegreeProgram=form.DegreeProgram.Trim();student.YearOfStudy=form.YearOfStudy;
        if(existing is null)Data.Students.Add(student);else student.Version++;
    }
    public void SaveCompany(CompanyForm form,Company? existing)
    {
        var name=form.Name.Trim();var location=form.Location.Trim();
        if(Data.Companies.Any(x=>x.Id!=existing?.Id && string.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase) && string.Equals(x.Location,location,StringComparison.OrdinalIgnoreCase)))
            throw new WorkspaceValidationException("A company with that name and location is already in your demo.");
        if(existing is null && Data.Companies.Count>=25)throw new WorkspaceValidationException("Your demo allows 25 companies. Remove an unused company or reset it.");
        if(existing is not null)Current(existing.Version,form.Version);
        var company=existing ?? new Company();company.Name=name;company.Industry=form.Industry.Trim();company.Location=location;company.ContactEmail=form.ContactEmail.Trim();
        if(existing is null)Data.Companies.Add(company);else company.Version++;
    }
    public void SavePlacement(PlacementForm form,Internship? existing)
    {
        if(Student(form.StudentId) is null || Company(form.CompanyId) is null)throw new WorkspaceValidationException("Choose a student and company from your own demo workspace.");
        var start=form.StartDate.Date;var end=form.EndDate.Date;
        if(start<new DateTime(2000,1,1) || end>new DateTime(2100,12,31) || end<start || (end-start).TotalDays>730)
            throw new WorkspaceValidationException("Use dates between 2000 and 2100, with the end on or after the start and a duration no longer than two years.");
        var position=form.Position.Trim();
        if(Data.Internships.Any(x=>x.Id!=existing?.Id && x.StudentId==form.StudentId && x.CompanyId==form.CompanyId &&
            string.Equals(x.Position,position,StringComparison.OrdinalIgnoreCase) && x.StartDate.Date==start && x.EndDate.Date==end))
            throw new WorkspaceValidationException("That student already has this placement with the same company, position and dates.");
        if(existing is null && Data.Internships.Count>=75)throw new WorkspaceValidationException("Your demo allows 75 placements. Remove one or reset it.");
        if(existing is not null)
        {
            Current(existing.Version,form.Version);
            if(existing.HoursLogged>Math.Min(2000,((end-start).Days+1)*24))throw new WorkspaceValidationException("The new date range cannot contain the hours already logged. Keep a longer range.");
        }
        var placement=existing ?? new Internship();placement.StudentId=form.StudentId;placement.CompanyId=form.CompanyId;placement.Position=position;placement.StartDate=start;placement.EndDate=end;
        if(existing is null)Data.Internships.Add(placement);else placement.Version++;
    }
    public void AddHours(Internship placement,HoursForm form)
    {
        Current(placement.Version,form.Version);
        var maximum=Math.Min(2000,((placement.EndDate.Date-placement.StartDate.Date).Days+1)*24);
        if((long)placement.HoursLogged+form.HoursToAdd>maximum)throw new WorkspaceValidationException("The total cannot exceed 2,000 hours or the number of hours in the placement's date range.");
        placement.HoursLogged+=form.HoursToAdd;placement.Version++;
    }
    public void SaveFeedback(Internship placement,FeedbackForm form)
    { Current(placement.Version,form.Version);placement.SupervisorFeedback=(form.Feedback ?? "").Trim();placement.Version++; }
    public void DeleteStudent(Student student,int version)
    {
        Current(student.Version,version);
        if(Data.Internships.Any(x=>x.StudentId==student.Id))throw new WorkspaceValidationException("This student has placements. Remove those sample placements first.");
        Data.Students.Remove(student);
    }
    public void DeleteCompany(Company company,int version)
    {
        Current(company.Version,version);
        if(Data.Internships.Any(x=>x.CompanyId==company.Id))throw new WorkspaceValidationException("This company has placements. Remove those sample placements first.");
        Data.Companies.Remove(company);
    }
    public void DeletePlacement(Internship placement,int version)
    { Current(placement.Version,version);Data.Internships.Remove(placement); }
}
