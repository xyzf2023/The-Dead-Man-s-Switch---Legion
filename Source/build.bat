@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo The Dead Man's Switch - Legion MOD 编译工具
echo ========================================
echo.

REM 检查dotnet命令是否可用
echo 检查.NET SDK...
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo 错误: 未找到 dotnet 命令，请确保已安装 .NET SDK
    echo 请访问: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

REM 预清理旧的编译产物
echo 清理旧的编译文件...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "obj_ce" rmdir /s /q "obj_ce"
echo ✓ 预编译清理完成！
echo.

REM 确保输出目录存在
if not exist "..\1.6\Assemblies" mkdir "..\1.6\Assemblies"
if not exist "..\CombatExtended\Assemblies" mkdir "..\CombatExtended\Assemblies"

set "CONFIG=Release"
set "MAIN_OK=0"
set "CE_OK=0"

REM ============================================================
REM  第一阶段：编译主模组 (DMS_Legion.dll)
REM ============================================================
echo ----------------------------------------
echo [1/2] 编译主模组 DMS_Legion.csproj ...
echo ----------------------------------------
dotnet build "DMS_Legion.csproj" --configuration %CONFIG% --verbosity normal

if %ERRORLEVEL% EQU 0 (
    set "MAIN_OK=1"
    echo ✓ 主模组编译成功
) else (
    echo ✗ 主模组编译失败！请检查上方错误信息。
)
echo.

REM ============================================================
REM  第二阶段：编译 CE 兼容层 (DMS_Legion_CE.dll)
REM  依赖主模组 DLL，因此必须在第一阶段成功后执行
REM ============================================================
if !MAIN_OK! EQU 0 (
    echo ----------------------------------------
    echo [2/2] 跳过 CE 编译（主模组编译失败）
    echo ----------------------------------------
    echo.
    goto :summary
)

if not exist "DMS_Legion_CE.csproj" (
    echo ----------------------------------------
    echo [2/2] 跳过 CE 编译（未找到 DMS_Legion_CE.csproj）
    echo ----------------------------------------
    echo.
    goto :summary
)

echo ----------------------------------------
echo [2/2] 编译 CE 兼容层 DMS_Legion_CE.csproj ...
echo ----------------------------------------
dotnet build "DMS_Legion_CE.csproj" --configuration %CONFIG% --verbosity normal

if %ERRORLEVEL% EQU 0 (
    set "CE_OK=1"
    echo ✓ CE 兼容层编译成功
) else (
    echo ✗ CE 兼容层编译失败！请检查上方错误信息。
    echo   提示：确认 CombatExtended.dll 的引用路径是否正确
)
echo.

REM ============================================================
REM  清理与汇总
REM ============================================================
:summary

REM 清理中间产物
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "obj_ce" rmdir /s /q "obj_ce"

REM 清理 PDB 文件
if exist "..\1.6\Assemblies\DMS_Legion.pdb" del /q "..\1.6\Assemblies\DMS_Legion.pdb"
if exist "..\CombatExtended\Assemblies\DMS_Legion_CE.pdb" del /q "..\CombatExtended\Assemblies\DMS_Legion_CE.pdb"

echo ========================================
echo  编译结果汇总
echo ========================================
echo.

if !MAIN_OK! EQU 1 (
    echo  [主模组]   ✓ 成功
    if exist "..\1.6\Assemblies\DMS_Legion.dll" (
        echo              ..\1.6\Assemblies\DMS_Legion.dll
    ) else (
        echo              警告：未在预期位置找到 DMS_Legion.dll
    )
) else (
    echo  [主模组]   ✗ 失败
)

if !CE_OK! EQU 1 (
    echo  [CE兼容层] ✓ 成功
    if exist "..\CombatExtended\Assemblies\DMS_Legion_CE.dll" (
        echo              ..\CombatExtended\Assemblies\DMS_Legion_CE.dll
    ) else (
        echo              警告：未在预期位置找到 DMS_Legion_CE.dll
    )
) else if !MAIN_OK! EQU 1 (
    echo  [CE兼容层] ✗ 失败
) else (
    echo  [CE兼容层] - 跳过
)

echo.
echo ========================================

if !MAIN_OK! EQU 1 if !CE_OK! EQU 1 (
    echo  全部编译成功！
) else if !MAIN_OK! EQU 1 (
    echo  主模组编译成功，CE 兼容层需要检查。
) else (
    echo  编译存在错误，请检查上方输出。
)

echo ========================================
echo.
pause
endlocal
