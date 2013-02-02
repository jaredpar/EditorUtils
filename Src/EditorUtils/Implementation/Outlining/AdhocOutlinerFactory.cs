using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;

namespace EditorUtils.Implementation.Outlining
{
    /// <summary>
    /// Responsible for managing instances of IAdhocOutliner for a given ITextBuffer
    /// </summary>
    [Export(Constants.ContractName, typeof(IAdhocOutlinerFactory))]
    [Export(typeof(ITaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Structured)]
    [TagType(typeof(OutliningRegionTag))]
    internal sealed class AdhocOutlinerFactory : IAdhocOutlinerFactory, ITaggerProvider
    {
        private readonly object _adhocOutlinerKey = new object();
        private readonly object _taggerKey = new object();
        private readonly ITaggerFactory _taggerFactory;

        [ImportingConstructor]
        internal AdhocOutlinerFactory([Import(Constants.ContractName)] ITaggerFactory taggerFactory)
        {
            _taggerFactory = taggerFactory;
        }

        internal AdhocOutliner GetOrCreateOutliner(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(_adhocOutlinerKey, () => new AdhocOutliner(textBuffer));
        }

        internal ITagger<OutliningRegionTag> CreateTagger(ITextBuffer textBuffer)
        {
            return _taggerFactory.CreateBasicTagger(
                textBuffer.Properties,
                _taggerKey,
                () => GetOrCreateOutliner(textBuffer));
        }

        IAdhocOutliner IAdhocOutlinerFactory.GetAdhocOutliner(ITextBuffer textBuffer)
        {
            return GetOrCreateOutliner(textBuffer);
        }

        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            var tagger = CreateTagger(textBuffer);
            return (ITagger<T>)(object)tagger;
        }
    }
}
