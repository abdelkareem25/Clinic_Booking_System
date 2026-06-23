namespace Clinic.Domain.Interfaces.Specifications.SpecParams
{
    public class PaginationParams
    {
        private const int MaxPageSize = 20;

        public int PageIndex { get; set; } = 1;

        private int pageSize = 5;

        public int PageSize
        {
            get => pageSize;
            set => pageSize = value > MaxPageSize
                ? MaxPageSize
                : value;
        }

    }
}
