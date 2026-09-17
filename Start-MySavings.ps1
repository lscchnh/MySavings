# Start-MySavings.ps1 — Compile, lance l'API et le frontend, ouvre le navigateur
$root    = $PSScriptRoot
$distApi = Join-Path $root "dist\ApiService"
$distWeb = Join-Path $root "dist\Web"
$apiUrl  = "http://localhost:5560"
$webBind = "http://0.0.0.0:5028"   # écoute sur toutes les interfaces (WiFi inclus)
$webUrl  = "http://localhost:5028"  # utilisé pour le health-check et le navigateur local

# IP locale pour affichage (accès téléphone)
$localIp = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "*Wi-Fi*","*WiFi*","*WLAN*","*Ethernet*" `
            -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike "169.*" } |
            Select-Object -First 1).IPAddress

# Toujours pointer vers la même base que VS (un seul fichier, pas de désynchronisation)
$dbPath  = Join-Path $root "MySavings.ApiService\mysavings.db"

# Tuer d'éventuelles instances avant la compilation (sinon les DLL sont verrouillées)
Get-Process -Name "MySavings.ApiService" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "MySavings.Web"        -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# ── Compilation ────────────────────────────────────────────────────────────────
Write-Host "Compilation en cours..." -ForegroundColor Cyan

$apiProject = Join-Path $root "MySavings.ApiService\MySavings.ApiService.csproj"
$webProject = Join-Path $root "MySavings.Web\MySavings.Web.csproj"

dotnet publish $apiProject -c Release -o $distApi --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Échec de la compilation de l'API." -ForegroundColor Red
    Read-Host "Appuyez sur Entrée pour fermer"
    exit 1
}

dotnet publish $webProject -c Release -o $distWeb --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Échec de la compilation du Web." -ForegroundColor Red
    Read-Host "Appuyez sur Entrée pour fermer"
    exit 1
}

Write-Host "Compilation terminée." -ForegroundColor Green
# ──────────────────────────────────────────────────────────────────────────────

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
    ASPNETCORE_URLS                     = $webBind
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

# Afficher l'URL téléphone
if ($localIp) {
    Write-Host ""
    Write-Host "Accès depuis le téléphone (même WiFi) :" -ForegroundColor Cyan
    Write-Host "  http://${localIp}:5028" -ForegroundColor Yellow
}
