using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class BasicClassifier : BasicProducer<ClassificationSpan>, IClassifier
    {
        private EventHandler<ClassificationChangedEventArgs> _classificationChanged;

        internal BasicClassifier(IBasicTaggerSource<ClassificationSpan> basicTaggerSource) : base(basicTaggerSource)
        {

        }

        protected override void OnChanged(SnapshotSpan changedSpan)
        {
            var list = _classificationChanged;
            if (list != null)
            {
                list(this, new ClassificationChangedEventArgs(changedSpan));
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
            return GetProducedTagSpans(new NormalizedSnapshotSpanCollection(span));
        }

        #endregion
    }
}
