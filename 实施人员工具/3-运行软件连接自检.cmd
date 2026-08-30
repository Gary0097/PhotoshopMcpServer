@echo off
chcp 65001 >nul
title 端行软件连接自检
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\运行端行Adobe现场自检.ps1"
echo.
if errorlevel 1 (
    echo 自检没有完成，请查看上方红色提示。
) else (
    echo Photoshop 和 Illustrator 自检已经完成。
)
pause
