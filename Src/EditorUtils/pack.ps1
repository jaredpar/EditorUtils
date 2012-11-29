$doc = [Environment]::GetFolderPath("MyDocuments")
$target = Join-Path $doc "NugetPackages"
if (-not (Test-Path $target)) {
    mkdir $target
}

& ..\.nuget\NuGet.exe pack EditorUtils.csproj -Symbols -OutputDirectory $target -Prop Configuration=Release

