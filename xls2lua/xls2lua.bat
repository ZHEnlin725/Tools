@echo off 
echo -----------------------��ʼ����LuaDataTable-----------------------
python xls2lua.py
echo ���Ƶ�LuaScript/Config
echo -----------------------��ʼ����LuaDataTable-----------------------
set CUR_DIR=%cd%
set LUA_CONFIG=lua_configs
set ASSET_CONFIG=Assets\LuaScripts\Configs
set SRC=%CUR_DIR%\%LUA_CONFIG%
set DST=%CUR_DIR%\..\%ASSET_CONFIG%
xcopy /y /e %SRC%\*.lua %DST%
pause