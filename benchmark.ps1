param ($build=$true, $Parallelism=100, $Protocols="*", $warmup = $true)

function Main()
{
	if ($build) 
	{
		.\build31.ps1
		.\build50.ps1
	}
	if ($warmup)
	{
		Write-Host "Warm up 2 minutes"
		Run-Test net31 "false" 5 120 "h2"
	}
	
	clear
	
	Run-Test -platform net31 -ClientPerThread "false" -Protocols $Protocols
	Run-Test -platform net31 -ClientPerThread "true" -Protocols $Protocols
	Run-Test -platform net50 -ClientPerThread "false" -Protocols $Protocols
	Run-Test -platform net50 -ClientPerThread "true" -Protocols $Protocols
}

function Run-Test($platform, $ClientPerThread, $ReportFrequency = 30, $TestDuration = 180, $Protocols = "*")
{
	$dotnet = "dotnet.exe"
	if ($platform -eq "net50") { $dotnet = "C:\Users\vlpereva\bin\dotnet-sdk-5\dotnet.exe" }
		
	Write-Host ""
	Write-Host "$platform tests"
	$server = Start-Process -FilePath $dotnet -PassThru -ArgumentList .\$platform\GrpcSampleServer.dll
	& $dotnet .\$platform\GrpcSampleClient.dll --ReportFrequency=$ReportFrequency --TestDuration=$TestDuration --Protocols=$Protocols --ClientPerThread=$ClientPerThread --Parallelism=$Parallelism
	$server.Kill()
}

Main