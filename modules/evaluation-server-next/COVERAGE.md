# Code Coverage - FeatBit Evaluation Server Next

This document describes the code coverage setup for the FeatBit Evaluation Server Next project.

## 🎯 Overview

The project uses **Coverlet** for code coverage collection and **ReportGenerator** for creating beautiful HTML reports. Coverage is configured for both:

- **Application.IntegrationTests** - Business logic tests
- **Streaming.IntegrationTests** - WebSocket integration tests

## 🚀 Quick Start

### Run Tests with Coverage

```bash
# Run specific test project with coverage
dotnet test tests/Streaming.IntegrationTests/Streaming.IntegrationTests.csproj --collect:"XPlat Code Coverage"

# Run all tests with coverage (PowerShell script)
./run-coverage.ps1

# Generate HTML report from latest results
reportgenerator "-reports:tests/*/TestResults/*/coverage.cobertura.xml" "-targetdir:coverage-report" "-reporttypes:Html;TextSummary"
```

### View Results

```bash
# Open HTML report
Start-Process coverage-report/index.html

# View text summary
Get-Content coverage-report/Summary.txt
```

## 📊 Current Coverage Metrics

Based on our latest integration test run:

| **Metric** | **Coverage** | **Details** |
|------------|-------------|-------------|
| **Line Coverage** | **18%** | 1,162 of 6,423 lines covered |
| **Branch Coverage** | **12.6%** | 153 of 1,207 branches covered |
| **Method Coverage** | **22%** | 162 of 735 methods covered |

### Coverage by Assembly

| **Assembly** | **Coverage** | **Focus** |
|-------------|-------------|-----------|
| **Edge** | **69%** | ✅ Program startup, middleware setup |
| **Streaming** | **26.5%** | ✅ WebSocket service, connection management |
| **Domain** | **17.9%** | ✅ Token validation, test data |
| **Application** | **10.5%** | ✅ Request validation |
| **Infrastructure** | **10.3%** | ✅ Lifecycle services, connection context |
| **DataStore** | **8.1%** | ✅ Configuration, dependency injection |

## 🔧 Configuration

### Project-Level Settings

Both test projects include coverage configuration in their `.csproj` files:

```xml
<!-- Code Coverage Configuration -->
<CollectCoverage>true</CollectCoverage>
<CoverletOutputFormat>opencover,lcov,json</CoverletOutputFormat>
<CoverletOutput>$(OutputPath)coverage/</CoverletOutput>
<Exclude>[*]*.Migrations.*,[*]*Tests*,[*]*Test*</Exclude>
<Include>[Application]*,[Domain]*,[Infrastructure]*,[Streaming]*,[DataStore]*,[Edge]*</Include>
```

### Coverage Tools

| **Tool** | **Purpose** | **Version** |
|----------|-------------|-------------|
| `coverlet.collector` | MSBuild coverage collection | 6.0.2 |
| `coverlet.msbuild` | MSBuild integration | 6.0.2 |
| `ReportGenerator` | HTML report generation | 5.4.1 |

## 📝 Scripts

### PowerShell Script (`run-coverage.ps1`)

Comprehensive script that:
- ✅ Runs both test projects with coverage
- ✅ Collects coverage data in multiple formats
- ✅ Generates combined HTML reports
- ✅ Displays summary information

**Usage:**
```bash
# Run all tests with coverage
./run-coverage.ps1

# Skip specific test projects
./run-coverage.ps1 -SkipApplicationTests
./run-coverage.ps1 -SkipStreamingTests

# Open HTML report automatically
./run-coverage.ps1 -OpenReport
```

### Batch Script (`run-coverage.cmd`)

Simple Windows batch file wrapper for the PowerShell script.

## 🎯 Coverage Interpretation

### Integration Test Coverage Goals

For **integration tests**, coverage focuses on:

✅ **Critical Paths** - Key user journeys and API workflows  
✅ **Infrastructure** - Service startup, dependency injection  
✅ **Integration Points** - WebSocket connections, authentication  
✅ **Error Handling** - Exception scenarios and cleanup  

### Why 18% Line Coverage is Good for Integration Tests

Integration tests are **not meant for exhaustive coverage**. They test:

- 🎯 **End-to-end workflows** rather than individual methods
- 🔒 **Authentication and authorization** paths
- 🌐 **WebSocket connection lifecycle**
- 🏗️ **Application startup and configuration**
- 🔄 **Service integration points**

For comprehensive line coverage, **unit tests** should be added to complement these integration tests.

## 📁 Output Files

Coverage artifacts are generated in multiple formats:

```
coverage/
├── application/           # Application test coverage
│   └── coverage.opencover.xml
├── streaming/            # Streaming test coverage  
│   └── coverage.opencover.xml
└── html-report/          # Combined HTML report
    ├── index.html        # Main report
    ├── Summary.txt       # Text summary
    └── ...              # Detailed coverage files
```

## 🚧 Excluded from Coverage

- `[*]*Tests*` - Test projects themselves
- `[*]*Test*` - Test-related files
- `[*]*.Migrations.*` - Database migrations
- Generated logging code (auto-excluded by file filters)

## 🎉 Next Steps

1. **Add Unit Tests** - For comprehensive method-level coverage
2. **CI/CD Integration** - Automate coverage collection in build pipeline
3. **Coverage Badges** - Display coverage metrics in README
4. **Threshold Enforcement** - Set appropriate coverage goals for different test types

---

*Generated on $(date) - FeatBit Evaluation Server Next* 