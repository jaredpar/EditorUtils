using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils.Implementation.Tagging
{
    /// <summary>
    /// This solves the same problem as CountedTagger but for IClassifier
    /// </summary>
    internal sealed class CountedClassifier : IClassifier, IDisposable
    {
        private readonly IClassifier _classifier;
        private readonly object _key;
        private readonly PropertyCollection _propertyCollection;
        private int _count;

        internal IClassifier Classifier
        {
            get { return _classifier; }
        }

        internal CountedClassifier(
            IClassifier classifier,
            object key,
            PropertyCollection propertyCollection)
        {
            _classifier = classifier;
            _key = key;
            _propertyCollection = propertyCollection;
            _count = 1;
        }

        internal void Dispose()
        {
            _count--;

            if (_count == 0)
            {
                var disposable = _classifier as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                _propertyCollection.RemoveProperty(_key);
            }
        }

        internal static IClassifier Create(
            object key, 
            PropertyCollection propertyCollection,
            Func<IClassifier> createFunc)
        {
            CountedClassifier countedClassifier;
            if (propertyCollection.TryGetPropertySafe(key, out countedClassifier))
            {
                countedClassifier._count++;
                return countedClassifier;
            }

            countedClassifier = new CountedClassifier(createFunc(), key, propertyCollection);
            propertyCollection[key] = countedClassifier;
            return countedClassifier;
        }

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion

        #region IClassifier

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged
        {
            add { _classifier.ClassificationChanged += value; }
            remove { _classifier.ClassificationChanged -= value; }
        }


        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span)
        {
            return _classifier.GetClassificationSpans(span);
        }

        #endregion
    }
}
