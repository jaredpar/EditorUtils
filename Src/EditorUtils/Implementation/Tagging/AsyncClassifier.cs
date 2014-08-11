using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class AsyncClassifier<TData> : AsyncProducer<TData, ClassificationSpan>, IClassifier
    {
        private EventHandler<ClassificationChangedEventArgs> _classificationChanged;

        internal AsyncClassifier(IAsyncTaggerSource<TData, ClassificationSpan> asyncTaggerSource) : base(asyncTaggerSource, ClassificationSpanUtil.Instance)
        {

        }

        protected override void OnChanged(SnapshotSpan span)
        {
            var list = _classificationChanged;
            if (list != null)
            {
                list(this, new ClassificationChangedEventArgs(span));
            }
        }

        #region IClassifier 

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged
        {
            add { _classificationChanged += value; }
            remove { _classificationChanged -= value; }
        }

        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span)
        {
            return GetTags(new NormalizedSnapshotSpanCollection(span)).ToList();
        }

        #endregion
    }
}
