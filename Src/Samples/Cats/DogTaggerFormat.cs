using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace Cats
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    internal sealed class DogClassificationFormatDefinition : ClassificationFormatDefinition
    {
        internal const string Name = "Dog info";

        internal DogClassificationFormatDefinition()
        {
            ForegroundColor = Colors.Green;
        }
    }

    internal sealed class DogClassifications
    {
        [Name(DogClassificationFormatDefinition.Name)]
        [Export]
        internal ClassificationTypeDefinition DogClassificationType { get; set; }
    }
}
