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
    Clinic "1" *-- "0..*" Branch : Branches / Clinic
    Branch "1" *-- "0..*" Department : Departments / Branch
    Department "1" *-- "0..*" MedicalService : Services / Department
    Department "1" <-- "0..*" Staff : Department
    Staff "1" <-- "0..*" StaffAvailability : Staff
    Patient "1" <-- "0..*" Appointment : Patient
    Doctor "1" <-- "0..*" Appointment : Doctor
    MedicalService "1" <-- "0..*" Appointment : Service
    Appointment --> AppointmentStatus : Status
```

`DecisionReason` is nullable. Appointment.Branch is derived from Service.Department.Branch, not an independent editable association. Navigation ID properties derive from the referenced entity; EF uses shadow foreign keys for these relationships. Read-only parent collections are not arbitrary public setters.
