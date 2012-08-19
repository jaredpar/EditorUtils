
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
        internal const string AssemblyVersion = "1.0.0.7";

        /// <summary>
        /// Standard delay for asynchronous taggers
        /// </summary>
        public const int DefaultAsyncDelay = 100;

        /// <summary>
        /// The contract name used for every MEF export in the system.  This is a versioned assembly which
        /// can appear in the same catalog as other instances of itself at different versions.  The 
        /// contract name is used to guarantee we don't satsify contracts across versions.
        /// 
        /// By default MEF only usse the non-assembly qualified name to match Import / Export pairs. So
        /// in a versioned world it will incorrectly match types from different versions of the DLL.  The 
        /// ContractName allows us to add versioning information into the Export / Import values
        /// </summary>
        public const string ContractName = "EditorUtils:" + AssemblyVersion;
    }
}
