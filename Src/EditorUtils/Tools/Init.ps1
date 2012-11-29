param($installPath, $toolsPath, $package)

# Using -force here to ensure that new versions of the EditorUtils package
# will force new versions of the psm1 script into the console.  Without 
# -force the same version will stay resident in memory until Visual Studio
# is restarted
Import-Module -force (Join-Path $toolsPath "EditorUtils.psm1")
