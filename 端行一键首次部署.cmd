@echo off
chcp 65001 >nul
title 端行 Codex 作图助手一键首次部署
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\端行一键首次部署.ps1"
echo.
if errorlevel 1 (
    echo 一键部署没有完成，请查看上方红色提示。
) else (
    echo 一键部署已经完成，可以打开新的 Codex 任务开始作图。
)
pause
