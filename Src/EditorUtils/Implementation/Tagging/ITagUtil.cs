using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EditorUtils.Implementation.Tagging
{
    internal interface ITagUtil<TTag>
    {
        /// <summary>
        /// Get the SnapshotSpan for the given tag
        /// </summary>
        SnapshotSpan GetSpan(TTag tag);

        /// <summary>
        /// Create a new instance of the given tag at the provided SnapshotSpan
        /// </summary>
        TTag CreateTag(TTag oldTag, SnapshotSpan newSpan);
    }

    internal sealed class TagSpanUtil<TTag> : ITagUtil<ITagSpan<TTag>>
        where TTag : ITag
    {
        internal static readonly TagSpanUtil<TTag> Instance = new TagSpanUtil<TTag>();

        ITagSpan<TTag> ITagUtil<ITagSpan<TTag>>.CreateTag(ITagSpan<TTag> oldTag, SnapshotSpan newSpan)
        {
            return new TagSpan<TTag>(newSpan, oldTag.Tag);
        }

        SnapshotSpan ITagUtil<ITagSpan<TTag>>.GetSpan(ITagSpan<TTag> tagSpan)
        {
            return tagSpan.Span;
        }
    }

    internal sealed class ClassificationSpanUtil : ITagUtil<ClassificationSpan>
    {
        internal static readonly ClassificationSpanUtil Instance = new ClassificationSpanUtil();

        ClassificationSpan ITagUtil<ClassificationSpan>.CreateTag(ClassificationSpan oldTag, SnapshotSpan newSpan)
        {
            return new ClassificationSpan(newSpan, oldTag.ClassificationType);
        }

        SnapshotSpan ITagUtil<ClassificationSpan>.GetSpan(ClassificationSpan tagSpan)
        {
            return tagSpan.Span;
        }
    }
}
