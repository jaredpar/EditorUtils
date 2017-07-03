using EditorUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text.Tagging;

namespace Cats
{
    internal sealed class DogClassifier : IBasicTaggerSource<IClassificationTag>
    {
        private readonly IClassificationType _classificationType;
#pragma warning disable 67
        public event EventHandler Changed;
#pragma warning restore 67

        internal DogClassifier(IClassificationType classificationType)
        {
            _classificationType = classificationType;
        }

        public ReadOnlyCollection<ITagSpan<IClassificationTag>> GetTags(SnapshotSpan span)
        {
            var list = new List<ITagSpan<IClassificationTag>>();
            var position = span.Start.Position;
            while (position < span.End.Position)
            {
                var point = new SnapshotPoint(span.Snapshot, position);
                if (IsDog(point))
                {
                    var dogSpan = new SnapshotSpan(point, 3);
                    var tagSpan = new TagSpan<IClassificationTag>(dogSpan, new ClassificationTag(_classificationType));
                    list.Add(tagSpan);
                    position += 3;
                }
                else
                {
                    position += 1;
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }

        private static bool IsDog(SnapshotPoint point)
        {
            var snapshot = point.Snapshot;
            if (point.Position + 2 < snapshot.Length &&
                snapshot[point.Position] == 'd' &&
                snapshot[point.Position + 1] == 'o' &&
                snapshot[point.Position + 2] == 'g')
            {
                return true;
            }

            return false;
        }
    }
}
