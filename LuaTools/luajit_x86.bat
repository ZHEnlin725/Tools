@echo off
cd /d %~dp0
cd /d ..\Assets\LuaBytes\

for /R %%i in (*.bytes) do ( 
	echo %%i 
	..\..\Tools\luajit_x86\luajit.exe -b %%i %%i
)
pause