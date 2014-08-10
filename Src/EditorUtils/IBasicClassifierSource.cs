using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace EditorUtils
{
    public interface IBasicClassifierSource
    {
        /// <summary>
        /// Get the tags for the given SnapshotSpan
        /// </summary>
        ReadOnlyCollection<ClassificationSpan> GetClassificationSpans(SnapshotSpan span);

        /// <summary>
        /// Raised when the source changes in some way
        /// </summary>
        event EventHandler Changed;
    }
}
