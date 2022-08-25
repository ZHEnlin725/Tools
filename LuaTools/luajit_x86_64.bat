@echo off
cd /d %~dp0
cd /d ..\Assets\LuaBytes\

for /R %%i in (*.lua.bytes) do ( 
	echo %%i - luajit_x86_64
	..\..\LuaTools\luajit_x86_64\luajit.exe -b %%i %%i
)
pause