
using System;
using System.Linq;
using EditorUtils.Implementation.Tagging;
using EditorUtils.Implementation.BasicUndo;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils
{
    /// <summary>
    /// Importable interface which produces ITagger implementations based on sources
    /// </summary>
    public static class EditorUtilsFactory
    {
        private static readonly object _adhocOutlinerKey = new object();
        private static readonly object _adhocOutlinerTaggerKey = new object();

        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.
        /// </summary>
        public static ITagger<TTag> CreateAsyncTaggerRaw<TData, TTag>(IAsyncTaggerSource<TData, ITagSpan<TTag>> asyncTaggerSource)
            where TTag : ITag
        {
            return new AsyncTagger<TData, TTag>(asyncTaggerSource);
        }

        /// <summary>
        /// Create an ITagger implementation for the IAsyncTaggerSource.  This instance will be a counted 
        /// wrapper over the single IAsyncTaggerSource represented by the specified key
        /// </summary>
        public static ITagger<TTag> CreateAsyncTagger<TData, TTag>(PropertyCollection propertyCollection, object key, Func<IAsyncTaggerSource<TData, ITagSpan<TTag>>> createFunc)
            where TTag : ITag
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new AsyncTagger<TData, TTag>(createFunc()));
        }

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource
        /// </summary>
        public static ITagger<TTag> CreateBasicTaggerRaw<TTag>(IBasicTaggerSource<ITagSpan<TTag>> basicTaggerSource)
            where TTag : ITag
        {
            return new BasicTagger<TTag>(basicTaggerSource);
        }

        /// <summary>
        /// Create an ITagger implementation for the IBasicTaggerSource.  This instance will be a counted
        /// wrapper over the single IBasicTaggerSource represented by the specified key
        /// </summary>
        public static ITagger<TTag> CreateBasicTagger<TTag>(PropertyCollection propertyCollection, object key, Func<IBasicTaggerSource<ITagSpan<TTag>>> createFunc)
            where TTag : ITag
        {
            return CountedTagger<TTag>.Create(
                key,
                propertyCollection,
                () => new BasicTagger<TTag>(createFunc()));
        }

        public static IClassifier CreateBasicClassifierRaw(IBasicTaggerSource<ClassificationSpan> basicTaggerSource)
        {
            return new BasicClassifier(basicTaggerSource);
        }

        public static IBasicUndoHistoryRegistry CreateBasicUndoHistoryRegistry()
        {
            return new BasicTextUndoHistoryRegistry();
        }

        public static IProtectedOperations CreateProtectedOperations(IEnumerable<Lazy<IExtensionErrorHandler>> errorHandlers)
        {
            return new ProtectedOperations(errorHandlers);
        }

        public static IProtectedOperations CreateProtectedOperations(IEnumerable<IExtensionErrorHandler> errorHandlers)
        {
            var lazyList = errorHandlers.Select(x => new Lazy<IExtensionErrorHandler>(() => x)).ToList();
            return new ProtectedOperations(lazyList);
        }

        /// <summary>
        /// Get or create the IAdhocOutliner instance for the given ITextBuffer.  This return will be useless 
        /// unless the code which calls this method exports an ITaggerProvider which proxies the return 
        /// of GetOrCreateOutlinerTagger
        /// </summary>
        public static IAdhocOutliner GetOrCreateOutliner(ITextBuffer textBuffer)
        {
            return GetOrCreateOutlinerCore(textBuffer);
        }

        /// <summary>
        /// This is the ITagger implementation for IAdhocOutliner
        /// </summary>
        public static ITagger<OutliningRegionTag> CreateOutlinerTagger(ITextBuffer textBuffer)
        {
            return CreateBasicTagger(
                textBuffer.Properties,
                _adhocOutlinerTaggerKey,
                () => GetOrCreateOutlinerCore(textBuffer));
        }

        private static AdhocOutliner GetOrCreateOutlinerCore(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(_adhocOutlinerKey, () => new AdhocOutliner(textBuffer));
        }
    }
}
