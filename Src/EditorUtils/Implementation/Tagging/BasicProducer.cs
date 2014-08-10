
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.ObjectModel;

namespace EditorUtils.Implementation.Tagging
{
    internal abstract class BasicProducer<TTagSpan> : IDisposable
    {
        private SnapshotSpan? _cachedRequestSpan;

        internal SnapshotSpan? CachedRequestSpan
        {
            get { return _cachedRequestSpan; }
            set { _cachedRequestSpan = value; }
        }

        internal BasicProducer()
        {

        }

        protected abstract void Dispose();

        protected abstract ReadOnlyCollection<TTagSpan> GetProducedTagSpansCore(SnapshotSpan span);

        private void AdjustRequestSpan(NormalizedSnapshotSpanCollection col)
        {
            if (col.Count > 0)
            {
                var requestSpan = col.GetOverarchingSpan();
                _cachedRequestSpan = TaggerUtil.AdjustRequestedSpan(_cachedRequestSpan, requestSpan);
            }
        }

        protected ReadOnlyCollection<TTagSpan> GetProducedTagSpans(NormalizedSnapshotSpanCollection col)
        {
            AdjustRequestSpan(col);
            if (col.Count == 0)
            {
                return new ReadOnlyCollection<TTagSpan>(new TTagSpan[] { });
            }

            if (col.Count == 1)
            {
                return GetProducedTagSpansCore(col[0]);
            }

            // Even though it's easier don't do a GetTags request for the overarching SnapshotSpan
            // of the request.  It's possible for the overarching SnapshotSpan to have an order
            // magnitudes more lines than the items in the collection.  This is very possible when
            // large folded regions or on screen.  Instead just request the individual ones
            var list = new List<TTagSpan>();
            foreach (var span in col)
            {
                list.AddRange(GetProducedTagSpansCore(span));
            }

            return list.ToReadOnlyCollectionShallow();
        }

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion
    }
}
