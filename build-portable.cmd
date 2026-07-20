@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-portable.ps1" %*
exit /b %ERRORLEVEL%
