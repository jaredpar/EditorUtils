using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace WordUnderCaret
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(Constants.FormatName)]
    [UserVisible(true)] 
    internal sealed class WordUnderCaretFormat : MarkerFormatDefinition
    {
        public WordUnderCaretFormat()
        {
            DisplayName = Constants.FormatDisplayName;
            BackgroundColor = Colors.BlueViolet;
        }
    }
}
