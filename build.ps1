# ============================================================
# build.ps1 -- Build limpo do Core_Aim
# Uso: .\build.ps1
# Output: C:\Projeto_Core\Core_Aim\App\
#
# Arquivos extras (auth.bin, Models, tt2_bridge.dll, etc.):
# Coloque em C:\Projeto_Core\Core_Aim\_assets\
# Sao copiados automaticamente para App\ a cada build.
# ============================================================

$ProjectDir = "$PSScriptRoot\Core_Aim"
$OutputDir  = "$PSScriptRoot\App"
$AssetsDir  = "$PSScriptRoot\_assets"
$BinDir     = "$ProjectDir\bin"
$ObjDir     = "$ProjectDir\obj"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Core_Aim Build" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# -- 1. Encerra Core_Aim.exe se estiver em execucao --------------------------
$proc = Get-Process -Name "Core_Aim" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "[1/4] Encerrando Core_Aim.exe (PID $($proc.Id))..." -ForegroundColor Yellow
    $proc | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# -- 1. Apaga bin\, obj\ e App\ (assets ficam em _assets\, nunca apagados) ----
Write-Host "[1/4] Limpando pastas antigas..." -ForegroundColor Yellow
if (Test-Path $BinDir)    { Remove-Item $BinDir    -Recurse -Force }
if (Test-Path $ObjDir)    { Remove-Item $ObjDir    -Recurse -Force }
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
Write-Host "      Limpo." -ForegroundColor Green

# -- 2. Publish (pasta unica, sem subpastas de configuracao) ------------------
Write-Host "[2/4] Publicando para App\..." -ForegroundColor Yellow
dotnet publish "$ProjectDir\Core_Aim.csproj" `
    -c Release `
    -p:Platform=x64 `
    -o "$OutputDir" `
    --nologo `
    --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: Publish falhou com codigo $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "      Publish OK." -ForegroundColor Green

# -- 3. Copia _assets\ para App\ (garante tt2_bridge.dll, auth.bin, Models) --
if (Test-Path $AssetsDir) {
    Write-Host "[3/4] Copiando _assets\ para App\..." -ForegroundColor Yellow
    Copy-Item "$AssetsDir\*" "$OutputDir\" -Recurse -Force
    Write-Host "      Assets copiados." -ForegroundColor Green
} else {
    Write-Host "[3/4] Pasta _assets\ nao encontrada (ignorado)." -ForegroundColor DarkYellow
}

# -- 4. Remove pastas de idioma residuais -------------------------------------
Write-Host "[4/4] Removendo pastas de idioma residuais..." -ForegroundColor Yellow
$langs = @("cs","cs-CZ","de","es","fr","hu","it","ja","ja-JP","ko",
           "pl","pt","pt-BR","ro","ru","sv","tr","zh","zh-Hans","zh-Hant")
$removed = 0
foreach ($lang in $langs) {
    $p = "$OutputDir\$lang"
    if (Test-Path $p) { Remove-Item $p -Recurse -Force; $removed++ }
}
Write-Host "      $removed pasta(s) removida(s)." -ForegroundColor Green

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Pronto! Output: $OutputDir" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
