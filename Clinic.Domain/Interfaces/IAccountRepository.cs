using Clinic.Domain.Entites.Identity;

namespace Clinic.Domain.Interfaces
{
    /// <summary>One account plus the role it holds, as one row.</summary>
    public sealed record AccountListItem(AppUser User, string Role);

    /// <summary>
    /// Reads over the account roster.
    ///
    /// AppUser is an IdentityUser, not a BaseEntity, so it cannot go through
    /// IGenericRepository/ISpecification - those are constrained to BaseEntity and key on int. This
    /// interface exists for the same reason the specification pattern does elsewhere: to keep the
    /// query out of the controller and the DbContext out of the API project.
    ///
    /// The role has to be joined in here rather than resolved per user with
    /// UserManager.GetRolesAsync, which would be one round trip per row - a page of 100 accounts
    /// meaning 101 queries.
    /// </summary>
    public interface IAccountRepository
    {
        /// <summary>
        /// One page of live (not soft-deleted) accounts, newest filters applied, with each account's
        /// role. Returns the page and the true total, which is counted before paging.
        /// </summary>
        Task<(IReadOnlyList<AccountListItem> Items, int TotalCount)> ListAsync(
            string? search,
            string? role,
            bool? isActive,
            string? sort,
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>A single live account, or null when it is missing or soft-deleted.</summary>
        Task<AccountListItem?> FindAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when another live account already uses this email. Excludes the account being
        /// edited, so saving a form without changing the address is not a conflict with itself.
        /// </summary>
        Task<bool> EmailInUseAsync(string email, string? excludeUserId, CancellationToken cancellationToken = default);

        /// <summary>See <see cref="EmailInUseAsync"/>.</summary>
        Task<bool> UserNameInUseAsync(string userName, string? excludeUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when a SOFT-DELETED account holds this email or username.
        ///
        /// Needed because the unique index on AspNetUsers still covers deleted rows, so
        /// re-provisioning the address would fail with a constraint violation the caller cannot
        /// interpret. Asking first lets the API say what really happened.
        /// </summary>
        Task<bool> IsRetiredIdentifierAsync(string email, string? userName, CancellationToken cancellationToken = default);
    }
}
