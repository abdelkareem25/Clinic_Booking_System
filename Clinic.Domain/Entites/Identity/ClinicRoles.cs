namespace Clinic.Domain.Entites.Identity
{
    /// <summary>
    /// The roles this system recognises.
    ///
    /// These are const so they can be used in [Authorize(Roles = ...)] attributes. That matters more
    /// than it looks: a role named in an attribute but never created is not an error anywhere - the
    /// check simply never passes, and the endpoint returns 403 forever. Sharing one definition
    /// between the attributes and the seeder makes that class of bug a compile error instead, and
    /// AuthorizationRoleTests asserts the two stay in step.
    /// </summary>
    public static class ClinicRoles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Receptionist = "Receptionist";
        public const string Patient = "Patient";

        public static readonly string[] All = [Admin, Doctor, Receptionist, Patient];
    }
}
