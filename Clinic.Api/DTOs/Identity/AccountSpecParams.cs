namespace Clinic.Api.DTOs.Identity
{
    /// <summary>
    /// Query for the accounts list.
    ///
    /// Not a BaseSpecification: AppUser is an IdentityUser, not a BaseEntity, so it cannot go
    /// through IGenericRepository/ISpecification at all. This is the same contract expressed for
    /// UserManager.Users, which is the only queryable Identity exposes.
    /// </summary>
    public class AccountSpecParams
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 10;
        private int _pageIndex = 1;

        /// <summary>One-based, matching every other paged endpoint here.</summary>
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = value < 1 ? 1 : value;
        }

        /// <summary>Clamped so a hostile PageSize cannot ask for the whole table.</summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value switch
            {
                > MaxPageSize => MaxPageSize,
                < 1 => 1,
                _ => value
            };
        }

        /// <summary>Matched against display name, username and email.</summary>
        public string? Search { get; set; }

        public string? Role { get; set; }

        /// <summary>"active" or "inactive"; anything else means no status filter.</summary>
        public string? Status { get; set; }

        /// <summary>"NameDesc", "CreatedAsc", "CreatedDesc"; default is name ascending.</summary>
        public string? Sort { get; set; }

        public int Skip => (PageIndex - 1) * PageSize;
    }
}
