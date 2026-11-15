using MediClinic.Domain;
using MediClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MediClinic.Web.Models;

public sealed class AdminFilters
{
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Search { get; set; }
    public string? Role { get; set; }
    public List<Branch> Branches { get; set; } = [];
    public List<Department> Departments { get; set; } = [];

    public async Task<bool> Populate(MediClinicDbContext db, string userId)
    {
        Branches = await db.Branches.Where(b => db.ClinicAdministrators.Any(a => a.UserId == userId && a.ClinicId == EF.Property<Guid>(b, "ClinicKey")))
            .Include(b => b.Clinic).OrderBy(b => b.Name).ToListAsync();
        var ids = Branches.Select(b => b.Id).ToList();
        Departments = await db.Departments.Include(d => d.Branch).Where(d => ids.Contains(EF.Property<Guid>(d, "BranchKey"))).OrderBy(d => d.Name).ToListAsync();
        var valid = (!BranchId.HasValue || ids.Contains(BranchId.Value)) &&
            (!DepartmentId.HasValue || Departments.Any(d => d.Id == DepartmentId && (!BranchId.HasValue || d.BranchId == BranchId)));
        return valid;
    }

    public static bool MatchesName(string name, string search) =>
        CultureInfo.GetCultureInfo("tr-TR").CompareInfo.IndexOf(name, search.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
