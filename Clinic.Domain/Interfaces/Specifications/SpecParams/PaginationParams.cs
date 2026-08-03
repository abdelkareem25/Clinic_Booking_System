namespace Clinic.Domain.Interfaces.Specifications.SpecParams
{
    /// <summary>
    /// Paging inputs, clamped at BOTH ends.
    ///
    /// Only the upper bound on PageSize was enforced, which left every one of these reachable from
    /// the query string by anyone:
    ///
    ///   ?pageSize=0    -> Take(0)      -> an endpoint that always returns nothing
    ///   ?pageSize=-1   -> Take(-1)     -> ArgumentOutOfRangeException -> HTTP 500
    ///   ?pageIndex=0   -> Skip(-5)     -> ArgumentOutOfRangeException -> HTTP 500
    ///   ?pageIndex=-100 -> Skip(-505)  -> ArgumentOutOfRangeException -> HTTP 500
    ///
    /// Trivially triggered 500s are a cheap denial-of-service and an error-log flood. Out-of-range
    /// values are clamped rather than rejected: a paginator that sends pageIndex=0 for its first
    /// page should get the first page, not an error.
    /// </summary>
    public class PaginationParams
    {
        private const int MaxPageSize = 20;
        private const int DefaultPageSize = 5;

        private int pageIndex = 1;
        private int pageSize = DefaultPageSize;

        /// <summary>One-based. Anything below 1 is treated as the first page.</summary>
        public int PageIndex
        {
            get => pageIndex;
            set => pageIndex = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => pageSize;
            set => pageSize = value switch
            {
                // A request for zero or fewer rows is a mistake, not an instruction to return an
                // empty page forever.
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => value
            };
        }

        /// <summary>
        /// Rows to skip for the current page.
        ///
        /// Computed here rather than repeated as (PageIndex - 1) * PageSize in every specification:
        /// that expression appeared four times, and it is exactly the sort of arithmetic where an
        /// off-by-one hides. One definition means the clamping above provably protects every caller.
        /// </summary>
        public int Skip => (PageIndex - 1) * PageSize;
    }
}
