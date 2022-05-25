@echo off 
echo -----------------------开始生成LuaDataTable-----------------------
python xls2lua.py
echo 复制到LuaScript/Config
echo -----------------------开始拷贝LuaDataTable-----------------------
set CUR_DIR=%cd%
set LUA_CONFIG=lua_configs
set ASSET_CONFIG=Assets\LuaScripts\Configs
set SRC=%CUR_DIR%\%LUA_CONFIG%
set DST=%CUR_DIR%\..\%ASSET_CONFIG%
xcopy /y /e %SRC%\*.lua %DST%
pause