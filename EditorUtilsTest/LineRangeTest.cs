using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class LineRangeTest
    {
        /// <summary>
        /// Set of not intersecting ranges
        /// </summary>
        [Fact]
        public void Intersects_SimpleDoesnt()
        {
            var left = LineRange.CreateFromBounds(0, 1);
            var right = LineRange.CreateFromBounds(3, 4);
            Assert.False(left.Intersects(right));
        }

        /// <summary>
        /// Set of intersecting ranges
        /// </summary>
        [Fact]
        public void Intersects_SimpleDoes()
        {
            var left = LineRange.CreateFromBounds(0, 2);
            var right = LineRange.CreateFromBounds(1, 4);
            Assert.True(left.Intersects(right));
        }

        /// <summary>
        /// The intersect if they have the same boundary lines (essentially if they touch
        /// each other)
        /// </summary>
        [Fact]
        public void Intersects_DoesAtBorder()
        {
            var left = LineRange.CreateFromBounds(0, 2);
            var right = LineRange.CreateFromBounds(3, 4);
            Assert.True(left.Intersects(right));
        }
    }
}
