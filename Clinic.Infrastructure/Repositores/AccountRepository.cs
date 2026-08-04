using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Clinic.Infrastructure.Data.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Repositores
{
    /// <summary>
    /// Account queries against the Identity tables. See <see cref="IAccountRepository"/> for why
    /// these do not go through the generic repository.
    /// </summary>
    public class AccountRepository : IAccountRepository
    {
        private readonly ClinicDbContext _context;

        public AccountRepository(ClinicDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<AccountListItem> Items, int TotalCount)> ListAsync(
            string? search,
            string? role,
            bool? isActive,
            string? sort,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = BuildQuery(search, role, isActive);

            // Counted from the FILTERED query but before Skip/Take, so the pager reports how many
            // rows match rather than how many are on this page.
            var totalCount = await query.CountAsync(cancellationToken);

            query = sort switch
            {
                "NameDesc" => query.OrderByDescending(x => x.User.DisplayName),
                "CreatedAsc" => query.OrderBy(x => x.User.CreatedAtUtc),
                "CreatedDesc" => query.OrderByDescending(x => x.User.CreatedAtUtc),
                _ => query.OrderBy(x => x.User.DisplayName),
            };

            var items = await query
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items.Select(x => new AccountListItem(x.User, x.Role)).ToList(), totalCount);
        }

        public async Task<AccountListItem?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            var match = await BuildQuery(search: null, role: null, isActive: null)
                .FirstOrDefaultAsync(x => x.User.Id == id, cancellationToken);

            return match is null ? null : new AccountListItem(match.User, match.Role);
        }

        public Task<bool> EmailInUseAsync(string email, string? excludeUserId, CancellationToken cancellationToken = default)
        {
            var normalised = Normalise(email);

            return _context.Users.AsNoTracking().AnyAsync(
                user => !user.IsDeleted
                        && user.NormalizedEmail == normalised
                        && (excludeUserId == null || user.Id != excludeUserId),
                cancellationToken);
        }

        public Task<bool> UserNameInUseAsync(string userName, string? excludeUserId, CancellationToken cancellationToken = default)
        {
            var normalised = Normalise(userName);

            return _context.Users.AsNoTracking().AnyAsync(
                user => !user.IsDeleted
                        && user.NormalizedUserName == normalised
                        && (excludeUserId == null || user.Id != excludeUserId),
                cancellationToken);
        }

        public Task<bool> IsRetiredIdentifierAsync(string email, string? userName, CancellationToken cancellationToken = default)
        {
            var normalisedEmail = Normalise(email);
            var normalisedUserName = Normalise(userName ?? email);

            return _context.Users.AsNoTracking().AnyAsync(
                user => user.IsDeleted
                        && (user.NormalizedEmail == normalisedEmail
                            || user.NormalizedUserName == normalisedUserName),
                cancellationToken);
        }

        /// <summary>
        /// Accounts, each with the role it holds.
        ///
        /// The role is a correlated subquery, NOT a join. A join would emit one row per
        /// user-role pair, so any account that ended up holding two roles would appear twice in the
        /// list and twice in the total - and the paging would then be quietly wrong for every page
        /// after it. This shape is one row per account by construction, whatever the data says.
        ///
        /// FirstOrDefault rather than Single for the same reason: the roster has to render an
        /// account with two roles, not throw on it. AccountsController.ReplaceRoleAsync is what
        /// keeps accounts to one role going forward.
        ///
        /// An account with NO role still appears, with an empty role. Dropping it would hide
        /// exactly the accounts an administrator most needs to find - ones that can sign in and
        /// then do nothing, for a reason invisible from every other screen.
        /// </summary>
        private IQueryable<UserWithRole> BuildQuery(string? search, string? role, bool? isActive)
        {
            var users = _context.Users.AsNoTracking().Where(user => !user.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                users = users.Where(user =>
                    EF.Functions.Like(user.DisplayName, $"%{term}%")
                    || EF.Functions.Like(user.UserName!, $"%{term}%")
                    || EF.Functions.Like(user.Email!, $"%{term}%")
                    || EF.Functions.Like(user.PhoneNumber!, $"%{term}%"));
            }

            if (isActive.HasValue)
            {
                users = users.Where(user => user.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                // Filtered with Any over the join table rather than against the projected Role, so
                // it stays a semi-join and cannot multiply rows.
                users = users.Where(user =>
                    _context.UserRoles.Any(userRole =>
                        userRole.UserId == user.Id
                        && _context.Roles.Any(r => r.Id == userRole.RoleId && r.Name == role)));
            }

            return users.Select(user => new UserWithRole
            {
                User = user,
                Role = (from userRole in _context.UserRoles
                        join identityRole in _context.Roles on userRole.RoleId equals identityRole.Id
                        where userRole.UserId == user.Id
                        select identityRole.Name).FirstOrDefault() ?? string.Empty
            });
        }

        /// <summary>
        /// Identity's normaliser is invariant upper-case. Matching on the Normalized* columns rather
        /// than on Email/UserName is what makes the comparison case-insensitive on providers with a
        /// case-sensitive collation, and it is the indexed column besides.
        /// </summary>
        private static string Normalise(string value) => value.Trim().ToUpperInvariant();

        /// <summary>A named projection: EF cannot translate an anonymous type across method boundaries.</summary>
        private sealed class UserWithRole
        {
            public AppUser User { get; init; } = null!;
            public string Role { get; init; } = string.Empty;
        }
    }
}
