@echo off
REM
REM Simple batch file wrapper for code coverage script
REM
echo Running FeatBit Code Coverage Analysis...
powershell -ExecutionPolicy Bypass -File "%~dp0run-coverage.ps1" %* 