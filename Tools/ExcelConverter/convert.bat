@echo off
chcp 65001 >nul
echo.
echo ═══════════════════════════════════════════════════════════
echo   CardMoba Excel → JSON 配置转换工具
echo ═══════════════════════════════════════════════════════════
echo.

cd /d "%~dp0"

REM 检查是否已编译
if not exist "bin\Release\net8.0\ExcelConverter.exe" (
    echo [编译] 首次运行，正在编译项目...
    dotnet build -c Release
    if errorlevel 1 (
        echo [错误] 编译失败，请检查 .NET SDK 是否已安装
        pause
        exit /b 1
    )
    echo.
)

REM 执行转换
echo [运行] 开始转换配置文件...
echo.
dotnet run -c Release --no-build -- "../../Config/Excel" "../../Client/Assets/StreamingAssets/Config"

echo.
pause
