@echo off
cd /d %~dp0
cd /d ..\Assets\LuaBytes\

for /R %%i in (*.lua.bytes) do ( 
	echo %%i - luajit_x86
	..\..\LuaTools\luajit_x86\luajit.exe -b %%i %%i
)
pause