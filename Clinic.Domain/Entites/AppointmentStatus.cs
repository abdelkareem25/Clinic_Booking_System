namespace Clinic.Domain.Entites
{
    /// <summary>
    /// The lifecycle of a booking, as the front desk understands it.
    ///
    /// Until now the API carried no status at all and the SPA derived one from the clock
    /// (Upcoming / Today / Past). That can never express the two states staff actually act on -
    /// a cancellation and a completed visit - because both are indistinguishable from "Past".
    /// Persisted as an int; see AppointmentConfig.
    /// </summary>
    public enum AppointmentStatus
    {
        Pending = 0,
        Confirmed = 1,
        Completed = 2,
        Cancelled = 3
    }
}
