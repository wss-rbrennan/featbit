#!/usr/bin/env pwsh
#
# Code Coverage Script for FeatBit Evaluation Server Next
# Runs all tests with coverage collection and generates HTML reports
#

param(
    [switch]$SkipApplicationTests,
    [switch]$SkipStreamingTests,
    [switch]$OpenReport
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 FeatBit Evaluation Server - Code Coverage Analysis" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green

# Create coverage output directory
$CoverageDir = "coverage"
if (Test-Path $CoverageDir) {
    Remove-Item $CoverageDir -Recurse -Force
    Write-Host "🧹 Cleaned existing coverage directory" -ForegroundColor Yellow
}
New-Item -Path $CoverageDir -ItemType Directory | Out-Null

# Function to run tests with coverage
function Run-TestsWithCoverage {
    param(
        [string]$ProjectPath,
        [string]$ProjectName,
        [string]$OutputPath
    )
    
    Write-Host "🧪 Running $ProjectName tests with coverage..." -ForegroundColor Cyan
    
    dotnet test $ProjectPath `
        --collect:"XPlat Code Coverage" `
        --results-directory $OutputPath `
        --logger "console;verbosity=minimal" `
        /p:CollectCoverage=true `
        /p:CoverletOutputFormat=opencover `
        /p:CoverletOutput="$OutputPath/coverage.opencover.xml"
        
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ $ProjectName tests failed!"
        exit 1
    }
    
    Write-Host "✅ $ProjectName tests completed successfully" -ForegroundColor Green
}

# Run Application Integration Tests
if (-not $SkipApplicationTests) {
    $AppTestsOutput = "$CoverageDir/application"
    New-Item -Path $AppTestsOutput -ItemType Directory -Force | Out-Null
    
    Run-TestsWithCoverage `
        -ProjectPath "tests/Application.IntegrationTests/Application.IntegrationTests.csproj" `
        -ProjectName "Application.IntegrationTests" `
        -OutputPath $AppTestsOutput
}

# Run Streaming Integration Tests  
if (-not $SkipStreamingTests) {
    $StreamingTestsOutput = "$CoverageDir/streaming"
    New-Item -Path $StreamingTestsOutput -ItemType Directory -Force | Out-Null
    
    Run-TestsWithCoverage `
        -ProjectPath "tests/Streaming.IntegrationTests/Streaming.IntegrationTests.csproj" `
        -ProjectName "Streaming.IntegrationTests" `
        -OutputPath $StreamingTestsOutput
}

# Find all coverage files
Write-Host "📊 Generating coverage reports..." -ForegroundColor Cyan

$CoverageFiles = Get-ChildItem -Path $CoverageDir -Filter "*.opencover.xml" -Recurse | ForEach-Object { $_.FullName }
$TestResultFiles = Get-ChildItem -Path $CoverageDir -Filter "coverage.cobertura.xml" -Recurse | ForEach-Object { $_.FullName }

if ($CoverageFiles.Count -eq 0 -and $TestResultFiles.Count -eq 0) {
    # Fallback: Look for any XML coverage files
    $CoverageFiles = Get-ChildItem -Path $CoverageDir -Filter "*.xml" -Recurse | 
        Where-Object { $_.Name -like "*coverage*" -or $_.Name -like "*cobertura*" } | 
        ForEach-Object { $_.FullName }
}

if ($CoverageFiles.Count -eq 0) {
    Write-Warning "⚠️  No coverage files found. Coverage collection may have failed."
    Write-Host "📁 Coverage directory contents:" -ForegroundColor Yellow
    Get-ChildItem -Path $CoverageDir -Recurse | ForEach-Object { 
        Write-Host "   $($_.FullName)" -ForegroundColor Gray 
    }
} else {
    Write-Host "📈 Found $($CoverageFiles.Count) coverage file(s)" -ForegroundColor Green
    
    # Generate combined HTML report
    $ReportDir = "$CoverageDir/html-report"
    $CoverageFilesArg = $CoverageFiles -join ";"
    
    Write-Host "🎨 Generating HTML report..." -ForegroundColor Cyan
    
    dotnet tool install --global dotnet-reportgenerator-globaltool --ignore-failed-sources 2>$null
    
    reportgenerator `
        "-reports:$CoverageFilesArg" `
        "-targetdir:$ReportDir" `
        "-reporttypes:Html;Badges;TextSummary" `
        "-title:FeatBit Evaluation Server Next - Code Coverage" `
        "-tag:integration-tests"
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ HTML coverage report generated successfully!" -ForegroundColor Green
        Write-Host "📁 Report location: $ReportDir/index.html" -ForegroundColor Green
        
        # Display summary
        $SummaryFile = "$ReportDir/Summary.txt"
        if (Test-Path $SummaryFile) {
            Write-Host "`n📊 Coverage Summary:" -ForegroundColor Yellow
            Get-Content $SummaryFile | Write-Host
        }
        
        # Open report if requested
        if ($OpenReport) {
            $ReportPath = (Resolve-Path "$ReportDir/index.html").Path
            Write-Host "🌐 Opening coverage report..." -ForegroundColor Cyan
            Start-Process $ReportPath
        } else {
            Write-Host "💡 To view the report, run: Start-Process '$ReportDir/index.html'" -ForegroundColor Yellow
        }
    } else {
        Write-Warning "⚠️  Failed to generate HTML report"
    }
}

Write-Host "`n🎉 Coverage analysis complete!" -ForegroundColor Green
Write-Host "📁 All coverage artifacts saved to: $CoverageDir" -ForegroundColor Green 