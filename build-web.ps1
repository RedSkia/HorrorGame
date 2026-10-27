# Runs the Unity web build (called by build.cmd) and prints a status line every few seconds by following the build log.
# Unity runs in its own hidden window, so closing build.cmd does not stop the build; running build.cmd again
# while it runs attaches to it and shows its progress. Exit code 0 = build succeeded (read from the log).
param([string]$Unity, [string]$Project, [string]$Log)

# Log line -> phase shown to the user (last match wins)
$phases = [ordered]@{
    'Start importing'                 = 'Importing assets'
    'Compiling shader'                = 'Compiling shaders'
    'Building scene|Building player'  = 'Building scene'
    'bee_backend|il2cpp'              = 'Compiling code (IL2CPP + WebAssembly)'
    'Build Finished'                  = 'Finishing'
}

$projectPattern = [regex]::Escape($Project)
$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match 'batchmode' -and $_.CommandLine -match $projectPattern } | Select-Object -First 1

if ($running) {
    Write-Host 'A web build is already running; showing its progress.'
    $unityProcess = Get-Process -Id $running.ProcessId
    $start = $running.CreationDate
} else {
    Remove-Item $Log -ErrorAction SilentlyContinue
    $arguments = "-batchmode -nographics -quit -projectPath `"$Project`" -buildTarget WebGL -executeMethod BuildWeb.Run -logFile `"$Log`""
    $unityProcess = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $start = Get-Date
}

$phase = 'Starting Unity'
$read = 0
while (-not $unityProcess.HasExited) {
    if (Test-Path $Log) {
        $lines = @(Get-Content $Log -ErrorAction SilentlyContinue)
        for ($i = $read; $i -lt $lines.Count; $i++) {
            foreach ($pattern in $phases.Keys) { if ($lines[$i] -match $pattern) { $phase = $phases[$pattern] } }
        }
        $read = $lines.Count
    }
    $elapsed = (Get-Date) - $start
    Write-Host ("`r  {0:mm\:ss}  {1,-40}" -f $elapsed, $phase) -NoNewline
    Start-Sleep -Seconds 3
}
Write-Host ''
if (Select-String -Path $Log -Pattern 'Build Finished, Result: Success' -Quiet) { exit 0 }
exit 1
