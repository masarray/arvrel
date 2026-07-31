@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-sibling.ps1" %*
exit /b %ERRORLEVEL%
