@echo off
chcp 65001 >nul
title 修复端行 Adobe 自动控制
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\修复Adobe自动控制.ps1"
echo.
if errorlevel 1 (
    echo 修复没有完成，请查看上方红色提示。
) else (
    echo 修复检查已经完成。
)
pause
