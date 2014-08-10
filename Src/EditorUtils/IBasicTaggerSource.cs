using System;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils
{
    public interface IBasicTaggerSource<TTagSpan>
    {
        /// <summary>
        /// Get the tags for the given SnapshotSpan
        /// </summary>
        ReadOnlyCollection<TTagSpan> GetTags(SnapshotSpan span);

        /// <summary>
        /// Raised when the source changes in some way
        /// </summary>
        event EventHandler Changed;
    }
}
