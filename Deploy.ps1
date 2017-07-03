param ( [switch]$push = $false, 
        [switch]$fast = $false,
        [switch]$skipTests = $false, 
        [string]$vsDir = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise")

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

function Create-Directory([string]$dir) {
    [IO.Directory]::CreateDirectory($dir) | Out-Null
}

# Handy function for executing a command in powershell and throwing if it 
# fails.  
#
# Use this when the full command is known at script authoring time and 
# doesn't require any dynamic argument build up.  Example:
#
#   Exec-Block { & $msbuild Test.proj }
# 
# Original sample came from: http://jameskovacs.com/2010/02/25/the-exec-problem/
function Exec-Block([scriptblock]$cmd) {
    & $cmd

    # Need to check both of these cases for errors as they represent different items
    # - $?: did the powershell script block throw an error
    # - $lastexitcode: did a windows command executed by the script block end in error
    if ((-not $?) -or ($lastexitcode -ne 0)) {
        throw "Command failed to execute: $cmd"
    } 
}

function Build-Clean([string]$fileName) {
    $name = Split-Path -leaf $fileName
    Exec-Block { & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Release /p:VisualStudioVersion=10.0 $fileName }
    Exec-Block { & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Debug /p:VisualStudioVersion=10.0 $fileName }
}

function Build-Release([string]$fileName, [string]$editorVersion) {
    $name = Split-Path -leaf $fileName
    Exec-Block { & $msbuild /nologo /verbosity:q /p:Configuration=Release /p:VisualStudioVersion=10.0 /p:EditorVersion=$editorVersion $fileName }
}

# Check to see if the given version of Visual Studio is installed
function Test-VSInstall() { 
    param ([string]$version = $(throw "Need a version"))

    if ([IntPtr]::Size -eq 4) { 
        $path = "hklm:\Software\Microsoft\VisualStudio\{0}" -f $version
    } 
    else {
        $path = "hklm:\Software\Wow6432Node\Microsoft\VisualStudio\{0}" -f $version
    }
    $i = Get-ItemProperty $path InstallDir -ea SilentlyContinue | %{ $_.InstallDir }
    return $i -ne $null
}

# Run all of the unit tests
function Test-UnitTests([string]$editorVersion, [string]$vsVersion) { 
    Write-Host -NoNewLine "`tRunning Unit Tests: "
    if ($skipTests) { 
        Write-Host "skipped"
        return
    }

    if (-not (Test-VSInstall $vsVersion)) { 
        Write-Host "skipped (VS missing)"
        return
    }

    $all = "Binaries\Release\EditorUtilsTest\$($editorVersion)\EditorUtils.UnitTest.dll"
    $xunit = Join-Path $PSScriptRoot "Tools\xunit.console.clr4.x86.exe"
    $resultFilePath = Join-Path $deployDir $editorVersion
    $resultFilePath = Join-Path $resultFilePath "xunit.xml"

    foreach ($file in $all) { 
        $name = Split-Path -leaf $file
        & $xunit $file /silent /xml $resultFilePath | Out-Null
        if ((-not $?) -or ($lastexitcode -ne 0)) {
            Write-Host "FAILED!!!"
            & notepad $resultFilePath
        }
        else { 
            Write-Host "passed"
        }
    }
}

# Get the version number of the package that we are deploying 
function Get-Version() { 
    $version = $null;
    foreach ($line in Get-Content "Src\EditorUtils\Constants.cs") {
        if ($line -match 'AssemblyVersion = "([\d.]*)"') {
            $version = $matches[1]
        }
    }

    if ($version -eq $null) {
        throw "Couldn't determine the version from Constants.cs"
    }

    if (-not ($version -match "^\d+\.\d+\.\d+.\d+$")) {
        throw "Version number in unexpected format"
    }

    return $version
}

# Do the NuGet work
function Invoke-NuGet([string]$editorVersion, [string]$version, [string]$suffix) {
    Write-Host "`tCreating NuGet Package"

    $scratchDir = Join-Path $deployDir $editorVersion
    $libDir = Join-Path $scratchDir "lib\net40"
    Create-Directory $scratchDir
    Create-Directory $libDir

    $fileName = "EditorUtils$($suffix)"
    Copy-Item "Binaries\Release\EditorUtils\$($editorVersion)\$($fileName).dll" (Join-Path $libDir "$($fileName).dll")
    Copy-Item "Binaries\Release\EditorUtils\$($editorVersion)\$($fileName).pdb" (Join-Path $libDir "$($fileName).pdb")

    $nuspecFilePath = Join-Path "Data" "EditorUtils$($suffix).nuspec"
    Exec-Block { & $nuget pack $nuspecFilePath -Version $version -BasePath $scratchDir -OutputDirectory $deployDir } | Out-Null

    if ($push) { 
        Write-Host "`tPushing Package"
        $name = "EditorUtils$($suffix).$version.nupkg"
        $packageFile = Join-Path $outputPath $name
        Exec-Block { & $nuget push $packageFile } | Out-Host
    }
}

function Deploy-Version([string]$editorVersion, [string]$vsVersion) { 
    $suffix = $editorVersion.Substring(2)
    Write-Host "Deploying $editorVersion"

    # First clean the projects
    Write-Host "`tCleaning Projects"
    Build-Clean Src\EditorUtils\EditorUtils.csproj
    Build-Clean Test\EditorUtilsTest\EditorUtilsTest.csproj

    # Build all of the relevant projects.  Both the deployment binaries and the 
    # test infrastructure
    Write-Host "`tBuilding Projects"
    Build-Release Src\EditorUtils\EditorUtils.csproj $editorVersion
    Build-Release Test\EditorUtilsTest\EditorUtilsTest.csproj $editorVersion

    Write-Host "`tDetermining Version Number"
    $version = Get-Version

    # Next run the tests
    Test-UnitTests $editorVersion $vsVersion

    # Now do the NuGet work 
    Invoke-NuGet $editorVersion $version $suffix
}

Push-Location $PSScriptRoot
try {

    $msbuild = Join-Path $vsDir "MSBuild\15.0\Bin\msbuild.exe"
    if (-not (Test-Path $msbuild)) {
        Write-Host "Can't find msbuild.exe"
        exit 1
    }

    $deployDir = Join-Path $PSScriptRoot "Binaries\Deploy"

    $nuget = Resolve-Path ".nuget\NuGet.exe"
    if (-not (Test-Path $nuget)) { 
        Write-Host "Can't find NuGet.exe"
        exit 1
    }

    if ($fast) {
        Deploy-Version "Vs2010" "10.0"
    }
    else { 
        Deploy-Version "Vs2010" "10.0"
        Deploy-Version "Vs2012" "11.0" 
        Deploy-Version "Vs2013" "12.0"
        Deploy-Version "Vs2015" "14.0"
        Deploy-Version "Vs2017" "15.0"
    }
}
catch { 
    Write-Host $_.ScriptStackTrace
    Write-Host $_
    Write-Host "Failed"
    exit 1
}
finally {
    Pop-Location
}


