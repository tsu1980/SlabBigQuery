[CmdletBinding()]
Param()

$current = (Get-Location).Path
$releasesDir = Join-Path -Path $current -ChildPath "Releases"
$projDir = Join-Path -Path $current -ChildPath "Mamemaki.Slab.BigQuery"
$projFile = Join-Path -Path $projDir -ChildPath "Mamemaki.Slab.BigQuery.csproj"
$targetDir = Join-Path -Path $projDir -ChildPath "bin\Release"
$targetFile = Join-Path -Path $targetDir -ChildPath "Mamemaki.Slab.BigQuery.dll"
$nuspecTempFile = Join-Path -Path $projDir -ChildPath "Mamemaki.Slab.BigQuery.nuspec.template.xml"
$nuspecFile = Join-Path -Path $projDir -ChildPath "Mamemaki.Slab.BigQuery.nuspec"
$slabSvcProjDir = Join-Path -Path $current -ChildPath "Mamemaki.Slab.BigQuery.Service"
$githubUrl = "https://github.com/tsu1980/SlabBigQuery.git"
$githubApiUrl = "https://api.github.com/repos/tsu1980/SlabBigQuery"

if (!(Test-Path Env:\GITHUB_OAUTH_TOKEN)) {
	throw "GITHUB_OAUTH_TOKEN is must be set"
}

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

Function Create-OOPPackage($ver, $destDir)
{
	$SLABSVC_URL = "https://slab.codeplex.com/downloads/get/881780"
	$SLABSVC_ZIPFILE = "$env:temp\SemanticLogging-svc.2.0.1406.1.zip"
	$SLABSVC_EXTRACTDIR = "$env:temp\SemanticLogging-svc.2.0.1406.1"

	# Download the slab-svc if not exist
	If (!(Test-Path $SLABSVC_ZIPFILE)) {
		Invoke-WebRequest -uri $SLABSVC_URL -OutFile $SLABSVC_ZIPFILE
		Unblock-File $SLABSVC_ZIPFILE
	}

	# Download the slab-svc packages if not exist
	If (!(Test-Path $SLABSVC_EXTRACTDIR)) {
		Expand-Archive -Path $SLABOOPSERVICEZIP -DestinationPath $SLABSVC_EXTRACTDIR

		# Remove the ReadLine line in the uppacked script.
		$INSTALLPACKAGESFILENAME = $SLABSVC_EXTRACTDIR + "\install-packages.ps1"
		(Get-Content $INSTALLPACKAGESFILENAME) |  Where-Object {$_ -notlike "*ReadLine*"} | Set-Content $INSTALLPACKAGESFILENAME

		# Run install-packages.ps1
		.($INSTALLPACKAGESFILENAME) -autoAcceptTerms
	}

	# Copy slab-svc to work dir
	$SLABSVC_WORKDIR = Join-Path -Path $destDir -ChildPath "SLABSVC-WORK"
	If (Test-Path $SLABSVC_WORKDIR) {
		Remove-Item $SLABSVC_WORKDIR -Recurse
	}
	New-Item -ItemType directory $SLABSVC_WORKDIR -ErrorAction SilentlyContinue | Out-Null
	Copy-Item $SLABSVC_EXTRACTDIR\* -Destination $SLABSVC_WORKDIR

	# Copy BigquerySink modules
	# Overwrite Newtonsoft.Json.dll to BigQuery's one because its higher version,
	# not overwrite anything else
	Remove-Item $SLABSVC_WORKDIR\Newtonsoft.Json.dll
	Copy-Item $targetDir\* -Destination $SLABSVC_WORKDIR -Exclude Microsoft.Practices.EnterpriseLibrary.SemanticLogging.*

	#Rename SemanticLogging-svc.xml
	Rename-Item -Path $SLABSVC_WORKDIR\SemanticLogging-svc.xml -NewName SemanticLogging-svc.default.xml

	# Adding dependencies of Mamemaki.Slab.Bigquery.dll to SemanticLogging-svc.exe.config
	$SLABSVCCONFIG_FILENAME = "$SLABSVC_WORKDIR\SemanticLogging-svc.exe.config"
	$txtSlabBigqueryDependencies = '<dependentAssembly>
		<assemblyIdentity name="System.Net.Http.Extensions" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
		<bindingRedirect oldVersion="0.0.0.0-2.2.29.0" newVersion="2.2.29.0" />
	  </dependentAssembly>
	  <dependentAssembly>
		<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
		<bindingRedirect oldVersion="0.0.0.0-7.0.0.0" newVersion="7.0.0.0" />
	  </dependentAssembly>
	  <dependentAssembly>
		<assemblyIdentity name="System.Net.Http.Primitives" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
		<bindingRedirect oldVersion="0.0.0.0-4.2.29.0" newVersion="4.2.29.0" />
	  </dependentAssembly>'
	$xml = [xml](Get-Content $SLABSVCCONFIG_FILENAME)
	$xmlFrag = $xml.CreateDocumentFragment()
	$xmlFrag.InnerXml = $txtSlabBigqueryDependencies
	$xml.configuration.runtime.assemblyBinding.AppendChild($xmlFrag) | Out-Null
	$xml.Save($SLABSVCCONFIG_FILENAME)
	# Remove xmlns=""
	(Get-Content $SLABSVCCONFIG_FILENAME).Replace(' xmlns=""', '') | Set-Content $SLABSVCCONFIG_FILENAME

	# Create final zip file
	$SLABSVC_RELEASE_ZIPNAME = Join-Path -Path $destDir -ChildPath "SlabBigquery-svc.zip"
	Remove-Item $SLABSVC_WORKDIR\*.pdb
	Remove-Item $SLABSVC_WORKDIR\install-packages.ps1
	Compress-Archive -Path $SLABSVC_WORKDIR\* -DestinationPath $SLABSVC_RELEASE_ZIPNAME -Force
	return $SLABSVC_RELEASE_ZIPNAME
}

