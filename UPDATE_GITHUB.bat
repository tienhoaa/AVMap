@echo off
chcp 65001 >nul
title Cap Nhat AVMap Len GitHub
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update_github.ps1"

echo.
pause
