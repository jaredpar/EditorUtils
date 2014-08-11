using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class BasicClassifier : IClassifier, IDisposable
    {
        private readonly BasicTagger<IClassificationTag> _basicTagger;
        private event EventHandler<ClassificationChangedEventArgs> _classificationChanged;

        internal BasicClassifier(IBasicTaggerSource<IClassificationTag> basicTaggerSource)
        {
            _basicTagger = new BasicTagger<IClassificationTag>(basicTaggerSource);
            _basicTagger.TagsChanged += OnTagsChanged;
        }

        private void Dispose()
        {
            _basicTagger.TagsChanged -= OnTagsChanged;
            _basicTagger.Dispose();
        }

        private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
        {
            var list = _classificationChanged;
            if (list != null)
            {
                list(this, new ClassificationChangedEventArgs(e.Span));
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
            return _basicTagger
                .GetTags(new NormalizedSnapshotSpanCollection(span))
                .Select(x => new ClassificationSpan(x.Span, x.Tag.ClassificationType))
                .ToList();
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion 
    }
}
