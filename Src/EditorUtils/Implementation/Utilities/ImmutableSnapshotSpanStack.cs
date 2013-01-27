using System;
using System.Collections.Generic;

namespace EditorUtils.Implementation.Utilities
{
    [UsedInBackgroundThread]
    internal sealed class ImmutableSnapshotLineRangeStack : IEnumerable<SnapshotLineRange>
    {
        internal static readonly ImmutableSnapshotLineRangeStack Empty = new ImmutableSnapshotLineRangeStack();

        private readonly ImmutableSnapshotLineRangeStack _next;
        private readonly SnapshotLineRange _value;
        private readonly int _count;

        internal bool IsEmpty
        {
            get { return _next == null; }
        }

        internal int Count
        {
            get { return _count; }
        }

        internal SnapshotLineRange Value
        {
            get
            {
                ThrowIfEmpty();
                return _value;
            }
        }

        private ImmutableSnapshotLineRangeStack()
        {

        }

        private ImmutableSnapshotLineRangeStack(SnapshotLineRange lineRange, ImmutableSnapshotLineRangeStack next)
        {
            _value = lineRange;
            _next = next;
            _count = next.Count + 1;
        }

        internal ImmutableSnapshotLineRangeStack Add(SnapshotLineRange span)
        {
            return new ImmutableSnapshotLineRangeStack(span, this);
        }

        internal ImmutableSnapshotLineRangeStack Pop()
        {
            ThrowIfEmpty();
            return _next;
        }

        internal ImmutableSnapshotLineRangeStack Push(SnapshotLineRange lineRange)
        {
            return new ImmutableSnapshotLineRangeStack(lineRange, this);
        }

        private void ThrowIfEmpty()
        {
            if (IsEmpty)
            {
                throw new Exception("Stack is empty");
            }
        }

        public IEnumerator<SnapshotLineRange> GetEnumerator()
        {
            var top = this;
            while (!top.IsEmpty)
            {
                yield return top.Value;
                top = top.Pop();
            }
        }

        #region IEnumerable<SnapshotLineRange>

        IEnumerator<SnapshotLineRange> IEnumerable<SnapshotLineRange>.GetEnumerator()
        {
            return GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
