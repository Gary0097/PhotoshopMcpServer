@echo off
chcp 65001 >nul
title 检查端行 Codex 作图环境
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\检查端行作图环境.ps1"
echo.
pause
