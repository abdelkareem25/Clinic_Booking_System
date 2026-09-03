using Microsoft.AspNetCore.Identity;

namespace Clinic.Domain.Entites.Identity
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; }

        // There is deliberately no Address navigation. The Address entity it pointed at was reached
        // by no query, mapped by no profile, exposed by no DTO and written by no controller, yet it
        // produced a real table whose every column was unbounded text NOT NULL and which cascaded
        // deletes from AspNetUsers. The patient address shown in the SPA is unrelated - a single
        // string held in browser-local storage, covering detail the API's Patient entity does not
        // carry. Dropped in DropAddressTable; add it back as a configured entity if the need is real.

        /// <summary>
        /// Whether the account may sign in.
        ///
        /// Distinct from Identity's LockoutEnd, which is a temporary, automatic consequence of
        /// failed sign-in attempts. This one is a deliberate administrative decision and does not
        /// expire, so the two must not be conflated: reading a lockout as "deactivated" would let
        /// a suspended account quietly come back on its own once the lockout window elapsed.
        /// Enforced in AccountsController.Login.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Soft delete. Deleted accounts keep their row so that CreatedBy/ModifiedBy stamps on
        /// clinical records still resolve to a person - which is the whole point of an audit trail.
        /// Every account query filters on this; see AccountsController.
        /// </summary>
        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAtUtc { get; set; }

        /// <summary>When the account was provisioned. IdentityUser records no creation time.</summary>
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>
        /// MULTI-TENANT: the clinic this account belongs to, or null for an account that
        /// belongs to no single clinic (the seeded administrator, or platform-level support).
        ///
        /// AppUser deliberately does NOT implement ITenantEntity, so it gets no global query
        /// filter. Identity looks accounts up BEFORE a tenant is known - FindByEmailAsync in
        /// Login runs before any token exists - and UserManager queries the same DbSet a filter
        /// would apply to, so filtering AspNetUsers by tenant would make every sign-in fail.
        /// This column is read at login and written into the token instead; TokenService turns
        /// it into the claim that every clinical query is then filtered by.
        /// </summary>
        public int? TenantId { get; set; }
    }
}
