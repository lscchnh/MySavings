# Stop-MySavings.ps1 — Arrête l'API et le frontend
Get-Process -Name "MySavings.ApiService","MySavings.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "MySavings arrêté."
