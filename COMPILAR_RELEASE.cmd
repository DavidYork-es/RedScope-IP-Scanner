@echo off
setlocal
title Compilar RedScope 2.1.0.3
cd /d "%~dp0"

set "RS_MSBUILD="
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2026\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2026\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if not defined RS_MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "RS_MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

set "RS_VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not defined RS_MSBUILD if exist "%RS_VSWHERE%" for /f "usebackq tokens=*" %%i in (`"%RS_VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined RS_MSBUILD set "RS_MSBUILD=%%i"

if not defined RS_MSBUILD (
  echo No se ha encontrado MSBuild.
  echo Instala Visual Studio o Build Tools con MSBuild y .NET Framework 4.8 Targeting Pack.
  pause
  exit /b 2
)

"%RS_MSBUILD%" "RedScope.sln" /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU" /m
if errorlevel 1 (
  echo.
  echo COMPILACION FALLIDA
  pause
  exit /b 1
)

echo.
echo COMPILACION CORRECTA
echo Ejecutable: RedScope\bin\Release\RedScope.exe
pause
endlocal
