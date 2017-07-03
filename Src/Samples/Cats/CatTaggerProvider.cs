using System.ComponentModel.Composition;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Cats
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class CatTaggerProvider : IViewTaggerProvider
    {
        private readonly object _key = new object();

        [ImportingConstructor]
        internal CatTaggerProvider()
        {
#if DEBUG
            EditorUtilsTrace.TraceSwitch.Level = System.Diagnostics.TraceLevel.Info;
#endif
        }

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            if (textView.TextBuffer != textBuffer)
            {
                return null;
            }

            var tagger = EditorUtilsFactory.CreateTagger<string, TextMarkerTag>(
                textView.Properties,
                _key,
                () => new CatTagger(textView));

            return (ITagger<T>)(object)tagger;
        }

        #endregion
    }
}
