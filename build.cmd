@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found: %CSC%
  exit /b 1
)

if not exist dmReplace.ico (
  "%CSC%" /nologo /codepage:65001 /r:System.Drawing.dll /out:"%TEMP%\dmReplace-MakeIcon.exe" tools\MakeIcon.cs || exit /b 1
  "%TEMP%\dmReplace-MakeIcon.exe" dmReplace.ico || exit /b 1
  del "%TEMP%\dmReplace-MakeIcon.exe"
)

"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /win32icon:dmReplace.ico /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:dmReplace.exe Program.cs Converter.cs || exit /b 1
echo Built dmReplace.exe
