using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;
using System.IO;

namespace EditorUtils
{
    /// <summary>
    /// Base class for hosting editor components.  This is primarily used for unit 
    /// testing. Any test base can derive from this and use the Create* methods to get
    /// ITextBuffer instances to run their tests against. 
    /// </summary>
    public class EditorHost
    {
        #region UndoExportProvider

        /// <summary>
        /// In order to host the editor we need to provide an ITextUndoHistory export.  However 
        /// we can't simply export it from the DLL because it would conflict with Visual Studio's
        /// export of ITextUndoHistoryRegistry in the default scenario.  This ComposablePartCatalog
        /// is simply here to hand export the type in the hosted scenario only
        /// </summary>
        private sealed class UndoExportProvider : ExportProvider
        {
            private readonly IBasicUndoHistoryRegistry _basicUndoHistoryRegistry;
            private readonly string _textUndoHistoryRegistryContractName;
            private readonly string _basicUndoHistoryRegistryContractName;
            private readonly Export _export;

            internal UndoExportProvider()
            {
                _textUndoHistoryRegistryContractName = AttributedModelServices.GetContractName(typeof(ITextUndoHistoryRegistry));
                _basicUndoHistoryRegistryContractName = AttributedModelServices.GetContractName(typeof(IBasicUndoHistoryRegistry));
                _basicUndoHistoryRegistry = EditorUtilsFactory.CreateBasicUndoHistoryRegistry();
                _export = new Export(_textUndoHistoryRegistryContractName, () => _basicUndoHistoryRegistry);
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
            {
                if (definition.ContractName == _textUndoHistoryRegistryContractName ||
                    definition.ContractName == _basicUndoHistoryRegistryContractName)
                {
                    yield return _export;
                }
            }
        }

        #endregion

        private static readonly string[] EditorComponents =
            new[]
            {
                // Core editor components
                "Microsoft.VisualStudio.Platform.VSEditor.dll",

                // Not entirely sure why this is suddenly needed
                "Microsoft.VisualStudio.Text.Internal.dll",

                // Must include this because several editor options are actually stored as exported information 
                // on this DLL.  Including most importantly, the tabsize information
                "Microsoft.VisualStudio.Text.Logic.dll",

                // Include this DLL to get several more EditorOptions including WordWrapStyle
                "Microsoft.VisualStudio.Text.UI.dll",

                // Include this DLL to get more EditorOptions values and the core editor
                "Microsoft.VisualStudio.Text.UI.Wpf.dll"
            };

        [ThreadStatic]
        private static CompositionContainer _editorUtilsCompositionContainer;

        private CompositionContainer _compositionContainer;
        private ITextBufferFactoryService _textBufferFactoryService;
        private ITextEditorFactoryService _textEditorFactoryService;
        private IProjectionBufferFactoryService _projectionBufferFactoryService;
        private ISmartIndentationService _smartIndentationService;
        private IEditorOperationsFactoryService _editorOperationsFactoryService;
        private IEditorOptionsFactoryService _editorOptionsFactoryService;
        private IOutliningManagerService _outliningManagerService;
        private ITextSearchService _textSearchService;
        private ITextBufferUndoManagerProvider _textBufferUndoManagerProvider;
        private IContentTypeRegistryService _contentTypeRegistryService;
        private IProtectedOperations _protectedOperations;
        private IBasicUndoHistoryRegistry _basicUndoHistoryRegistry;

        public CompositionContainer CompositionContainer
        {
            get { return _compositionContainer; }
        }

        public ISmartIndentationService SmartIndentationService
        {
            get { return _smartIndentationService; }
        }

        public ITextBufferFactoryService TextBufferFactoryService
        {
            get { return _textBufferFactoryService; }
        }

        public ITextEditorFactoryService TextEditorFactoryService
        {
            get { return _textEditorFactoryService; }
        }

        public IProjectionBufferFactoryService ProjectionBufferFactoryService
        {
            get { return _projectionBufferFactoryService; }
        }

        public IEditorOperationsFactoryService EditorOperationsFactoryService
        {
            get { return _editorOperationsFactoryService; }
        }

