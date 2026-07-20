@echo off
chcp 65001 >nul
title Claude 中文包安装（免 Python）
cd /d "%~dp0"

echo.
echo ========================================
echo   Claude Desktop 中文包
echo   免 Python 版（PowerShell）
echo ========================================
echo.
echo 请先完全退出 Claude（托盘图标也退出）。
echo 若提示权限不足，请右键本文件 - 以管理员身份运行。
echo.
pause

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-zh-cn-no-python.ps1"
set ERR=%ERRORLEVEL%
echo.
if %ERR%==0 (
  echo 安装完成。请重新打开 Claude。
) else (
  echo 安装失败，错误码 %ERR%。
  echo 常见原因：1^) 未以管理员运行  2^) 未安装 Claude  3^) 缺少 translation-memory.json
)
echo.
pause
exit /b %ERR%
