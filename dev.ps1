<#
.SYNOPSIS
  One-command Reflow dev environment: stop, build, start backend + frontend.

.USAGE
  .\dev.ps1          # build + start everything
  .\dev.ps1 -Test    # also run the full test suite before starting
#>
param([switch]$Test)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$LogDir = Join-Path $env:LOCALAPPDATA "Temp\opencode"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$BackendLog = Join-Path $LogDir "reflow-backend.log"
$BackendErr = Join-Path $LogDir "reflow-backend-err.log"
$FrontendLog = Join-Path $LogDir "reflow-frontend.log"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

function Wait-Port($Port, $TimeoutSec, $Label) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $c = New-Object Net.Sockets.TcpClient
        try {
            $iar = $c.BeginConnect("localhost", $Port, $null, $null)
            if ($iar.AsyncWaitHandle.WaitOne(1000)) { $c.EndConnect($iar); Write-Host "$Label is up (port $Port)"; return $true }
        } catch { } finally { $c.Close() }
        Start-Sleep -Seconds 3
    }
    return $false
}

# 1. Stop any running backend (it locks build output).
Write-Step "Stopping old backend"
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='Reflow.Api.exe'" |
    Where-Object { $_.CommandLine -like '*Reflow*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Host "Stopped PID $($_.ProcessId)" }
Start-Sleep -Seconds 2

# 2. Build.
Write-Step "Building"
dotnet build (Join-Path $Root "Reflow.slnx") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 3. Optional tests.
if ($Test) {
    Write-Step "Testing (full suite)"
    dotnet test (Join-Path $Root "Reflow.slnx") --no-build --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

# 4. Start backend.
Write-Step "Starting backend"
Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project src\Reflow.Api --no-build --urls http://localhost:5001" `
    -WorkingDirectory $Root `
    -RedirectStandardOutput $BackendLog -RedirectStandardError $BackendErr `
    -WindowStyle Hidden | Out-Null

# 5. Wait for real readiness via /health (app + Postgres probe).
Write-Step "Waiting for backend readiness"
$deadline = (Get-Date).AddMinutes(3)
$ready = $false
while ((Get-Date) -lt $deadline) {
    try {
        $h = Invoke-RestMethod -Uri "http://localhost:5001/health" -TimeoutSec 5
        if ($h.status -eq "healthy") { $ready = $true; break }
        Write-Host "Backend degraded (database unreachable?), waiting..."
    } catch { }
    Start-Sleep -Seconds 4
}
if (-not $ready) { throw "Backend did not become healthy in time. See $BackendLog" }
Write-Host "Backend healthy: http://localhost:5001/health"

# 6. Reuse a running frontend if there is one; otherwise start it.
function Find-Frontend {
    foreach ($port in 5173..5180) {
        try {
            $page = Invoke-WebRequest -Uri "http://localhost:$port/" -UseBasicParsing -TimeoutSec 5
            if ($page.StatusCode -eq 200 -and $page.Content -match 'id="root"') {
                return "http://localhost:$port/"
            }
        } catch { }
    }
    return $null
}

$frontendUrl = Find-Frontend
if ($frontendUrl) {
    Write-Host "Reusing running frontend: $frontendUrl"
} else {
    Write-Step "Starting frontend"
    Start-Process -FilePath "cmd.exe" `
        -ArgumentList "/c npm run dev > `"$FrontendLog`" 2>&1" `
        -WorkingDirectory (Join-Path $Root "frontend") `
        -WindowStyle Hidden | Out-Null
}

# 7. Wait until the frontend answers (fresh start can take a bit).
$deadline = (Get-Date).AddSeconds(90)
while ((Get-Date) -lt $deadline -and -not $frontendUrl) {
    Start-Sleep -Seconds 3
    $frontendUrl = Find-Frontend
}
if (-not $frontendUrl) { throw "Frontend did not start in time. See $FrontendLog" }

Write-Host ""
Write-Host "Reflow is running:" -ForegroundColor Green
Write-Host "  Backend:  http://localhost:5001  (health: http://localhost:5001/health)"
Write-Host "  Frontend: $frontendUrl"
Write-Host "  Login:    demo@reflow.local / Demo1234"
Write-Host "  Logs:     $BackendLog"
Write-Host "            $FrontendLog"
