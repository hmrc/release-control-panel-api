Push-Location ..

dotnet publish -r ubuntu.14.04-x64

$instanceName = "rcp-api"
$containerName = "hmrc/releasecontrolcanelapi"

Write-Output "Checking if application is running"
$existingProcess = docker ps -a | findstr $instanceName
IF (![string]::IsNullOrEmpty($existingProcess))
{
	Write-Output "Container is running. Stopping it."
	docker kill $instanceName
	docker rm $instanceName
}

docker build -t $containerName .

Pop-Location