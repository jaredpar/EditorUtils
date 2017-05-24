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
    public sealed partial class EditorHostFactory
    {
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
                "Microsoft.VisualStudio.Text.UI.Wpf.dll",
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        /// <summary>
        /// The minimum <see cref="EditorVersion"/> value supported by this assembly. 
        /// </summary>
        public static EditorVersion MinimumEditorVersion
        {
            get { return EditorVersionUtil.MinVersion; }
        }

        public static EditorVersion MaxEditorVersion
        {
            get { return EditorVersionUtil.MaxVersion; }
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
            Version version;
            string installDirectory;
            if (!EditorLocatorUtil.TryGetEditorInfo(editorVersion, out version, out installDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            HookResolve(version, installDirectory);

            var assemblyList = LoadEditorComponents(version);
            list.AddRange(assemblyList.Select(x => new AssemblyCatalog(x)));
        }

        /// <summary>
        /// Need to hook <see cref="AppDomain.AssemblyResolve" /> so that we can load the editor assemblies from the 
        /// desired location for this AppDomain.
        /// </summary>
        private static void HookResolve(Version version, string installDirectory)
        {
            var dirList = new List<string>();
            dirList.Add(Path.Combine(installDirectory, "PrivateAssemblies"));

            // Before 15.0 all of the editor assemblies were located in the GAC.  Hence no resolve needs to be done
            // because they will be discovered automatically when we load by the qualified name.  Starting in 15.0 
            // though the assemblies are not GAC'd and we need to load from the extension directory. 
            if (version.Major >= 15)
            {
                dirList.Add(Path.Combine(installDirectory, @"CommonExtensions\Microsoft\Editor"));
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                {
                    var assemblyName = new AssemblyName(e.Name);
                    var name = string.Format("{0}.dll", assemblyName.Name);
                    foreach (var dir in dirList)
                    {
                        var fullName = Path.Combine(dir, name);
                        if (File.Exists(fullName))
                        {
                            return Assembly.LoadFrom(fullName);
                        }
                    }

                    return null;
                };
        }

        private static List<Assembly> LoadEditorComponents(Version version)
        {
            var list = new List<Assembly>(EditorComponents.Length);
            foreach (var name in EditorComponents)
            {
                var simpleName = Path.GetFileNameWithoutExtension(name);
                var qualifiedName = string.Format("{0}, Version={1}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", simpleName, version);
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

                list.Add(assembly);
            }

            return list;
        }
    }
}
