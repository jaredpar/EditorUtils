param($installPath, $toolsPath, $package, $project)

# Need to add the EditorUtil.dll into the source.extension.vsixmanifest file.  First
# step is to find the file itself.  It may or may not be present in the project we
# are installing into.  It's perfectly legal for a project to not have a manifest
# file if it's just a utility project itself.  Only the actual VSIX project will
# have one.
Function Add-MefReference() {
    $manifestFilePath = Get-ManifestFilePath $script:project

    # No manifest file isn't a problem
    if ($manifestFilePath -eq $null) {
        Write-Host "No source.extension.vsixmanifest found"
        return
    }

    Write-Host "Found manifest: $manifestFilePath"
    $x = [xml](gc $manifestFilePath)
    $found = $false
    foreach ($item in $x.Vsix.Content.MefComponent) {
        if ($item -eq "EditorUtils.dll") {
            $found = $true
        }
    }

    if (-not $found) {
        Write-Host "Adding MefComponent reference to EditorUtils.dll"
        $node = $x.Vsix.Content.ChildNodes.Item(0).Clone()
        $node.set_InnerText("EditorUtils.dll")
        $x.Vsix.Content.AppendChild($node)
        $x.Save($manifestFilePath)
    }
}

Add-MefReference

