@echo off
chcp 65001 >nul
title 端行完整作图流程自检
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\运行端行完整流程自检.ps1"
echo.
if errorlevel 1 (
    echo 完整流程自检没有完成，请查看上方红色提示。
) else (
    echo 端行完整作图流程自检已经完成。
)
pause
