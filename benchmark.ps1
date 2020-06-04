function Main()
{
	.\build31.ps1
	.\build50.ps1
	Write-Host "Warm up 2 minutes"
	Run-Test net31 "false" 5 120 "g"
	
	clear
	
	Run-Test net31 "false"
	Run-Test net31 "true"
	Run-Test net50 "false"
	Run-Test net50 "true"
}

function Run-Test($platform, $ClientPerThread, $ReportFrequency = 30, $TestDuration = 180, $Protocols = "*")
{
	$dotnet = "dotnet.exe"
	if ($platform -eq "net50") { $dotnet = "C:\Users\vlpereva\bin\dotnet-sdk-5\dotnet.exe" }
		
	Write-Host ""
	Write-Host "$platform tests"
	$server = Start-Process -FilePath $dotnet -PassThru -ArgumentList .\$platform\GrpcSampleServer.dll
	& $dotnet .\$platform\GrpcSampleClient.dll --ReportFrequency=$ReportFrequency --TestDuration=$TestDuration --Protocols=$Protocols --ClientPerThread=$ClientPerThread
	$server.Kill()
}

Main