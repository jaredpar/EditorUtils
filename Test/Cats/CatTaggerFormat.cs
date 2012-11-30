using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Cats
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(Constants.FormatName)]
    [UserVisible(true)]
    internal sealed class CatTaggerFormat : MarkerFormatDefinition
    {
        public CatTaggerFormat()
        {
            DisplayName = Constants.FormatDisplayName;
            BackgroundColor = Colors.BlueViolet;
        }
    }
}
