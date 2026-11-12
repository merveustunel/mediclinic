# Patient booking sequence

Sources: [AppointmentsController](../../src/MediClinic.Web/Controllers/AppointmentsController.cs), [IAppointmentBooking](../../src/MediClinic.Application/AppointmentBooking.cs), [AppointmentBooking](../../src/MediClinic.Infrastructure/AppointmentBooking.cs), [Appointment](../../src/MediClinic.Domain/Appointment.cs), and [SQLite triggers](../../src/MediClinic.Infrastructure/Persistence/Migrations/20261106022559_AppointmentScheduling.cs).

```mermaid
sequenceDiagram
    actor Patient
    participant MVC as AppointmentsController
    participant Booking as AppointmentBooking : IAppointmentBooking
    participant DB as MediClinicDbContext / SQLite
    participant Domain as Appointment
    Patient->>MVC: GET Book (Stage 1, catalog IDs)
    MVC->>MVC: PrepareBooking(model)
    MVC->>Booking: Options(UserId)
    Booking->>Booking: User(userId, Patient)
    Booking->>DB: Query user links/roles, doctors and services
    DB-->>Booking: Linked patient / explicitly assigned services and prices
    Booking-->>MVC: BookingOption list
    MVC-->>Patient: Cascading selections
    Patient->>MVC: GET Book (Stage 2, date, selection)
    MVC->>MVC: PrepareBooking(model), Options(UserId)
    MVC->>Booking: Slots(UserId, DoctorId, ServiceId, Date)
    Booking->>DB: Availability, branch hours and Blocking(doctorId)
    DB-->>Booking: Availability / configured hours / occupied intervals
    Booking-->>MVC: Future 15-minute starts fitting full duration
    MVC-->>Patient: Time cards
    Patient->>MVC: GET Book (Stage 3, LocalStart)
    MVC->>MVC: PrepareBooking(model), recheck Options and Slots
    MVC-->>Patient: Review form with antiforgery token
    Patient->>MVC: POST Book (selection, date, LocalStart, token)
    Note over MVC: Patient policy, antiforgery, binding/injection checks
    MVC->>MVC: PrepareBooking(model), recheck Options and Slots
    MVC->>Booking: Book(UserId, BookingRequest)
    Booking->>DB: User, catalog hierarchy, availability, overlap checks
    alt Checks pass
        Booking->>Domain: new Appointment(id, patient, doctor, service, start)
        Domain-->>Booking: Pending with EndsAt and price snapshots
        Booking->>DB: Appointments.Add(appointment), SaveChangesAsync()
        Note over DB: SQLite trigger rejects overlapping blocking writes
        Booking->>DB: Revalidate assignment, availability, hours, duration and price under write lock
        Note over Booking,DB: Commit only if still valid; otherwise rollback and retry feedback
        alt Saved
            DB-->>Booking: Persisted
            Booking-->>MVC: Appointment Id
            MVC-->>Patient: Redirect to Details(id)
        else Concurrent reservation / database busy
            DB-->>Booking: DbUpdateException
            Booking-->>MVC: BookingException
            MVC-->>Patient: Turkish refresh/retry feedback, Stage 2
        end
    else Invalid hierarchy, time, availability or overlap
        Booking-->>MVC: BookingException
        MVC-->>Patient: Turkish feedback, Stage 2
    end
```

For brevity repeated catalog/slot checks inside PrepareBooking are summarized on the MVC lifeline. Invalid model/slot checks can return the form before Book is called. The service independently rechecks authorization and business conditions; displayed availability is not a reservation. Times enter as Istanbul local DateTime, convert with Scheduling.ToUtc, and persist as UTC ticks. JavaScript enhances native GET selections; the sequence also works without it.
