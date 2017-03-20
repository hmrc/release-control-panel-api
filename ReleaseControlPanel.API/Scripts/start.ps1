Push-Location ..

$instanceName = "rcp-api"
$containerName = "hmrc/releasecontrolcanelapi"

Write-Output "Checking if application is running"
$existingProcess = docker ps -a | findstr $instanceName
IF (![string]::IsNullOrEmpty($existingProcess))
{
	Write-Output "Container is running. Stopping it."
	docker kill $instanceName
}

Write-Output "Starting application on port :5000"
docker run --link rcp-mongodb:mongo -itd -p 5000:80 --name $instanceName $containerName 

Pop-Location