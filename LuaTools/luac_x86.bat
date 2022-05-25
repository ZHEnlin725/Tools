@echo off
cd /d %~dp0
cd /d ..\Assets\LuaBytes\

for /R %%i in (*.lua.bytes) do ( 
	echo %%i 
	..\..\Tools\luac53_x86.exe -o %%i %%i
)
pause
