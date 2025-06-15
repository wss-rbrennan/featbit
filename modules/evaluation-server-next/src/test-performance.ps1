# Performance Testing Script for WebSocket Server
# This script runs k6 tests with different throughput levels to validate performance improvements

param(
    [int]$StartThroughput = 50,
    [int]$MaxThroughput = 500,
    [int]$Step = 50,
    [int]$Duration = 60,
    [string]$ServerUrl = "ws://localhost:5231/streaming"
)

Write-Host "Starting Performance Testing Suite" -ForegroundColor Green
Write-Host "Server URL: $ServerUrl" -ForegroundColor Yellow
Write-Host "Throughput Range: $StartThroughput to $MaxThroughput (step: $Step)" -ForegroundColor Yellow
Write-Host "Test Duration: $Duration seconds" -ForegroundColor Yellow

# Create results directory
$resultsDir = "performance-results-$(Get-Date -Format 'yyyy-MM-dd-HH-mm-ss')"
New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

# Test results summary
$testResults = @()

for ($throughput = $StartThroughput; $throughput -le $MaxThroughput; $throughput += $Step) {
    Write-Host "`nTesting with throughput: $throughput connections/second" -ForegroundColor Cyan
    
    $env:THROUGHPUT = $throughput
    $env:KEEP_PEAK_DURATION_SECONDS = $Duration
    $env:RAMPING_UP_DURATION_SECONDS = 10
    $env:RAMPING_DOWN_DURATION_SECONDS = 10
    
    $startTime = Get-Date
    
    try {
        # Run k6 test
        $k6Output = k6 run --out json="$resultsDir/results-$throughput.json" ../../../benchmark/k6-scripts/data-sync.js 2>&1 | Out-String
        
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds
        
        # Parse results from k6 output - look for success indicators and avoid failure indicators
        $hasErrors = $k6Output -match "failed" -or $k6Output -match "error" -or $k6Output -match "✗" -or $k6Output -match "x"
        $success = -not $hasErrors
        
        # Extract metrics with improved regex patterns
        $errorRate = "0"
        if ($k6Output -match "error_rate[^\d]*(\d+\.?\d*)%") { 
            $errorRate = $matches[1] 
        } elseif ($k6Output -match "(\d+\.?\d*)%.*error") {
            $errorRate = $matches[1]
        }
        
        $avgLatency = "N/A"
        if ($k6Output -match "avg=(\d+\.?\d*m?s)") { 
            $avgLatency = $matches[1] 
        } elseif ($k6Output -match "latency.*?(\d+\.?\d*m?s)") {
            $avgLatency = $matches[1]
        }
        
        $maxConnections = "N/A"
        if ($k6Output -match "vus_max[^\d]*(\d+)") { 
            $maxConnections = $matches[1] 
        } elseif ($k6Output -match "(\d+).*vus.*max") {
            $maxConnections = $matches[1]
        }
        
        # If error rate is above 5%, consider it a failure
        if ($errorRate -ne "N/A" -and $errorRate -ne "0" -and [double]$errorRate -gt 5) {
            $success = $false
        }
        
        $result = [PSCustomObject]@{
            Throughput = $throughput
            Success = $success
            ErrorRate = $errorRate
            AvgLatency = $avgLatency
            MaxConnections = $maxConnections
            Duration = [math]::Round($duration, 2)
            Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss")
        }
        
        $testResults += $result
        
        if ($success) {
            Write-Host "[PASS] Test completed successfully" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] Test failed or had errors" -ForegroundColor Red
        }
        
        Write-Host "  Error Rate: $errorRate%" -ForegroundColor $(if ($errorRate -eq "0" -or $errorRate -eq "N/A") { "Green" } else { "Yellow" })
        Write-Host "  Avg Latency: $avgLatency" -ForegroundColor Green
        Write-Host "  Max Connections: $maxConnections" -ForegroundColor Green
        Write-Host "  Duration: $($result.Duration)s" -ForegroundColor Green
        
    } catch {
        Write-Host "[ERROR] Test failed with error: $($_.Exception.Message)" -ForegroundColor Red
        
        $result = [PSCustomObject]@{
            Throughput = $throughput
            Success = $false
            ErrorRate = "ERROR"
            AvgLatency = "ERROR"
            MaxConnections = "ERROR"
            Duration = 0
            Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss")
        }
        
        $testResults += $result
    }
    
    # Wait between tests to allow server recovery
    Write-Host "Waiting 30 seconds before next test..." -ForegroundColor Gray
    Start-Sleep -Seconds 30
}

# Generate summary report
Write-Host "`n" + "="*80 -ForegroundColor Green
Write-Host "PERFORMANCE TEST SUMMARY" -ForegroundColor Green
Write-Host "="*80 -ForegroundColor Green

$testResults | Format-Table -AutoSize

# Export results to CSV
$csvPath = "$resultsDir/performance-summary.csv"
$testResults | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "`nResults exported to: $csvPath" -ForegroundColor Yellow

# Find performance limits
$successfulTests = $testResults | Where-Object { $_.Success -eq $true }
$maxSuccessfulThroughput = if ($successfulTests) { ($successfulTests | Measure-Object -Property Throughput -Maximum).Maximum } else { 0 }

Write-Host "`nPERFORMANCE ANALYSIS:" -ForegroundColor Green
Write-Host "Maximum successful throughput: $maxSuccessfulThroughput connections/second" -ForegroundColor $(if ($maxSuccessfulThroughput -gt 200) { "Green" } else { "Yellow" })

if ($maxSuccessfulThroughput -gt 0) {
    $bestTest = $successfulTests | Where-Object { $_.Throughput -eq $maxSuccessfulThroughput }
    Write-Host "Best performance achieved:" -ForegroundColor Green
    Write-Host "  - Throughput: $($bestTest.Throughput) conn/s" -ForegroundColor Green
    Write-Host "  - Error Rate: $($bestTest.ErrorRate)%" -ForegroundColor Green
    Write-Host "  - Avg Latency: $($bestTest.AvgLatency)" -ForegroundColor Green
    Write-Host "  - Max Connections: $($bestTest.MaxConnections)" -ForegroundColor Green
}

Write-Host "`nTest completed. Results saved in: $resultsDir" -ForegroundColor Green 