@echo off
chcp 65001 >nul
title 端行 Codex 工具列表检查
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\验证端行MCP工具.ps1"
echo.
if errorlevel 1 (
    echo 检查没有通过，请联系实施人员。
) else (
    echo 检查通过，可以在 Codex 中开始作图。
)
pause
