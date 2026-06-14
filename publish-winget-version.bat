@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-winget-version.ps1" %*
exit /b %ERRORLEVEL%
