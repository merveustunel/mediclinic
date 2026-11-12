# Domain class diagram

Source: [Domain](../../src/MediClinic.Domain). Selected public members are shown; all concrete domain entities inherit Entity, directly or through Person/Staff. Multiplicities describe the object model; database deletion uses Restrict rather than cascading deletion. Guard is an internal validation helper, not an entity.

```mermaid
classDiagram
    class Entity {
        <<abstract>>
        +Guid Id
    }
    class Person {
        <<abstract>>
        +string FirstName
        +string LastName
        +string FullName
        +string Role
        +Rename(string firstName, string lastName) void
    }
    class Patient {
        +DateOnly DateOfBirth
        +string Role
    }
    class Staff {
        +string JobTitle
        +string Role
    }
    class Doctor {
        +string Specialty
        +string Role
        +CanProvide(MedicalService service) bool
        +Offers(MedicalService service) bool
        +AssignServices(IEnumerable~MedicalService~ services) void
    }
    class Clinic {
        +string Name
        +AddBranch(Guid id, string name, string address) Branch
    }
    class Branch {
        +string Name
        +string Address
        +AddDepartment(Guid id, string name) Department
    }
    class Department {
        +string Name
        +AddService(Guid id, string name, TimeSpan duration) MedicalService
    }
    class MedicalService {
        +string Name
        +TimeSpan Duration
        +decimal Price
        +Update(string name, int durationMinutes, decimal price) void
    }
    class WorkingHours {
        +Guid BranchId
        +DayOfWeek Day
        +bool Closed
        +TimeOnly Opens
        +TimeOnly Closes
        +Update(bool closed, TimeOnly opens, TimeOnly closes) void
    }
    class StaffAvailability {
        +DateTimeOffset StartsAt
        +DateTimeOffset EndsAt
        +Covers(DateTimeOffset startsAt, DateTimeOffset endsAt) bool
    }
    class Appointment {
        +DateTimeOffset StartsAt
        +DateTimeOffset EndsAt
        +AppointmentStatus Status
        +decimal Price
        +string DecisionReason
        +Confirm() void
        +Reject(string reason) void
        +Cancel(string reason) void
        +Complete(DateTimeOffset completedAt) void
    }
    class AppointmentStatus {
        <<enumeration>>
        Pending
        Confirmed
        Rejected
        Cancelled
        Completed
    }
    Entity <|-- Person
    Person <|-- Patient
    Person <|-- Staff
    Staff <|-- Doctor
    Entity <|-- Clinic
    Entity <|-- Branch
    Entity <|-- Department
    Entity <|-- MedicalService
    Entity <|-- StaffAvailability
    Entity <|-- Appointment
    Entity <|-- WorkingHours
    Clinic "1" *-- "0..*" Branch : Branches / Clinic
    Branch "1" *-- "0..*" Department : Departments / Branch
    Branch "1" <-- "0..7" WorkingHours : BranchId
    Department "1" *-- "0..*" MedicalService : Services / Department
    Department "1" <-- "0..*" Staff : Department
    Staff "1" <-- "0..*" StaffAvailability : Staff
    Patient "1" <-- "0..*" Appointment : Patient
    Doctor "1" <-- "0..*" Appointment : Doctor
    MedicalService "1" <-- "0..*" Appointment : Service
    Doctor "0..*" --> "0..*" MedicalService : explicit Services
    Appointment --> AppointmentStatus : Status
```

`DecisionReason`, appointment `Price`, and working-hours `Opens`/`Closes` are nullable. Appointment Price and StartsAt/EndsAt retain booking-time price/duration; NULL price is unknown legacy data. Appointment.Branch derives from Service.Department.Branch. WorkingHours associates by BranchId, with a unique branch/day pair; missing weekdays are unrestricted. DoctorServices persists the explicit many-to-many assignment. ClinicAdministrator is an Infrastructure authorization record; see [architecture](layered-architecture.md). Navigation IDs otherwise derive from referenced entities; EF uses shadow FKs for those relationships. Read-only collections protect mutation.
