@echo off
chcp 65001 >nul
title 验证端行客户中文回答
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\验证客户中文回答.ps1"
echo.
if errorlevel 1 (
    echo 验收没有通过，请查看上方红色提示。
) else (
    echo 客户中文回答已经通过验收。
)
pause
