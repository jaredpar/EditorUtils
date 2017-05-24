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
        public static readonly EditorVersion MinVersion = EditorVersion.Vs2010;
#elif VS2012
        public static readonly EditorVersion MinVersion = EditorVersion.Vs2012;
#elif VS2013
        public static readonly EditorVersion MinVersion = EditorVersion.Vs2013;
#elif VS2015
        public static readonly EditorVersion MinVersion = EditorVersion.Vs2015;
#elif VS2017
        public static readonly EditorVersion MinVersion = EditorVersion.Vs2017;
#else
#error Unexpected build combination 
#endif

        public static IEnumerable<EditorVersion> All => Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>();

        public static EditorVersion MaxVersion => All.OrderByDescending(x => GetMajorVersionNumber(x)).First();

        public static int GetMajorVersionNumber(EditorVersion version)
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

        public static string GetShortVersionString(EditorVersion version)
        {
            var number = GetMajorVersionNumber(version);
            return string.Format("{0}.0", number);
        }
    }
}