Function Get-ChangeLog($ver)
{
	$changeLogFile = "CHANGELOG.md"

	$result = ""
	$readStart = $false
	$content = Get-Content $changeLogFile
	foreach ($line in $content) {
	#foreach ($line in [System.IO.File]::ReadLines($changeLogFile)) {
		if ($line -eq "") {
			continue
		}
		if ($line.StartsWith("# v$ver")) {
			$readStart = $true
		} elseif ($line.StartsWith("# ")) {
			break
		} else {
			$result += $line + "`n"
		}
	}
	if (!$readStart)
	{
		throw "Failed to get changelog content. version:$ver"
	}

	return $result
}

Function Check-Tag($ver)
{
	Write-Output "Check tag.."
	$stdout = & git tag --list v$ver 2>&1
	Write-Verbose "stdout: $stdout"
	if ($stdout -ne "v$ver")
	{
		throw "Version tag not set. version:$ver"
	}
}

Function Check-Origin()
{
	Write-Output "Check origin.."
	$stdout = & git remote -v
	Write-Verbose "stdout: $stdout"
	if (!$stdout.Contains("origin`t$githubUrl (fetch)")) {
		throw "origin not set or bad url. version:$stdout"
	}
}

Function Push-GitHub()
{
	Write-Output "Push to GitHub.."

	$stdout = & git push origin master --tags
	Write-Verbose "stdout: $stdout"
	if ($LASTEXITCODE -ne 0)
	{
		throw "git returned error code '$LASTEXITCODE'"
	}

	Write-Output "Push to GitHub completed successfully"
}

Function Check-GitHubTag($ver)
{
	Write-Output "Check GitHub tag.."

	$stdout = & git ls-remote $githubUrl
	$stdout | ForEach-Object {
		Write-Verbose $_
		if ($_.Contains("refs/tags/v$ver")) {
			Return
		}
	}

	throw "Version tag not set in GitHub. version:$ver"
}

Function Upload-ReleaseAsset($uploadUrl, $assetsUrl, $fileName, $fileData, $overwrite = $true)
{
	#List assets and delete asset if already exist
	$res = Invoke-RestMethod -Uri $assetsUrl
	foreach ($item in $res) {
		if ($item.name -eq $fileName) {
			Write-Output "Delete file($fileName)"
			$url = $item.url
			$url += "?access_token=${Env:\GITHUB_OAUTH_TOKEN}"
			$res = Invoke-RestMethod -Uri $url -Method Delete
			Write-Verbose $res
			break
		}
	}

	#Upload asset file
	$url = $uploadUrl + "?name=$fileName"
	$url += "&label=Out-of-process service for SlabBigQuery."
	$url += "&access_token=${Env:\GITHUB_OAUTH_TOKEN}"
	Invoke-WebRequest -Uri $url -Method "POST" -Body $fileData -ContentType "application/zip"
}

Function Create-GitHubRelease($ver, $changelog, $slabSvcPackageFile = $null)
{
	Write-Output "Create GitHub release.."

	# Create GitHub release
	$releaseId = $null
	try
	{
		$url = "$githubApiUrl/releases/tags/v${ver}?access_token=${Env:\GITHUB_OAUTH_TOKEN}"
		$res = Invoke-RestMethod -Uri $url
		$releaseId = $res.id
		Write-Output "GitHub release('v$ver') is already exists"
    } catch {
        $res = $_.Exception.Response
		Write-Verbose "${res.StatusCode.value__} ${res.StatusCode}"
		if ($res.StatusCode.value__ -eq 404) {
		} else {
			throw
		}
    }

	$postData = @{
		tag_name = "v${ver}";
		target_commitish = "master";
		name = "v${ver}";
		body = $changelog;
		draft = $false;
		prerelease = $false;
	}
	$json = (ConvertTo-Json $postData -Compress)
	Write-Verbose $json
	if ($releaseId -ne $null) {
		Write-Verbose "releaseId = $releaseId"
		$method = "PATCH"
		$url = "$githubApiUrl/releases/${releaseId}?access_token=${Env:\GITHUB_OAUTH_TOKEN}"
	} else {
		$method = "POST"
		$url = "$githubApiUrl/releases?access_token=${Env:\GITHUB_OAUTH_TOKEN}"
	}
	$res = Invoke-RestMethod -Uri $url -Method $method -Body $json
	Write-Verbose $res
	$uploadUrl = $res.upload_url.Replace("{?name}", "")
	$assetsUrl = $res.assets_url
	Write-Verbose "upload_url = $uploadUrl"

	if ($slabSvcPackageFile) {
		#Upload SLABSVC package
		$slabSvcFileName = "SlabBigquery-svc.zip"
		$fileData = [IO.File]::ReadAllBytes($slabSvcPackageFile)
		Upload-ReleaseAsset $uploadUrl $assetsUrl $slabSvcFileName $fileData
	}

	Write-Output "Create GitHub release completed successfully"
}

