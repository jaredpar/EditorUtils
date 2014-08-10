using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class AsyncTagger<TData, TTag> : AsyncProducer<TData, ITagSpan<TTag>>, ITagger<TTag>
        where TTag : ITag
    {
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        internal AsyncTagger(IAsyncTaggerSource<TData, ITagSpan<TTag>> asyncTaggerSource) : base(asyncTaggerSource)
        {

        }

        protected override void OnChanged(SnapshotSpan span)
        {
            var list = _tagsChanged;
            if (list != null)
            {
                list(this, new SnapshotSpanEventArgs(span));
            }
        }

        #region ITagger<TTag>

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagsChanged += value; }
            remove { _tagsChanged -= value; }
        }

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return GetTags(spans);
        }

        #endregion
    }
}
