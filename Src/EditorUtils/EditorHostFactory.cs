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
        internal static string[] CoreEditorComponents =
            new[]
            {
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Text.Internal.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
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
            var version = AppendEditorCatalog(_composablePartCatalogList, editorVersion);
            _exportProviderList.Add(new UndoExportProvider());

            if (version.Major >= 15)
            {
                _exportProviderList.Add(new JoinableTaskContextExportProvider());
                _composablePartCatalogList.Add(new TypeCatalog(GetEditorType(
                    "Microsoft.VisualStudio.Editor.Implementation",
                    version,
                    "Microsoft.VisualStudio.Editor.Implementation.LoggingServiceInternal")));

#if VS2017
                _composablePartCatalogList.Add(new TypeCatalog(typeof(SimpleWaitIndicator)));
#endif
            }
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
        private static Version AppendEditorCatalog(List<ComposablePartCatalog> list, EditorVersion? editorVersion)
        {
            Version vsVersion;
            string vsInstallDirectory;
            if (!EditorLocatorUtil.TryGetEditorInfo(editorVersion, out vsVersion, out vsInstallDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            var version = new Version(vsVersion.Major, 0);
            HookResolve(version, vsInstallDirectory);

            var assemblyList = GetEditorAssemblies(version);
            list.AddRange(assemblyList.Select(x => new AssemblyCatalog(x)));
            return version;
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

        private static List<Assembly> GetEditorAssemblies(Version version)
        {
            var list = new List<Assembly>(CoreEditorComponents.Length);
            foreach (var name in CoreEditorComponents)
            {
                var simpleName = Path.GetFileNameWithoutExtension(name);
                var assembly = GetEditorAssembly(simpleName, version);
                list.Add(assembly);
            }

            return list;
        }

        private static Assembly GetEditorAssembly(string assemblyName, Version version)
        {
            var qualifiedName = string.Format("{0}, Version={1}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", assemblyName, version);
            try
            {
                return Assembly.Load(qualifiedName);
            }
            catch (Exception e)
            {
                var msg = string.Format("Unable to load editor dependency {0}", assemblyName);
                throw new Exception(msg, e);
            }
        }

        private static Type GetEditorType(string assemblyName, Version version, string typeName)
        {
            var assembly = GetEditorAssembly(assemblyName, version);
            try
            {
                return assembly.GetType(typeName);
            }
            catch (Exception e)
            {
                var msg = string.Format("Unable to load editor type {0}", typeName);
                throw new Exception(msg, e);
            }
        }

    }
}
