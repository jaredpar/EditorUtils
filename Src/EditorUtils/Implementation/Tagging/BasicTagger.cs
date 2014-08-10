using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class BasicTagger<TTag> : BasicProducer<ITagSpan<TTag>>, ITagger<TTag>
        where TTag : ITag
    {
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        internal BasicTagger(IBasicTaggerSource<ITagSpan<TTag>> basicTaggerSource) : base(basicTaggerSource)
        {

        }

        protected override void OnChanged(SnapshotSpan changedSpan)
        {
            var list = _tagsChanged;
            if (list != null)
            {
                list(this, new SnapshotSpanEventArgs(changedSpan));
            }
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return GetProducedTagSpans(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagsChanged += value; }
            remove { _tagsChanged -= value; }
        }

        #endregion
    }
}
