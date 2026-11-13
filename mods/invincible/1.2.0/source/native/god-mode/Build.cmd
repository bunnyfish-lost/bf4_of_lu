@echo off
if not defined VSCMD_VER call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
pushd "%~dp0"
lib /nologo /def:kernel32.def /machine:x64 /out:kernel32.lib
lib /nologo /def:user32.def /machine:x64 /out:user32.lib
lib /nologo /def:ntdll.def /machine:x64 /out:ntdll.lib
cl /nologo /O2 /W4 /utf-8 /GS- /GR- /LD Invincible.cpp /link /NODEFAULTLIB /ENTRY:DllMain /SUBSYSTEM:WINDOWS /INCREMENTAL:NO kernel32.lib user32.lib ntdll.lib /OUT:BF4InvincibleV2.dll
if errorlevel 1 exit /b 1
cl /nologo /O2 /W4 /utf-8 /GS- /GR- Test.cpp /link /NODEFAULTLIB /ENTRY:main /SUBSYSTEM:CONSOLE /INCREMENTAL:NO kernel32.lib user32.lib ntdll.lib /OUT:Test.exe
if errorlevel 1 exit /b 1
Test.exe
set "result=%errorlevel%"
type test-results.txt
popd
exit /b %result%
