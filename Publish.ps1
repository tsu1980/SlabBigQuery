[CmdletBinding()]
Param()

$projFile = "Mamemaki.Slab.BigQuery\Mamemaki.Slab.BigQuery.csproj"
$moduleFile = "Mamemaki.Slab.BigQuery\bin\Release\Mamemaki.Slab.BigQuery.dll"
$githubUrl = "https://github.com/tsu1980/SlabBigQuery"
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

Function Push-GitHub()
{
	Write-Output "Push to GitHub.."

	$stdout = & git push $githubUrl --tags
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

#Rebuild project
Build-Module

#Get version
$version = (Get-Item $moduleFile).VersionInfo.FileVersion
Write-Output "version: $version"

#Get changelog for the publish version
$changelog = Get-ChangeLog $version
Write-Output "ChangeLog for v${version}:"
Write-Output $changelog

#Check tag corespond to the version exists in local and GitHub
#NOTE: You need tagging and push it to GitHub before start
Check-Tag $version
Push-GitHub
#Check-GitHubTag $version

#Create GitHub release
Create-GitHubRelease $version $changelog

#Publish to Nuget
Write-Output "Version: $version"
Publish-NugetPackage $version

Read-Host -Prompt "Press Enter to exit"
