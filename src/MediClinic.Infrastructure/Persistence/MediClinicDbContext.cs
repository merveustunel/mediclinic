using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MediClinic.Infrastructure.Persistence;

public sealed class MediClinicDbContext(DbContextOptions<MediClinicDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<MedicalService> MedicalServices => Set<MedicalService>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<StaffAvailability> StaffAvailabilities => Set<StaffAvailability>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var user = modelBuilder.Entity<ApplicationUser>();
        user.HasOne(x => x.Patient).WithOne().HasForeignKey<ApplicationUser>(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        user.HasOne(x => x.Staff).WithOne().HasForeignKey<ApplicationUser>(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        user.ToTable("AspNetUsers", t => t.HasCheckConstraint("CK_User_OnePerson", "\"PatientId\" IS NULL OR \"StaffId\" IS NULL"));
        var clinic = Root<Clinic>(modelBuilder, "Clinics");
        Text(clinic, nameof(Clinic.Name));
        clinic.HasMany(x => x.Branches).WithOne(x => x.Clinic).HasForeignKey("ClinicKey")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        clinic.Navigation(x => x.Branches).HasField("_branches").UsePropertyAccessMode(PropertyAccessMode.Field);

        var branch = Root<Branch>(modelBuilder, "Branches");
        branch.Ignore(x => x.ClinicId);
        branch.Property<Guid>("ClinicKey").HasColumnName("ClinicId");
        Text(branch, nameof(Branch.Name));
        Text(branch, nameof(Branch.Address));
        branch.Navigation(x => x.Clinic).UsePropertyAccessMode(PropertyAccessMode.Field);
        branch.HasMany(x => x.Departments).WithOne(x => x.Branch).HasForeignKey("BranchKey")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        branch.Navigation(x => x.Departments).HasField("_departments").UsePropertyAccessMode(PropertyAccessMode.Field);

        var department = Root<Department>(modelBuilder, "Departments");
        department.Ignore(x => x.BranchId);
        department.Property<Guid>("BranchKey").HasColumnName("BranchId");
        Text(department, nameof(Department.Name));
        department.Navigation(x => x.Branch).UsePropertyAccessMode(PropertyAccessMode.Field);
        department.HasMany(x => x.Services).WithOne(x => x.Department).HasForeignKey("DepartmentKey")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        department.Navigation(x => x.Services).HasField("_services").UsePropertyAccessMode(PropertyAccessMode.Field);

        var service = Root<MedicalService>(modelBuilder, "MedicalServices");
        service.Ignore(x => x.DepartmentId);
        service.Property<Guid>("DepartmentKey").HasColumnName("DepartmentId");
        Text(service, nameof(MedicalService.Name));
        service.Property(x => x.Duration).HasConversion(x => x.Ticks, x => TimeSpan.FromTicks(x)).IsRequired();
        service.ToTable(t => t.HasCheckConstraint("CK_MedicalServices_Duration", "\"Duration\" > 0"));
        service.Navigation(x => x.Department).UsePropertyAccessMode(PropertyAccessMode.Field);

        var person = Root<Person>(modelBuilder, "People");
        Text(person, nameof(Person.FirstName));
        Text(person, nameof(Person.LastName));
        person.Ignore(x => x.FullName);
        person.Ignore(x => x.Role);
        person.HasDiscriminator<string>("PersonType").HasValue<Patient>("Patient")
            .HasValue<Staff>("Staff").HasValue<Doctor>("Doctor");
        person.ToTable(t =>
        {
            t.HasCheckConstraint("CK_People_Type", "\"PersonType\" IN ('Patient', 'Staff', 'Doctor')");
            t.HasCheckConstraint("CK_People_Patient", "\"PersonType\" <> 'Patient' OR \"DateOfBirth\" IS NOT NULL");
            t.HasCheckConstraint("CK_People_Staff", "\"PersonType\" NOT IN ('Staff', 'Doctor') OR (\"DepartmentId\" IS NOT NULL AND \"JobTitle\" IS NOT NULL AND length(trim(\"JobTitle\")) > 0)");
            t.HasCheckConstraint("CK_People_Doctor", "\"PersonType\" <> 'Doctor' OR (\"Specialty\" IS NOT NULL AND length(trim(\"Specialty\")) > 0 AND \"JobTitle\" = 'Doctor')");
        });
        modelBuilder.Entity<Patient>().Property(x => x.DateOfBirth).IsRequired();
        var staff = modelBuilder.Entity<Staff>();
        staff.Ignore(x => x.DepartmentId);
        staff.Property<Guid>("DepartmentKey").HasColumnName("DepartmentId");
        staff.Property(x => x.JobTitle).IsRequired();
        staff.HasOne(x => x.Department).WithMany().HasForeignKey("DepartmentKey")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        staff.Navigation(x => x.Department).UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<Doctor>().Property(x => x.Specialty).IsRequired();

        var availability = Root<StaffAvailability>(modelBuilder, "StaffAvailabilities");
        availability.Ignore(x => x.StaffId);
        availability.Property<Guid>("StaffKey").HasColumnName("StaffId");
        availability.Property(x => x.StartsAt).IsRequired();
        availability.Property(x => x.EndsAt).IsRequired();
        availability.HasOne(x => x.Staff).WithMany().HasForeignKey("StaffKey")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        availability.Navigation(x => x.Staff).UsePropertyAccessMode(PropertyAccessMode.Field);
        availability.HasIndex("StaffKey", nameof(StaffAvailability.StartsAt));
        availability.ToTable(t => t.HasCheckConstraint("CK_StaffAvailabilities_Interval", "\"EndsAt\" > \"StartsAt\""));

        var appointment = Root<Appointment>(modelBuilder, "Appointments");
        appointment.Ignore(x => x.PatientId);
        appointment.Ignore(x => x.DoctorId);
        appointment.Ignore(x => x.ServiceId);
        appointment.Ignore(x => x.Branch);
        appointment.Ignore(x => x.BranchId);
        appointment.Property<Guid>("PatientKey").HasColumnName("PatientId");
        appointment.Property<Guid>("DoctorKey").HasColumnName("DoctorId");
        appointment.Property<Guid>("ServiceKey").HasColumnName("ServiceId");
        appointment.HasOne(x => x.Patient).WithMany().HasForeignKey("PatientKey").IsRequired().OnDelete(DeleteBehavior.Restrict);
        appointment.HasOne(x => x.Doctor).WithMany().HasForeignKey("DoctorKey").IsRequired().OnDelete(DeleteBehavior.Restrict);
        appointment.HasOne(x => x.Service).WithMany().HasForeignKey("ServiceKey").IsRequired().OnDelete(DeleteBehavior.Restrict);
        appointment.Navigation(x => x.Patient).UsePropertyAccessMode(PropertyAccessMode.Field);
        appointment.Navigation(x => x.Doctor).UsePropertyAccessMode(PropertyAccessMode.Field);
        appointment.Navigation(x => x.Service).UsePropertyAccessMode(PropertyAccessMode.Field);
        appointment.Property(x => x.StartsAt).IsRequired();
        appointment.Property(x => x.EndsAt).IsRequired();
        appointment.Property(x => x.Status).HasConversion<string>().IsRequired();
        appointment.Property(x => x.DecisionReason);
        appointment.HasIndex("DoctorKey", nameof(Appointment.StartsAt));
        appointment.HasIndex("PatientKey", nameof(Appointment.StartsAt));
        appointment.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Appointments_Interval", "\"EndsAt\" > \"StartsAt\"");
            t.HasCheckConstraint("CK_Appointments_Status", "\"Status\" IN ('Pending', 'Confirmed', 'Rejected', 'Cancelled', 'Completed')");
            t.HasCheckConstraint("CK_Appointments_Reason", "\"Status\" NOT IN ('Rejected', 'Cancelled') OR (\"DecisionReason\" IS NOT NULL AND length(trim(\"DecisionReason\")) > 0)");
        });
    }

    private static EntityTypeBuilder<T> Root<T>(ModelBuilder modelBuilder, string table) where T : Entity
    {
        var entity = modelBuilder.Entity<T>();
        entity.ToTable(table);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        return entity;
    }

    private static void Text<T>(EntityTypeBuilder<T> entity, string property) where T : Entity
    {
        entity.Property<string>(property).IsRequired();
        entity.ToTable(t => t.HasCheckConstraint($"CK_{typeof(T).Name}_{property}", $"length(trim(\"{property}\")) > 0"));
    }
}

// INTEGER preserves chronological comparisons and full .NET tick precision in SQLite.
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
