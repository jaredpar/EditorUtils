
namespace EditorUtils
{
    public static class Constants
    {
        /// <summary>
        /// The version of the assembly.  This must be changed every time a new version of the utility 
        /// library is published to MEF.  We absolutely depend on this being unique for every instance
        /// which is available in the wild.
        ///
        /// The uniqueness of this name is necessary because we include it in the ContractName we export
        /// for MEF components
        /// </summary>
#if DEBUG
        internal const string AssemblyVersion = "99.0.0.0";
#else
        internal const string AssemblyVersion = "1.1.0.0";
#endif

        /// <summary>
        /// Standard delay for asynchronous taggers
        /// </summary>
        public const int DefaultAsyncDelay = 100;
    }
}
