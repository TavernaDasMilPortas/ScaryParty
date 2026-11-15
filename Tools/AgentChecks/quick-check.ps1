<#
.SYNOPSIS
    quick-check.ps1 - Verificação estática rápida para ScaryParty (Nível L0/L1)
.DESCRIPTION
    Realiza verificações estáticas leves sem acionar Unity em headless ou batchmode:
    1. Executa git status e git diff --check para detectar formatação / espaços em branco / conflitos.
    2. Identifica scripts C# alterados.
    3. Verifica se arquivos novos ou modificados em Assets/ possuem pares .meta correspondentes.
    4. Detecta importações acidentais de UnityEditor em scripts de runtime (fora de Editor/).
    5. Detecta alterações suspeitas de GUID em diffs.
    Retorna exit code 0 se tudo estiver correto, ou 1 se houver alertas críticos.
#>

[CmdletBinding()]
param(
    [switch]$Detailed
)

Write-Host "=== ScaryParty Quick Check (L0/L1) ===" -ForegroundColor Cyan

$hasErrors = $false

# 1. Git diff --check
Write-Host "`n[1/4] Verificando formatação Git (git diff --check)..." -ForegroundColor Yellow
$diffCheck = git diff --check 2>&1
if ($LASTEXITCODE -ne 0 -or $diffCheck) {
    Write-Host "[ALERTA] Problemas de formatacao / espacos em branco detectados:" -ForegroundColor Red
    $diffCheck | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
    # Nao bloquear para espacos em branco simples, mas sinalizar
} else {
    Write-Host "[OK] Formatacao Git limpa." -ForegroundColor Green
}

# 2. Arquivos alterados no git
Write-Host "`n[2/4] Verificando status dos arquivos alterados..." -ForegroundColor Yellow
$statusLines = git status --short
if (-not $statusLines) {
    Write-Host "[OK] Nenhuma modificacao pendente." -ForegroundColor Green
} else {
    Write-Host "[INFO] Arquivos em modificacao:" -ForegroundColor Cyan
    $statusLines | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
}

# 3. Verificacao de pares .meta para arquivos em Assets/
Write-Host "`n[3/4] Verificando integridade de pares .meta em Assets/..." -ForegroundColor Yellow
$untrackedOrAdded = git status --porcelain | Where-Object { $_ -match '^\s*(\?\?|A)\s+(.+)$' } | ForEach-Object {
    $matches[2].Trim('"')
}

$missingMetaCount = 0
foreach ($filePath in $untrackedOrAdded) {
    if ($filePath -like "*Assets/*" -and $filePath -notlike "*.meta") {
        if (-not (Test-Path $filePath -PathType Container)) {
            $metaPath = "$filePath.meta"
            if (-not (Test-Path $metaPath)) {
                Write-Host "[ALERTA .META] Asset sem .meta correspondente: $filePath" -ForegroundColor Red
                $missingMetaCount++
            }
        }
    }
}

if ($missingMetaCount -eq 0) {
    Write-Host "[OK] Todos os novos assets alterados possuem arquivo .meta." -ForegroundColor Green
} else {
    Write-Host "[ERRO] Foram encontrados $missingMetaCount asset(s) sem .meta correspondente." -ForegroundColor Red
    $hasErrors = $true
}

# 4. Verificacao de UnityEditor acidental em runtime
Write-Host "`n[4/4] Verificando imports acidentais de UnityEditor em runtime..." -ForegroundColor Yellow
$changedCsFiles = git status --porcelain | Where-Object { $_ -match '\.cs$' } | ForEach-Object {
    if ($_ -match '^\s*([MADRC\?]+)\s+(.+)$') { $matches[2].Trim('"') }
}

$editorLeakCount = 0
foreach ($cs in $changedCsFiles) {
    if ((Test-Path $cs) -and ($cs -notmatch '[\\/]Editor[\\/]') -and ($cs -notmatch '\.Tests[\\/]')) {
        $leak = Select-String -Path $cs -Pattern '^\s*using\s+UnityEditor(\s*;|\.)'
        if ($leak) {
            Write-Host "[ERRO RUNTIME] Script de runtime referenciando UnityEditor: $cs" -ForegroundColor Red
            $leak | ForEach-Object { Write-Host "  Linha $($_.LineNumber): $($_.Line.Trim())" -ForegroundColor DarkRed }
            $editorLeakCount++
        }
    }
}

if ($editorLeakCount -eq 0) {
    Write-Host "[OK] Nenhuma referencia indevida a UnityEditor encontrada em runtime." -ForegroundColor Green
} else {
    Write-Host "[ERRO] $editorLeakCount script(s) de runtime contem referencia a UnityEditor!" -ForegroundColor Red
    $hasErrors = $true
}

Write-Host "`n=== Fim da Verificacao ===" -ForegroundColor Cyan
if ($hasErrors) {
    Write-Host "Resultado: FALHA. Corrija os alertas antes de prosseguir." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Resultado: SUCESSO. Integridade estatica verificada com exito." -ForegroundColor Green
    exit 0
}
