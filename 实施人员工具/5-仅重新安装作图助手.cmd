@echo off
chcp 65001 >nul
title 重新安装端行作图助手
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\安装端行作图助手.ps1"
echo.
if errorlevel 1 (
    echo 安装没有完成，请查看上方红色提示。
) else (
    echo 安装和检查已经完成。
)
pause
