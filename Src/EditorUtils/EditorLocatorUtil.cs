using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EditorUtils
{
    /// <summary>
    /// Utility for locating instances of Visual Studio on the machine. 
    /// </summary>
    internal static class EditorLocatorUtil
    {
        /// <summary>
        /// A list of key names for versions of Visual Studio which have the editor components 
        /// necessary to create an EditorHost instance.  Listed in preference order
        /// </summary>
        internal static readonly string[] VisualStudioSkuKeyNames =
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


        internal static bool TryGetEditorInfo(EditorVersion? editorVersion, out Version version, out string installDirectory)
        {
            if (editorVersion.HasValue)
            {
                return TryGetEditorInfo(editorVersion.Value, out version, out installDirectory);
            }

            foreach (var e in EditorVersionUtil.All.OrderBy(x => EditorVersionUtil.GetMajorVersionNumber(x)))
            {
                if (TryGetEditorInfo(e, out version, out installDirectory))
                {
                    return true;
                }
            }

            version = default(Version);
            installDirectory = null;
            return false;
        }

        internal static bool TryGetEditorInfo(EditorVersion editorVersion, out Version version, out string installDirectory)
        {
            var majorVersion = EditorVersionUtil.GetMajorVersionNumber(editorVersion);
            return majorVersion < 15
                ? TryGetEditorInfoLegacy(majorVersion, out version, out installDirectory)
                : TryGetEditorInfoWillow(majorVersion, out version, out installDirectory);
        }

        private static bool TryGetEditorInfoLegacy(int majorVersion, out Version version, out string installDirectory)
        {
            if (TryGetInstallDirectoryLegacy(majorVersion, out installDirectory))
            {
                version = new Version(majorVersion, 0);
                return true;
            }

            version = default(Version);
            installDirectory = null;
            return false;
        }

        /// <summary>
        /// Try and get the installation directory for the specified SKU of Visual Studio.  This 
        /// will fail if the specified version of Visual Studio isn't installed.  Only works on 
        /// pre-willow VS installations (< 15.0).  
        /// </summary>
        private static bool TryGetInstallDirectoryLegacy(int majorVersion, out string installDirectory)
        {
            foreach (var skuKeyName in VisualStudioSkuKeyNames)
            {
                if (TryGetInstallDirectoryLegacy(majorVersion, skuKeyName, out installDirectory))
                {
                    return true;
                }
            }

            installDirectory = null;
            return false;
        }

        private static bool TryGetInstallDirectoryLegacy(int majorVersion, string skuKeyName,out string installDirectory)
        {
            try
            {
                var subKeyPath = String.Format(@"Software\Microsoft\{0}\{1}.0", skuKeyName, majorVersion);
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
        /// Get the first Willow VS installation with the specified major version.
        /// </summary>
        private static bool TryGetEditorInfoWillow(int majorVersion, out Version version, out string directory)
        {
            Debug.Assert(majorVersion >= 15);

            var setup = new SetupConfiguration();
            var e = setup.EnumAllInstances();
            var array = new ISetupInstance[] { null };
            do
            {
                var found = 0;
                e.Next(array.Length, array, out found);
                if (found == 0)
                {
                    break;
                }

                var instance = array[0];
                if (Version.TryParse(instance.GetInstallationVersion(), out version) &&
                    version.Major == majorVersion)
                {
                    directory = Path.Combine(instance.GetInstallationPath(), @"Common7\IDE");
                    return true;
                }
            }
            while (true);

            directory = null;
            version = default(Version);
            return false;
        }
    }
}
