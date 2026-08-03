using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Tests.Specifications
{
    /// <summary>
    /// Unit tests for TODO #19 (finding H6).
    ///
    /// Only the upper bound on PageSize was enforced. Every assertion below corresponds to a query
    /// string anyone could type that produced either a permanently empty endpoint or an unhandled
    /// ArgumentOutOfRangeException from Skip/Take - a trivially triggered 500.
    /// </summary>
    public sealed class PaginationParamsTests
    {
        private const int MaxPageSize = 20;
        private const int DefaultPageSize = 5;

        #region PageIndex

        [Theory]
        [InlineData(0)]        // ?pageIndex=0    -> Skip(-5)
        [InlineData(-1)]
        [InlineData(-100)]     // ?pageIndex=-100 -> Skip(-505)
        [InlineData(int.MinValue)]
        public void A_Page_Index_Below_One_Falls_Back_To_The_First_Page(int requested)
        {
            var param = new PaginationParams { PageIndex = requested };

            Assert.Equal(1, param.PageIndex);
            Assert.Equal(0, param.Skip);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(9999)]
        public void A_Valid_Page_Index_Is_Preserved(int requested)
        {
            Assert.Equal(requested, new PaginationParams { PageIndex = requested }.PageIndex);
        }

        [Fact]
        public void The_Default_Page_Index_Is_The_First_Page()
        {
            Assert.Equal(1, new PaginationParams().PageIndex);
        }

        #endregion

        #region PageSize

        [Theory]
        [InlineData(0)]        // ?pageSize=0  -> Take(0)  -> always empty
        [InlineData(-1)]       // ?pageSize=-1 -> Take(-1) -> throws
        [InlineData(int.MinValue)]
        public void A_Page_Size_Below_One_Falls_Back_To_The_Default(int requested)
        {
            Assert.Equal(DefaultPageSize, new PaginationParams { PageSize = requested }.PageSize);
        }

        [Theory]
        [InlineData(MaxPageSize + 1)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        public void A_Page_Size_Above_The_Maximum_Is_Capped(int requested)
        {
            // This half already worked; asserted so the fix to the lower bound cannot break it.
            Assert.Equal(MaxPageSize, new PaginationParams { PageSize = requested }.PageSize);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(MaxPageSize)]
        public void A_Valid_Page_Size_Is_Preserved(int requested)
        {
            Assert.Equal(requested, new PaginationParams { PageSize = requested }.PageSize);
        }

        [Fact]
        public void The_Default_Page_Size_Is_Applied_When_Nothing_Is_Requested()
        {
            Assert.Equal(DefaultPageSize, new PaginationParams().PageSize);
        }

        #endregion

        #region Skip

        [Theory]
        [InlineData(1, 5, 0)]
        [InlineData(2, 5, 5)]
        [InlineData(3, 10, 20)]
        [InlineData(4, 1, 3)]
        public void Skip_Is_The_Offset_Of_The_Requested_Page(int pageIndex, int pageSize, int expected)
        {
            var param = new PaginationParams { PageIndex = pageIndex, PageSize = pageSize };

            Assert.Equal(expected, param.Skip);
        }

        [Theory]
        [InlineData(-5, -5)]
        [InlineData(0, 0)]
        [InlineData(-1, 1000)]
        public void Skip_Is_Never_Negative_However_Hostile_The_Input(int pageIndex, int pageSize)
        {
            // Skip(-n) is what produced the 500s. It must be unreachable.
            var param = new PaginationParams { PageIndex = pageIndex, PageSize = pageSize };

            Assert.True(param.Skip >= 0, $"Skip was {param.Skip} for pageIndex={pageIndex}, pageSize={pageSize}.");
            Assert.True(param.PageSize >= 1);
        }

        [Fact]
        public void Skip_Reflects_Clamped_Values_Not_Requested_Ones()
        {
            // PageSize is capped at 20, so page 3 starts at row 40 - not at 3000.
            var param = new PaginationParams { PageIndex = 3, PageSize = 1500 };

            Assert.Equal(MaxPageSize, param.PageSize);
            Assert.Equal(2 * MaxPageSize, param.Skip);
        }

        #endregion

        #region Property order independence

        [Fact]
        public void Clamping_Does_Not_Depend_On_The_Order_Properties_Are_Bound()
        {
            // Model binding sets properties in whatever order the query string happens to list them.
            var sizeFirst = new PaginationParams { PageSize = -1, PageIndex = -1 };
            var indexFirst = new PaginationParams { PageIndex = -1, PageSize = -1 };

            Assert.Equal(sizeFirst.PageIndex, indexFirst.PageIndex);
            Assert.Equal(sizeFirst.PageSize, indexFirst.PageSize);
            Assert.Equal(sizeFirst.Skip, indexFirst.Skip);
        }

        #endregion
    }
}
