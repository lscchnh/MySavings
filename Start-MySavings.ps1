# Start-MySavings.ps1 — Lance l'API et le frontend, ouvre le navigateur
$root    = $PSScriptRoot
$distApi = Join-Path $root "dist\ApiService"
$distWeb = Join-Path $root "dist\Web"
$apiUrl  = "http://localhost:5560"
$webUrl  = "http://localhost:5028"

# Toujours pointer vers la même base que VS (un seul fichier, pas de désynchronisation)
$dbPath  = Join-Path $root "MySavings.ApiService\mysavings.db"

function Start-Hidden($exe, $workDir, $envVars) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName       = $exe
    $psi.WorkingDirectory = $workDir
    $psi.UseShellExecute  = $false
    $psi.CreateNoWindow   = $true
    foreach ($kv in $envVars.GetEnumerator()) {
        $psi.EnvironmentVariables[$kv.Key] = $kv.Value
    }
    [System.Diagnostics.Process]::Start($psi) | Out-Null
}

# Tuer d'éventuelles instances déjà en cours
Get-Process -Name "MySavings.ApiService" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "MySavings.Web"        -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# Démarrer l'API (toujours sur la base dev — même fichier que VS)
Start-Hidden "$distApi\MySavings.ApiService.exe" $distApi @{
    ASPNETCORE_ENVIRONMENT          = "Development"
    ASPNETCORE_URLS                 = $apiUrl
    "ConnectionStrings__MySavingsDb" = "Data Source=$dbPath"
}

# Attendre que l'API soit prête (max 30 s)
for ($i = 0; $i -lt 30; $i++) {
    try {
        $r = Invoke-WebRequest "$apiUrl/health" -UseBasicParsing -TimeoutSec 1 -EA Stop
        if ($r.StatusCode -eq 200) { break }
    } catch { Start-Sleep -Seconds 1 }
}

# Démarrer le frontend Web (avec service discovery Aspire simulé)
Start-Hidden "$distWeb\MySavings.Web.exe" $distWeb @{
    ASPNETCORE_ENVIRONMENT              = "Development"
    ASPNETCORE_URLS                     = $webUrl
    "services__apiservice__http__0"     = $apiUrl
}

# Attendre que le Web soit prêt (max 30 s)
for ($i = 0; $i -lt 30; $i++) {
    try {
        $r = Invoke-WebRequest "$webUrl/health" -UseBasicParsing -TimeoutSec 1 -EA Stop
        if ($r.StatusCode -eq 200) { break }
    } catch { Start-Sleep -Seconds 1 }
}

# Ouvrir le navigateur
Start-Process $webUrl
