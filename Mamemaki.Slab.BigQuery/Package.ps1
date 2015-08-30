$spec = "Mamemaki.Slab.BigQuery.nuspec"
$proj = "Mamemaki.Slab.BigQuery.csproj"

Function Update-Version($file) {
    $ver = (Get-Item $file).VersionInfo.FileVersion
    $repl = "<version>$ver</version>"
    (Get-Content $spec) | 
        Foreach-Object { $_ -replace "<version>(.*)</version>", $repl } | 
        Set-Content $spec
    return $ver
}

Function Publish-Package($ver) {
	Write-Output "Publish-Package $ver"
    $file = "Mamemaki.Slab.BigQuery.$ver.nupkg"
    nuget pack $proj -IncludeReferencedProjects -Prop Configuration=Release
    nuget push $file
    return $LastExitCode
}

$version = Update-Version "bin\Release\Mamemaki.Slab.BigQuery.dll"
Write-Output "version: $version"
$success = Publish-Package $version

Read-Host -Prompt "Press Enter to continue"
