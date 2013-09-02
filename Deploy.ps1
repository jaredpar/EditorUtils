param ( [switch]$push = $false, 
        [switch]$fast = $false)
$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
pushd $scriptPath

function test-return() {
    if ($LASTEXITCODE -ne 0) {
        write-error "Command failed with code $LASTEXITCODE"
    }
}

function build-clean() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "`t$name"
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Release /p:VisualStudioVersion=10.0 $fileName
    test-return
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Debug /p:VisualStudioVersion=10.0 $fileName
    test-return
}

function build-release() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "`t$name"
    & $msbuild /nologo /verbosity:q /p:Configuration=Release /p:VisualStudioVersion=10.0 $fileName
    test-return
}

# Run all of the unit tests
function test-unittests() { 
    $all = "Test\EditorUtilsTest\bin\Release\EditorUtils.UnitTest.dll"
    $xunit = join-path $scriptPath "Tools\xunit.console.clr4.x86.exe"

    write-host "Running Unit Tests"
    foreach ($file in $all) { 
        $name = split-path -leaf $file
        write-host -NoNewLine ("`t{0}: " -f $name)
        $output = & $xunit $file /silent
        test-return
        $last = $output[$output.Count - 1] 
        write-host $last
    }
}

# Get the version number of the package that we are deploying 
function get-version() { 
    write-host "Testing Version Numbers"
    $version = $null;
    foreach ($line in gc "Src\EditorUtils\Constants.cs") {
        if ($line -match 'AssemblyVersion = "([\d.]*)"') {
            $version = $matches[1]
        }
    }

    if ($version -eq $null) {
        write-error "Couldn't determine the version from Constants.cs"
        return 
    }

    if (-not ($version -match "^\d+\.\d+\.\d+.\d+$")) {
        write-error "Version number in unexpected format"
    }

    return $version
}


# Do the NuGet work
function invoke-nuget() {
    param ([string]$version = $(throw "Need a version number"))

    write-host "NuGet Package"
    $docPath = [Environment]::GetFolderPath("MyDocuments")
    $target = Join-Path $docPath "NugetPackages"
    if (-not (Test-Path $target)) {
        mkdir $target | out-null
    }

    write-host "`tPacking to $docPath"

    & $nuget pack Src\EditorUtils\EditorUtils.csproj -Symbols -OutputDirectory $target -Prop Configuration=Release | %{ write-host "`t`t$_" }
    test-return

    if ($script:push) { 
        write-host "`tPushing Package"
        $name = "EditorUtils.{0}.nupkg" -f $version
        $packageFile = join-path $target $name
        & $nuget push $packageFile  | %{ write-host "`t`t$_" }
        test-return
    }
}

$msbuild = join-path ${env:SystemRoot} "microsoft.net\framework\v4.0.30319\msbuild.exe"
if (-not (test-path $msbuild)) {
    write-error "Can't find msbuild.exe"
}

# Several of the projects involved use NuGet and the resulting .csproj files 
# rely on the SolutionDir MSBuild property being set.  Hence we set the appropriate
# environment variable for build
${env:SolutionDir} = $scriptPath

$nuget = resolve-path ".nuget\NuGet.exe"
if (-not (test-path $nuget)) { 
    write-error "Can't find NuGet.exe"
}

# First step is to clean out all of the projects 
if (-not $fast) { 
    write-host "Cleaning Projects"
    build-clean Src\EditorUtils\EditorUtils.csproj
    build-clean Test\EditorUtilsTest\EditorUtilsTest.csproj
}

# Build all of the relevant projects.  Both the deployment binaries and the 
# test infrastructure
write-host "Building Projects"
build-release Src\EditorUtils\EditorUtils.csproj
build-release Test\EditorUtilsTest\EditorUtilsTest.csproj

write-host "`tDetermining Version Number"
$version = get-version

# Next run the tests
test-unittests

# Now do the NuGet work 
invoke-nuget $version

rm env:\SolutionDir

popd

