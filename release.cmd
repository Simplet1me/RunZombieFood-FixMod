@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1"
set "result=%errorlevel%"
if not "%result%"=="0" echo Release build failed. See the error above.
if "%result%"=="0" echo Release build completed.
if /I "%~1"=="--pause" pause
exit /b %result%
