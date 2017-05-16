﻿using System;
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
    public sealed partial class EditorHostFactory
    {
        // Determine the Visual Studio number that we are compiled against.  This is important for 
        // doing probing of editor binaries (okay to load later, but not earlier).  
#if VS2010 
        private const EditorVersion ReferencedEditorVersion = EditorVersion.Vs2010;
#elif VS2012
        private const EditorVersion ReferencedEditorVersion = EditorVersion.Vs2012;
#elif VS2013
        private const EditorVersion ReferencedEditorVersion = EditorVersion.Vs2013;
#elif VS2015
        private const EditorVersion ReferencedEditorVersion = EditorVersion.Vs2015;
#elif VS2017
        private const EditorVersion ReferencedEditorVersion = EditorVersion.Vs2017;
#else
#error Unexpected build combination 
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

        /// <summary>
        /// The minimum <see cref="EditorVersion"/> value supported by this assembly. 
        /// </summary>
        public static EditorVersion MinimumEditorVersion
        {
            get { return ReferencedEditorVersion; }
        }

        public static EditorVersion MaxEditorVersion
        {
            get { return EditorVersion.Vs2017; }
        }

        public EditorHostFactory(EditorVersion? editorVersion = null)
        {
            AppendEditorCatalog(_composablePartCatalogList, editorVersion);
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
        private static void AppendEditorCatalog(List<ComposablePartCatalog> list, EditorVersion? editorVersion)
        {
            string version;
            string installDirectory;
            if (!TryGetEditorInfo(editorVersion, out version, out installDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            if (!TryLoadInteropAssembly(installDirectory))
            {
                var message = string.Format("Unable to load the interop assemblies.  Install directory is: ", installDirectory);
                throw new Exception(message);
            }

            // Load the core editor compontents from the GAC
            var versionInfo = string.Format(", Version={0}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", version);
            foreach (var name in EditorComponents)
            {
                var simpleName = name.Substring(0, name.Length - 4);
                var qualifiedName = simpleName + versionInfo;

                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(qualifiedName);
                }
                catch (Exception e)
                {
                    var msg = string.Format("Unable to load editor dependency {0}", name);
                    throw new Exception(msg, e);
                }

                list.Add(new AssemblyCatalog(assembly));
            }
        }

        private static bool TryGetEditorInfo(EditorVersion? editorVersion, out string fullVersion, out string installDirectory)
        {
            if (editorVersion.HasValue)
            {
                var shortVersion = GetShortVersionString(editorVersion.Value);
                return TryGetEditorInfoCore(shortVersion, out fullVersion, out installDirectory);
            }

            return TryCalculateEditorInfo(out fullVersion, out installDirectory);
        }

        /// <summary>
        /// Try and calculate the version of Visual Studio installed on this machine.  Need both the version
        /// and the install directory in order to load up the editor components for testing
        /// </summary>
        private static bool TryCalculateEditorInfo(out string fullVersion, out string installDirectory)
        {
            // The same pattern exists for all known versions of Visual Studio.  The editor was 
            // introduced in version 10 (VS2010).  The max of 20 is arbitrary and just meant to 
            // future proof this algorithm for some time into the future
            var max = GetVersionNumber(MaxEditorVersion);
            for (int i = GetVersionNumber(MinimumEditorVersion); i <= max; i++)
            {
                var shortVersion = String.Format("{0}.0", i);
                if (TryGetEditorInfoCore(shortVersion, out fullVersion, out installDirectory))
                {
                    return true;
                }
            }

            installDirectory = null;
            fullVersion = null;
            return false;
        }

        private static bool TryGetEditorInfoCore(string shortVersion, out string fullversion, out string installDirectory)
        {
            if (TryGetInstallDirectory(shortVersion, out installDirectory))
            {
                fullversion = string.Format("{0}.0.0", shortVersion);
                return true;
            }

            fullversion = null;
            return false;
        }

        /// <summary>
        /// Try and get the installation directory for the specified version of Visual Studio.  This 
        /// will fail if the specified version of Visual Studio isn't installed
        /// </summary>
        private static bool TryGetInstallDirectory(string shortVersion, out string installDirectory)
        {
            foreach (var skuKeyName in VisualStudioSkuKeyNames)
            {
                if (TryGetInstallDirectory(skuKeyName, shortVersion, out installDirectory))
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
        private static bool TryGetInstallDirectory(string skuKeyName, string shortVersion, out string installDirectory)
        {
            try
            {
                var subKeyPath = String.Format(@"Software\Microsoft\{0}\{1}", skuKeyName, shortVersion);
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

        internal static int GetVersionNumber(EditorVersion version)
        {
            switch (version)
            {
                case EditorVersion.Vs2010: return 10;
                case EditorVersion.Vs2012: return 11;
                case EditorVersion.Vs2013: return 12;
                case EditorVersion.Vs2015: return 14;
                case EditorVersion.Vs2017: return 15;
                default:
                    throw new Exception(string.Format("Unexpected enum value {0}", version));
            }
        }

        internal static string GetShortVersionString(EditorVersion version)
        {
            var number = GetVersionNumber(version);
            return string.Format("{0}.0", number);
        }
    }
}
