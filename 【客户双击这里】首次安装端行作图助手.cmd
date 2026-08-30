@echo off
chcp 65001 >nul
title 端行智能作图助手首次安装
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\端行一键首次部署.ps1"
echo.
if errorlevel 1 (
    echo 安装没有完成，请拍下上方红色提示并联系实施人员。
) else (
    echo 安装已经完成。请打开新的 Codex 任务，拖入原图后直接说中文要求。
)
pause
