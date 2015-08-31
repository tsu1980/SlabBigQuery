$projFile = "Mamemaki.Slab.BigQuery\Mamemaki.Slab.BigQuery.csproj"
$moduleFile = "Mamemaki.Slab.BigQuery\bin\Release\Mamemaki.Slab.BigQuery.dll"


Function Build-Module()
{
	$msbuildEXE = "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
	Write-Output "Rebuild module.."
	& "$msbuildEXE" $projFile /p:Configuration=Release /t:rebuild /v:quiet /nologo
	if ($LASTEXITCODE -ne 0)
	{
		throw "msbuild returned error code '$LASTEXITCODE'"
	}
	Write-Output "Rebuild completed successfully"
}

Function Publish-NugetPackage($ver)
{
	Write-Output "Publishing Nuget package start.."
    $file = "Mamemaki.Slab.BigQuery.$ver.nupkg"
    nuget pack $projFile -IncludeReferencedProjects -Prop Configuration=Release
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget pack returned error code '$LASTEXITCODE'"
	}
    #nuget push $file
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget push returned error code '$LASTEXITCODE'"
	}
	Write-Output "Publishing Nuget package completed successfully"
}

Build-Module

$version = (Get-Item $moduleFile).VersionInfo.FileVersion
Write-Output "Version: $version"
Publish-NugetPackage $version

Read-Host -Prompt "Press Enter to exit"
