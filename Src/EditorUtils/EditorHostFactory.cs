using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.Win32;

namespace EditorUtils
{
    public sealed class EditorHostFactory
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

        // Determine the minimum Visual Studio number that we should be probing for.  It is okay to 
        // find later versions because Visual Studio is back compat.  Using an earlier version does
        // not work though 
#if VS2010 
        private const int MinVisualStudioVersion = 10;
#elif VS2012 
        private const int MinVisualStudioVersion = 11;
#elif VS2013 
        private const int MinVisualStudioVersion = 12;
#else
        private const int MinVisualStudioVersion = 10;
#endif

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

        /// <summary>
        /// A list of key names for versions of Visual Studio which have the editor components 
        /// necessary to create an EditorHost instance.  Listed in preference order
        /// </summary>
        private static readonly string[] VisualStudioSkuKeyNames =
            new[]
            {
                // Standard non-express SKU of Visual Studio
                "VisualStudio",

                // Windows Desktop express
                "WDExpress",

                // Visual C# express
                "VCSExpress",

                // Visual C++ express
                "VCExpress",

                // Visual Basic Express
                "VBExpress",
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        public EditorHostFactory()
        {
            AppendEditorCatalog(_composablePartCatalogList);
            _exportProviderList.Add(new UndoExportProvider());
        }

        public void Add(ComposablePartCatalog composablePartCatalog)
        {
            _composablePartCatalogList.Add(composablePartCatalog);
        }

        public void Add(ExportProvider exportProvider)
        {
            _exportProviderList.Add(exportProvider);
        }

        public CompositionContainer CreateCompositionContainer()
        {
            var catalog = new AggregateCatalog(_composablePartCatalogList.ToArray());
            return new CompositionContainer(catalog, _exportProviderList.ToArray());
        }

        public EditorHost CreateEditorHost()
        {
            return new EditorHost(CreateCompositionContainer());
        }

        /// <summary>
        /// Load the list of editor assemblies into the specified catalog list.  This method will
        /// throw on failure
        /// </summary>
        private static void AppendEditorCatalog(List<ComposablePartCatalog> list)
        {
            string version;
            string installDirectory;
            if (!TryCalculateVersion(out version, out installDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            Console.WriteLine("Version = {0}", version);
            Console.WriteLine("Install Directory = {0}", installDirectory);
            if (!TryLoadInteropAssembly(installDirectory))
            {
                var message = string.Format("Unable to load the interop assemblies.  Install directory is: ", installDirectory);
                throw new Exception(message);
            }

            // Load the core editor compontents from the GAC
            string versionInfo = string.Format(", Version={0}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", version);
            foreach (var name in EditorComponents)
            {
                var simpleName = name.Substring(0, name.Length - 4);
                var qualifiedName = simpleName + versionInfo;
                var assembly = Assembly.Load(qualifiedName);
                list.Add(new AssemblyCatalog(assembly));
            }
        }

        /// <summary>
        /// Try and calculate the version of Visual Studio installed on this machine.  Need both the version
        /// and the install directory in order to load up the editor components for testing
        /// </summary>
        private static bool TryCalculateVersion(out string version, out string installDirectory)
        {
            // The same pattern exists for all known versions of Visual Studio.  The editor was 
            // introduced in version 10 (VS2010).  The max of 20 is arbitrary and just meant to 
            // future proof this algorithm for some time into the future
            for (int i = MinVisualStudioVersion; i < 20; i++)
            {
                var shortVersion = String.Format("{0}.0", i);
                if (TryGetInstallDirectory(shortVersion, out installDirectory))
                {
                    version = String.Format("{0}.0.0.0", i);
                    return true;
                }
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
            foreach (var skuKeyName in VisualStudioSkuKeyNames)
            {
                if (TryGetInstallDirectory(skuKeyName, version, out installDirectory))
                {
                    return true;
                }
            }

            installDirectory = null;
            return false;
        }

        /// <summary>
        /// Try and get the installation directory for the specified SKU of Visual Studio.  This 
        /// will fail if the specified version of Visual Studio isn't installed
        /// </summary>
        private static bool TryGetInstallDirectory(string skuKeyName, string version, out string installDirectory)
        {
            try
            {
                var subKeyPath = String.Format(@"Software\Microsoft\{0}\{1}", skuKeyName, version);
                using (var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false))
                {
                    if (key != null)
                    {
                        installDirectory = key.GetValue("InstallDir", null) as string;
                        if (!String.IsNullOrEmpty(installDirectory))
                        {
                            return true;
                        }
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
