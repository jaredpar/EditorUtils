using EditorUtils.Implementation.Utilities;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class SingleItemQueueTest
    {
        internal SingleItemQueue<string> m_queue = new SingleItemQueue<string>();

        public sealed class TryDequeueTest : SingleItemQueueTest
        {
            /// <summary>
            /// Attempting a dequeue on an empty instance shouldn't fail
            /// </summary>
            [Fact]
            public void Empty()
            {
                string value = null;
                Assert.False(m_queue.TryDequeue(out value));
                Assert.Null(value);
            }
        }
    }
}
