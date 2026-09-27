# Setup the `floodlink` Postgres role + database to match the API connection string
# (appsettings.json). Safe to re-run.
#
# Run from the repo root:
#   powershell -ExecutionPolicy Bypass -File setup_db.ps1

$ErrorActionPreference = 'Continue'

$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
if (-not (Test-Path $psql)) {
    Write-Host "[ERROR] psql not found at: $psql" -ForegroundColor Red
    Write-Host "PostgreSQL 18 seems to be installed elsewhere. Find psql.exe and edit the path above."
    exit 1
}

Write-Host "Enter your local PostgreSQL SUPERUSER password (set during the PostgreSQL 18 install):" -ForegroundColor Yellow
$secure = Read-Host -AsSecureString
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$pw = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

$env:PGPASSWORD = $pw

Write-Host ""
Write-Host "[1/2] Creating role 'floodlink' (password floodlink_dev_pw) ..."
& $psql -U postgres -h localhost -c "CREATE USER floodlink WITH LOGIN PASSWORD 'floodlink_dev_pw';" -c "ALTER ROLE floodlink WITH LOGIN PASSWORD 'floodlink_dev_pw';"

Write-Host "[2/2] Creating database 'floodlink' (owner floodlink) ..."
& $psql -U postgres -h localhost -c "CREATE DATABASE floodlink OWNER floodlink;"

Remove-Item Env:\PGPASSWORD

Write-Host ""
Write-Host "Done. Next steps:" -ForegroundColor Green
Write-Host "  cd backend/src/FloodLink.Api"
Write-Host "  dotnet ef database update"
Write-Host "  dotnet run"