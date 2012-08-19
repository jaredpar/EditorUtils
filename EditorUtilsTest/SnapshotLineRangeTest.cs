using Microsoft.VisualStudio.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public class SnapshotLineRangeTest : EditorHost
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
        }

        [Fact]
        public void Lines1()
        {
            Create("a", "b");
            var lineRange = SnapshotLineRange.CreateForLineAndMaxCount(_buffer.GetLine(0), 400);
            Assert.Equal(2, lineRange.Count);
        }
    }
}
