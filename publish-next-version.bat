@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-next-version.ps1" %*
exit /b %ERRORLEVEL%
