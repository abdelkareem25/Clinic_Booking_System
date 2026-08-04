using Microsoft.AspNetCore.Identity;

namespace Clinic.Domain.Entites.Identity
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; }
        public Address Address { get; set; }

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
    }
}
