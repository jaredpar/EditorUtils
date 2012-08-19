param($installPath, $toolsPath, $package, $project)

# Remove the EditorUtils reference from the source.extension.vsixmanifest file. 
Function Remove-MefReference() {
    $manifestFilePath = Get-ManifestFilePath $script:project

    # No manifest file isn't a problem
    if ($manifestFilePath -eq $null) {
        Write-Host "No source.extension.vsixmanifest found"
        return
    }

    # The vsixmanifest file uses an xml namespace.  Need to build up a namespace 
    # table here for our xpath query
    $ns = @{ 
        vsns = "http://schemas.microsoft.com/developer/vsx-schema/2010"
    }

    $x = [xml](gc $manifestFilePath) 
    $result = $x | Select-Xml -xpath "//vsns:MefComponent[text()=`"EditorUtils.dll`"]" -namespace $ns
    if ($result -ne $null) {
        Write-Host "Removing vsixmanifest reference to EditorUtils"
        $node = $result.Node
        $node.ParentNode.RemoveChild($node)
        $x.Save($manifestFilePath)
    }
}

Remove-MefReference
