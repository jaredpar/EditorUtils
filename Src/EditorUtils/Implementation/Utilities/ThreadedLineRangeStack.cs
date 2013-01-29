using System.Threading;

namespace EditorUtils.Implementation.Utilities
{

    /// <summary>
    /// The foreground and background thread use instances of this type to communicate what line ranges
    /// spans need to be retrieved for.  The background thread is constantly pulling SnapshotSpan values
    /// off to process and the foreground process is adding more for requests as they come in.
    /// 
    /// There is *no* danger in adding extra requests for the background thread.  It is responsible, for
    /// many other reasons, for avoiding duplicate work.  If the same span comes down, it shouldn't process
    /// it.  
    /// </summary>
    [UsedInBackgroundThread]
    internal sealed class ThreadedLineRangeStack
    {
        private ImmutableSnapshotLineRangeStack _stack;
        private int _version;

        internal int CurrentVersion
        {
            get { return _version; }
        }

        internal ImmutableSnapshotLineRangeStack CurrentStack
        {
            get { return _stack; }
        }

        internal ThreadedLineRangeStack()
        {
            _stack = ImmutableSnapshotLineRangeStack.Empty;
        }

        internal void Push(SnapshotLineRange lineRange)
        {
            bool success;
            do
            {
                var oldStack = _stack;
                var newStack = _stack.Push(lineRange);
                success = oldStack == Interlocked.CompareExchange(ref _stack, newStack, oldStack);
            } while (!success);

            Interlocked.Increment(ref _version);
        }

        internal SnapshotLineRange? Pop()
        {
            bool success;
            SnapshotLineRange lineRange;
            do
            {
                var oldStack = _stack;
                if (oldStack.IsEmpty)
                {
                    return null;
                }

                lineRange = oldStack.Value;
                var newStack = _stack.Pop();
                success = oldStack == Interlocked.CompareExchange(ref _stack, newStack, oldStack);
            } while (!success);

            return lineRange;
        }
    }
}
