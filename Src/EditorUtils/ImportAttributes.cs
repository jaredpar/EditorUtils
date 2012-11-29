using System;
using System.ComponentModel.Composition;

namespace EditorUtils
{
    /// <summary>
    /// Attribute for simple importing of EditorUtils components
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class EditorUtilsImportAttribute : ImportAttribute
    {
        public EditorUtilsImportAttribute()
            : base(Constants.ContractName)
        {

        }
    }

    /// Attribute for simple importing of EditorUtils components
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class EditorUtilsImportManyAttribute : ImportManyAttribute
    {
        public EditorUtilsImportManyAttribute()
            : base(Constants.ContractName)
        {

        }
    }

}