Function Publish-NugetPackage($ver, $destDir, $changelog)
{
	Write-Output "Publishing Nuget package start.."

	#Generate nuspec from template
	Copy-Item $nuspecTempFile -Destination $nuspecFile
	$xml = [xml](Get-Content $nuspecFile)
	$xml.package.metadata.releaseNotes = $changelog
	$xml.Save($nuspecFile)

    $nupkgFile = Join-Path -Path $destDir -ChildPath "Mamemaki.Slab.BigQuery.$ver.nupkg"
    nuget pack $projFile -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory $destDir
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget pack returned error code '$LASTEXITCODE'"
	}
    nuget push $nupkgFile
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget push returned error code '$LASTEXITCODE'"
	}
	Write-Output "Publishing Nuget package completed successfully"
}

Function Publish-OOPSvcNugetPackage($ver, $releaseDir, $changelog)
{
	Write-Output "Publishing Out-of-process service Nuget package start.."

	# Prepare work dir
	$nugetWorkDir = Join-Path -Path $releaseDir -ChildPath "SLABSVC-NUGET-WORK"
	If (Test-Path $nugetWorkDir) {
		Remove-Item $nugetWorkDir -Recurse
	}
	New-Item -ItemType directory $nugetWorkDir -ErrorAction SilentlyContinue | Out-Null
	Copy-Item $slabSvcProjDir/* -Destination $nugetWorkDir
	Copy-Item $slabSvcProjDir/tools/* -Destination $nugetWorkDir/tools

	#Generate nuspec from template
	$nuspecFile = Join-Path -Path $nugetWorkDir -ChildPath "Mamemaki.Slab.BigQuery.Service.nuspec"
	$xml = [xml](Get-Content $nuspecFile)
	$xml.package.metadata.version = $ver
	$xml.package.metadata.releaseNotes = $changelog
	$xml.Save($nuspecFile)

	#Copy content files
	$contentDir = Join-Path -Path $nugetWorkDir -ChildPath "content"
	$slabSvcDir = Join-Path -Path $contentDir -ChildPath "SlabSvc"
	New-Item -ItemType directory $slabSvcDir -ErrorAction SilentlyContinue | Out-Null
	Copy-Item $releaseDir\SlabBigquery-svc.zip -Destination $slabSvcDir
	Copy-Item $releaseDir\SLABSVC-WORK\BigQuerySinkElement.xsd -Destination $slabSvcDir
	Copy-Item $releaseDir\SLABSVC-WORK\SemanticLogging-svc.xsd -Destination $slabSvcDir

    $nupkgFile = Join-Path -Path $releaseDir -ChildPath "Mamemaki.Slab.BigQuery.Service.$ver.nupkg"
    nuget pack $nuspecFile -OutputDirectory $releaseDir
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget pack returned error code '$LASTEXITCODE'"
	}
    nuget push $nupkgFile
	if ($LASTEXITCODE -ne 0)
	{
		throw "nuget push returned error code '$LASTEXITCODE'"
	}
	Write-Output "Publishing Out-of-process service Nuget package completed successfully"
}

#Rebuild project
Build-Module

#Get version
$version = (Get-Item $targetFile).VersionInfo.FileVersion
Write-Output "version: $version"

#Create release folder
$releaseDir = Join-Path -Path $releasesDir -ChildPath "v$version"
New-Item -ItemType directory $releaseDir -ErrorAction SilentlyContinue | Out-Null

#Create Out-of-process service package
$slabSvcZipFile = Create-OOPPackage $version $releaseDir

#Get changelog for the publish version
$changelog = Get-ChangeLog $version
Write-Output "ChangeLog for v${version}:"
Write-Output $changelog

#Check tag corespond to the version exists in local and GitHub
#NOTE: You need tagging and push it to GitHub before start
Check-Tag $version
Check-Origin
Push-GitHub
#Check-GitHubTag $version

#Create GitHub release
Create-GitHubRelease $version $changelog $slabSvcZipFile

#Publish to Nuget
Write-Output "Version: $version"
Publish-NugetPackage $version $releaseDir $changelog
Publish-OOPSvcNugetPackage $version $releaseDir $changelog

Read-Host -Prompt "Press Enter to exit"