        public IEditorOptionsFactoryService EditorOptionsFactoryService
        {
            get { return _editorOptionsFactoryService; }
        }

        public ITextSearchService TextSearchService
        {
            get { return _textSearchService; }
        }

        public ITextBufferUndoManagerProvider TextBufferUndoManagerProvider
        {
            get { return _textBufferUndoManagerProvider; }
        }

        public IOutliningManagerService OutliningManagerService
        {
            get { return _outliningManagerService; }
        }

        public IContentTypeRegistryService ContentTypeRegistryService
        {
            get { return _contentTypeRegistryService; }
        }

        public IProtectedOperations ProtectedOperations
        {
            get { return _protectedOperations; }
        }

        public IBasicUndoHistoryRegistry BasicUndoHistoryRegistry
        {
            get { return _basicUndoHistoryRegistry; }
        }

        public EditorHost()
        {
            Reset();
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines
        /// </summary>
        public ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return _textBufferFactoryService.CreateTextBuffer(lines);
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given IContentType
        /// </summary>
        public ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            return _textBufferFactoryService.CreateTextBuffer(contentType, lines);
        }

        /// <summary>
        /// Create a simple IProjectionBuffer from the specified SnapshotSpan values
        /// </summary>
        public IProjectionBuffer CreateProjectionBuffer(params SnapshotSpan[] spans)
        {
            var list = new List<object>();
            foreach (var span in spans)
            {
                var snapshot = span.Snapshot;
                var trackingSpan = snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive);
                list.Add(trackingSpan);
            }

            return ProjectionBufferFactoryService.CreateProjectionBuffer(
                null,
                list,
                ProjectionBufferOptions.None);
        }

        /// <summary>
        /// Create an ITextView instance with the given lines
        /// </summary>
        public IWpfTextView CreateTextView(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return _textEditorFactoryService.CreateTextView(textBuffer);
        }

        public IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            var textBuffer = _textBufferFactoryService.CreateTextBuffer(contentType, lines);
            return _textEditorFactoryService.CreateTextView(textBuffer);
        }

