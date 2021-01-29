$inceptionYear = "2016"
$currentYear = "2020"
$newYear = (Get-Date).year

$currentHeader = "Copyright (C) $inceptionYear-$currentYear SonarSource SA"
Write-Host $currentHeader
$newHeader = "Copyright (C) $inceptionYear-$newYear SonarSource SA"
Write-Host $newHeader

Get-ChildItem -Path src\*.cs -Recurse -Exclude *.Designer.cs,*AssemblyAttributes.cs -Force | 
	foreach {
		Write-Host $_.FullName
		(Get-Content $_).Replace($currentHeader, $newHeader) | Set-Content $_ -Encoding utf8NoBOM
	}

Get-ChildItem -Path tests\*.cs -Recurse -Exclude *AssemblyAttributes.cs -Force | 
	foreach {
		Write-Host $_.FullName
		(Get-Content $_).Replace($currentHeader, $newHeader) | Set-Content $_ -Encoding utf8NoBOM
	}
