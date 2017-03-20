Push-Location ..

$instanceName = "rcp-api"

Write-Output "Checking if application is running"
$existingProcess = docker ps -a | findstr $instanceName
IF (![string]::IsNullOrEmpty($existingProcess))
{
	Write-Output "Container is running. Stopping it."
	docker kill $instanceName
	docker rm $instanceName
}

Pop-Location