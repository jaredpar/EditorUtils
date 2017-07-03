using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using EditorUtils;

namespace Cats
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("text")]
    internal sealed class DogClassifierProvider : IClassifierProvider
    {
        IClassificationTypeRegistryService _classificationTypeRegistryService;

        [ImportingConstructor]
        internal DogClassifierProvider(IClassificationTypeRegistryService classificationTypeRegistryService)
        {
            _classificationTypeRegistryService = classificationTypeRegistryService;
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            var classificationType = _classificationTypeRegistryService.GetClassificationType(DogClassificationFormatDefinition.Name);
            return EditorUtilsFactory.CreateClassifierRaw(new DogClassifier(classificationType));
        }
    }
}
