@echo off
setlocal

set GAME_PLUGINS=I:\SteamLibrary\steamapps\common\Flipping is Hard Demo\BepInEx\plugins\FlippingIsHardTrainer

echo Building FlippingIsHardTrainer...
dotnet build -c Debug

if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    pause
    exit /b 1
)

echo.
echo Deploying to game...
if not exist "%GAME_PLUGINS%" mkdir "%GAME_PLUGINS%"
copy /Y "bin\Debug\net6.0\FlippingIsHardTrainer.dll" "%GAME_PLUGINS%\FlippingIsHardTrainer.dll"

echo.
echo Done! Plugin deployed to:
echo %GAME_PLUGINS%
pause
