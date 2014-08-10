
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.ObjectModel;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class BasicTagger<TTag> : BasicProducer<ITagSpan<TTag>>, ITagger<TTag>
        where TTag : ITag
    {
        private readonly IBasicTaggerSource<TTag> _basicTaggerSource;
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        internal BasicTagger(IBasicTaggerSource<TTag> basicTaggerSource)
        {
            Contract.Requires(basicTaggerSource != null);
            _basicTaggerSource = basicTaggerSource;
            _basicTaggerSource.Changed += OnBasicTaggerSourceChanged;
        }

        protected override void Dispose()
        {
            _basicTaggerSource.Changed -= OnBasicTaggerSourceChanged;
            var disposable = _basicTaggerSource as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        protected override ReadOnlyCollection<ITagSpan<TTag>> GetProducedTagSpansCore(SnapshotSpan span)
        {
            return _basicTaggerSource.GetTags(span);
        }

        private void OnBasicTaggerSourceChanged(object sender, EventArgs e)
        {
            if (CachedRequestSpan.HasValue && _tagsChanged != null)
            {
                var args = new SnapshotSpanEventArgs(CachedRequestSpan.Value);
                _tagsChanged(this, args);
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
