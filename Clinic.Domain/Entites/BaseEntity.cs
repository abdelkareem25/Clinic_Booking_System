namespace Clinic.Domain.Entites
{
    public class BaseEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// Optimistic concurrency token, rotated on every insert and update by ClinicDbContext.
        ///
        /// Without it, Update() issues a blind UPDATE of every column: two receptionists opening the
        /// same appointment both save, and the second silently overwrites the first with data that
        /// was already stale when their screen loaded. In a booking system that destroys real
        /// reservations with no error and no trace - the classic lost update.
        ///
        /// A Guid maintained by the application rather than SQL Server's native rowversion, so the
        /// behaviour is identical on every provider and can actually be tested. The trade-off is
        /// that it only detects writes that go through EF; a native rowversion would also catch raw
        /// SQL. Every write in this application goes through the unit of work, so that is covered.
        /// </summary>
        public Guid RowVersion { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>Null when the row was created by an unauthenticated request or by seeding.</summary>
        public string? CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAtUtc { get; set; }

        public string? ModifiedBy { get; set; }
    }
}
