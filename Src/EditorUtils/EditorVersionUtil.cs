using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorUtils
{
    public static class EditorVersionUtil
    {
        /// <summary>
        /// The version of Visual Studio this editor instance is compiled against.
        /// </summary>
#if VS2010 
        public static readonly EditorVersion TargetVersion = EditorVersion.Vs2010;
#elif VS2012
        public static readonly EditorVersion TargetVersion = EditorVersion.Vs2012;
#elif VS2013
        public static readonly EditorVersion TargetVersion = EditorVersion.Vs2013;
#elif VS2015
        public static readonly EditorVersion TargetVersion = EditorVersion.Vs2015;
#elif VS2017
        public static readonly EditorVersion TargetVersion = EditorVersion.Vs2017;
#else
#error Unexpected build combination 
#endif

        public static IEnumerable<EditorVersion> All => Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderBy(x => GetMajorVersionNumber(x));

        public static IEnumerable<EditorVersion> Supported => All.Where(IsSupported);

        public static EditorVersion MaxVersion => All.OrderByDescending(x => GetMajorVersionNumber(x)).First();

        /// <summary>
        /// Whether or not this EditorVersion is supported by this particular compilation.  For instance 
        /// the VS2012 version of this binary may not support VS2010.
        /// </summary>
        public static bool IsSupported(EditorVersion version) 
        {
            return GetMajorVersionNumber(TargetVersion) <= GetMajorVersionNumber(version);
        }

        public static EditorVersion GetEditorVersion(int majorVersion)
        {
            switch (majorVersion)
            {
                case 10: return EditorVersion.Vs2010;
                case 11: return EditorVersion.Vs2012;
                case 12: return EditorVersion.Vs2013;
                case 14: return EditorVersion.Vs2015;
                case 15: return EditorVersion.Vs2017;
                default: throw new Exception(string.Format("Unexpected major version value {0}", majorVersion));
            }
        }

        public static int GetMajorVersionNumber(EditorVersion version)
        {
            switch (version)
            {
                case EditorVersion.Vs2010: return 10;
                case EditorVersion.Vs2012: return 11;
                case EditorVersion.Vs2013: return 12;
                case EditorVersion.Vs2015: return 14;
                case EditorVersion.Vs2017: return 15;
                default: throw new Exception(string.Format("Unexpected enum value {0}", version));
            }
        }

        public static string GetShortVersionString(EditorVersion version)
        {
            var number = GetMajorVersionNumber(version);
            return string.Format("{0}.0", number);
        }
    }
}