        /// <summary>
        /// Get or create a content type of the specified name with the specified base content type
        /// </summary>
        public IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = ContentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = ContentTypeRegistryService.AddContentType(type, new[] { baseType });
            }

            return ct;
        }

        /// <summary>
        /// The MEF composition container for the current thread.  We cache all of our compositions in this
        /// container to speed up the unit tests
        /// </summary>
        protected virtual CompositionContainer GetOrCreateCompositionContainer()
        {
            if (_editorUtilsCompositionContainer == null)
            {
                var composablePartCatalogList = new List<ComposablePartCatalog>();
                var exportProviderList = new List<ExportProvider>();

                // First allow the derived type to insert any catalogs or providers that 
                // it wants to add
                GetEditorHostParts(composablePartCatalogList, exportProviderList);

                // Now get the core editor catalog information
                if (!TryGetEditorCatalog(composablePartCatalogList))
                {
                    throw new Exception("Could not locate the editor components.  Is Visual Studio installed?");
                }

                // Add in our custom undo export 
                exportProviderList.Add(new UndoExportProvider());

                var catalog = new AggregateCatalog(composablePartCatalogList.ToArray());
                _editorUtilsCompositionContainer = new CompositionContainer(catalog, exportProviderList.ToArray());
            }

            return _editorUtilsCompositionContainer;
        }

        /// <summary>
        /// This method allows the host of EditorHost to provide additional ComposablePartCatalog instances or 
        /// ExportProvider values 
        /// </summary>
        protected virtual void GetEditorHostParts(List<ComposablePartCatalog> composablePartCatalogList, List<ExportProvider> exportProviderList)
        {

        }

        /// <summary>
        /// Fully reset the composition container and all exported values
        /// </summary>
        protected void Reset()
        {
            _compositionContainer = GetOrCreateCompositionContainer();
            _textBufferFactoryService = _compositionContainer.GetExportedValue<ITextBufferFactoryService>();
            _textEditorFactoryService = _compositionContainer.GetExportedValue<ITextEditorFactoryService>();
            _projectionBufferFactoryService = _compositionContainer.GetExportedValue<IProjectionBufferFactoryService>();
            _smartIndentationService = _compositionContainer.GetExportedValue<ISmartIndentationService>();
            _editorOperationsFactoryService = _compositionContainer.GetExportedValue<IEditorOperationsFactoryService>();
            _editorOptionsFactoryService = _compositionContainer.GetExportedValue<IEditorOptionsFactoryService>();
            _textSearchService = _compositionContainer.GetExportedValue<ITextSearchService>();
            _outliningManagerService = _compositionContainer.GetExportedValue<IOutliningManagerService>();
            _textBufferUndoManagerProvider = _compositionContainer.GetExportedValue<ITextBufferUndoManagerProvider>();
            _contentTypeRegistryService = _compositionContainer.GetExportedValue<IContentTypeRegistryService>();

            var errorHandlers = _compositionContainer.GetExportedValues<IExtensionErrorHandler>();
            _protectedOperations = EditorUtilsFactory.CreateProtectedOperations(errorHandlers);
            _basicUndoHistoryRegistry = _compositionContainer.GetExportedValue<IBasicUndoHistoryRegistry>();
        }

        /// <summary>
        /// Try and load the core editor catalog directly from the GAC.  
        /// </summary>
        private static bool TryGetEditorCatalog(List<ComposablePartCatalog> list)
        {
            string version;
            string installDirectory;
            if (!TryCalculateVersion(out version, out installDirectory))
            {
                return false;
            }

            if (!TryLoadInteropAssembly(installDirectory))
            {
                return false;
            }

            try
            {
                // Load the core editor compontents from the GAC
                string versionInfo = string.Format(", Version={0}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", version);
                foreach (var name in EditorComponents)
                {
                    var simpleName = name.Substring(0, name.Length - 4);
                    var qualifiedName = simpleName + versionInfo;
                    var assembly = Assembly.Load(qualifiedName);
                    list.Add(new AssemblyCatalog(assembly));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try and calculate the version of Visual Studio installed on this machine.  Need both the version
        /// and the install directory in order to load up the editor components for testing
        /// </summary>
        private static bool TryCalculateVersion(out string version, out string installDirectory)
        {
            if (TryGetInstallDirectory("10.0", out installDirectory))
            {
                version = "10.0.0.0";
                return true;
            }

            if (TryGetInstallDirectory("11.0", out installDirectory))
            {
                version = "11.0.0.0";
                return true;
            }

            installDirectory = null;
            version = null;
            return false;
        }

        /// <summary>
        /// Try and get the installation directory for the specified version of Visual Studio.  This 
        /// will fail if the specified version of Visual Studio isn't installed
        /// </summary>
        private static bool TryGetInstallDirectory(string version, out string installDirectory)
        {
            try
            {
                var subKeyPath = String.Format(@"Software\Microsoft\VisualStudio\{0}", version);
                using (var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false))
                {
                    installDirectory = key.GetValue("InstallDir", null) as string;
                    if (!String.IsNullOrEmpty(installDirectory))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore and try the next version
            }

            installDirectory = null;
            return false;
        }

        /// <summary>
        /// The interop assembly isn't included in the GAC and it doesn't offer any MEF components (it's
        /// just a simple COM interop library).  Hence it needs to be loaded a bit specially.  Just find
        /// the assembly on disk and hook into the resolve event
        /// </summary>
        private static bool TryLoadInteropAssembly(string installDirectory)
        {
            const string interopName = "Microsoft.VisualStudio.Platform.VSEditor.Interop";
            const string interopNameWithExtension = interopName + ".dll";
            var interopAssemblyPath = Path.Combine(installDirectory, "PrivateAssemblies");
            interopAssemblyPath = Path.Combine(interopAssemblyPath, interopNameWithExtension);
            try
            {
                var interopAssembly = Assembly.LoadFrom(interopAssemblyPath);
                if (interopAssembly == null)
                {
                    return false;
                }

                var comparer = StringComparer.OrdinalIgnoreCase;
                AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                    {
                        if (comparer.Equals(e.Name, interopAssembly.FullName))
                        {
                            return interopAssembly;
                        }

                        return null;
                    };

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
