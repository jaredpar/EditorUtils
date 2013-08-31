using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EditorUtils;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace WordUnderCaret
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class WordUnderCaretTaggerProvider : IViewTaggerProvider
    {
        private readonly object _key = new object();

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            if (textView.TextBuffer != textBuffer)
            {
                return null;
            }

            var tagger = EditorUtilsFactory.CreateAsyncTagger<string, TextMarkerTag>(
                textView.Properties,
                _key,
                () => new WordUnderCaretTagger(textView));

            return (ITagger<T>)(object)tagger;
        }

        #endregion
    }
}
