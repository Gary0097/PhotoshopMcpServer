@echo off
chcp 65001 >nul
title 安装端行 Codex 作图助手
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\安装端行作图助手.ps1"
echo.
pause
