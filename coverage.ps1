#!/usr/bin/env pwsh
# ========================================
# Code Coverage Report Generator (PowerShell)
# ========================================
# Runs tests with coverage, generates HTML report
# Usage: ./coverage.ps1 [threshold]
#     ./coverage.ps1 80  # Enforce 80% coverage
# ========================================

param(
    [int]$Threshold = 0  # Default: no threshold enforcement
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " CineMatch Code Coverage" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Clean previous coverage results
Write-Host "?? Cleaning previous coverage results..." -ForegroundColor Yellow
if (Test-Path "./TestResults") {
    Remove-Item -Recurse -Force "./TestResults"
}
if (Test-Path "./coverage-report") {
    Remove-Item -Recurse -Force "./coverage-report"
}

# 2. Restore packages (ensures coverlet.msbuild is available)
Write-Host "?? Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# 3. Run tests with coverage
Write-Host ""
Write-Host "?? Running tests with coverage collection..." -ForegroundColor Yellow
Write-Host "   Excluding: Migrations, DTOs, Entities, Models" -ForegroundColor Gray

# Build test command with proper escaping
$testArgs = @(
    "test"
    "/p:CollectCoverage=true"
    "/p:CoverletOutputFormat=cobertura"
    '--no-restore'
    '--verbosity', 'minimal'
)

if ($Threshold -gt 0) {
    Write-Host "Threshold: $Threshold% (build will fail if below)" -ForegroundColor Magenta
    $testArgs += "/p:Threshold=$Threshold"
    $testArgs += "/p:ThresholdType=line"
    $testArgs += "/p:ThresholdStat=total"
} else {
    Write-Host "   Threshold: None (report only)" -ForegroundColor Green
}

# Run dotnet test (exclusions from Directory.Build.props will be used)
& dotnet @testArgs

$testExitCode = $LASTEXITCODE

if ($testExitCode -ne 0) {
    Write-Host ""
    Write-Host "? Tests failed or coverage threshold not met!" -ForegroundColor Red
    Write-Host "   Exit code: $testExitCode" -ForegroundColor Red
    exit $testExitCode
}

# 4. Find coverage files
Write-Host ""
Write-Host "?? Locating coverage files..." -ForegroundColor Yellow
$coverageFiles = Get-ChildItem -Recurse -Filter "coverage.cobertura.xml" | Select-Object -ExpandProperty FullName

if ($coverageFiles.Count -eq 0) {
    Write-Host "? No coverage files found!" -ForegroundColor Red
    Write-Host "   Expected: **/TestResults/coverage/coverage.cobertura.xml" -ForegroundColor Yellow
    exit 1
}

Write-Host "   Found $($coverageFiles.Count) coverage file(s)" -ForegroundColor Green
foreach ($file in $coverageFiles) {
    Write-Host "   - $file" -ForegroundColor Gray
}

# 5. Install ReportGenerator (if not already installed)
Write-Host ""
Write-Host "?? Ensuring ReportGenerator is installed..." -ForegroundColor Yellow
$reportGenPath = dotnet tool list --global | Select-String "dotnet-reportgenerator-globaltool"

if (-not $reportGenPath) {
    Write-Host "   Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
} else {
    Write-Host "   ReportGenerator is already installed" -ForegroundColor Green
}

# 6. Generate HTML report using FULL PATH
Write-Host ""
Write-Host "?? Generating HTML coverage report..." -ForegroundColor Yellow

$reportsArg = $coverageFiles -join ";"

# Get the user's .dotnet tools directory
$userProfile = $env:USERPROFILE
$reportGenExe = "$userProfile\.dotnet\tools\reportgenerator.exe"

Write-Host "   Using ReportGenerator at: $reportGenExe" -ForegroundColor Gray

if (Test-Path $reportGenExe) {
    & $reportGenExe `
      "-reports:$reportsArg" `
        "-targetdir:./coverage-report" `
   "-reporttypes:Html;TextSummary" `
        "-title:CineMatch Code Coverage"
} else {
    Write-Host "   ??  ReportGenerator exe not found at expected location" -ForegroundColor Yellow
    Write-Host "   Trying dotnet command (requires PATH refresh)..." -ForegroundColor Yellow
    
    # Try refreshing PATH first
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    
    dotnet reportgenerator `
        "-reports:$reportsArg" `
        "-targetdir:./coverage-report" `
     "-reporttypes:Html;TextSummary" `
      "-title:CineMatch Code Coverage"
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Report generation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 7. Display summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " ? Coverage Report Generated!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? HTML Report: ./coverage-report/index.html" -ForegroundColor Cyan
Write-Host "?? Coverage Files: $($coverageFiles.Count) file(s) in ./TestResults/" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Opening report in browser..." -ForegroundColor Yellow

# 8. Open report in default browser
$reportPath = Resolve-Path "./coverage-report/index.html"
Start-Process $reportPath

Write-Host ""
Write-Host "? Done!" -ForegroundColor Green
Write-Host ""
