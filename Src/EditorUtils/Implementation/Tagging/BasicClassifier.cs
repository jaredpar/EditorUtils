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
        private readonly IBasicClassifierSource _basicClassifierSource;
        private EventHandler<ClassificationChangedEventArgs> _classificationChanged;

        internal BasicClassifier(IBasicClassifierSource basicClassifierSource)
        {
            Contract.Requires(basicClassifierSource != null);
            _basicClassifierSource = basicClassifierSource;
            _basicClassifierSource.Changed += OnBasicClassifierSourceChanged;
        }

        protected override void Dispose()
        {
            _basicClassifierSource.Changed -= OnBasicClassifierSourceChanged;
            var disposable = _basicClassifierSource as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        protected override ReadOnlyCollection<ClassificationSpan> GetProducedTagSpansCore(SnapshotSpan span)
        {
            return _basicClassifierSource.GetClassificationSpans(span);
        }

        private void OnBasicClassifierSourceChanged(object sender, EventArgs e)
        {
            if (CachedRequestSpan.HasValue && _classificationChanged != null)
            {
                var args = new ClassificationChangedEventArgs(CachedRequestSpan.Value);
                _classificationChanged(this, args);
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
