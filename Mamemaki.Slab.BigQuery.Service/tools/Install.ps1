param($installPath, $toolsPath, $package, $project)

$slabSvcFolderItem = $project.ProjectItems.Item("SlabSvc")
$slabSvcItem = $slabSvcFolderItem.ProjectItems.Item("SlabBigquery-svc.zip")

# set 'Copy To Output Directory' to 'Copy if newer'
$copyToOutput = $slabSvcItem.Properties.Item("CopyToOutputDirectory")
$copyToOutput.Value = 2

# set 'Build Action' to 'Content'
$buildAction = $slabSvcItem.Properties.Item("BuildAction")
$buildAction.Value = 2
