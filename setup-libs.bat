@echo off
REM Setup script to copy BepInEx and Unity DLLs to lib folder
REM Automatically searches for the game in common Steam library locations

echo Setting up BepInEx Trainer build environment...
echo.

REM Create lib folder
if not exist "lib" mkdir lib

REM Try to find the game in common Steam library locations
set "GAME_PATH="

REM Check common Steam library paths
for %%D in (C: D: E: F: G: H: I: J: K: L: M: N: O: P: Q: R: S: T: U: V: W: X: Y: Z:) do (
    if exist "%%D\SteamLibrary\steamapps\common\Flipping is Hard Demo\BepInEx" (
        set "GAME_PATH=%%D\SteamLibrary\steamapps\common\Flipping is Hard Demo"
        goto found
    )
    if exist "%%D\Program Files\Steam\steamapps\common\Flipping is Hard Demo\BepInEx" (
        set "GAME_PATH=%%D\Program Files\Steam\steamapps\common\Flipping is Hard Demo"
        goto found
    )
    if exist "%%D\Program Files (x86)\Steam\steamapps\common\Flipping is Hard Demo\BepInEx" (
        set "GAME_PATH=%%D\Program Files (x86)\Steam\steamapps\common\Flipping is Hard Demo"
        goto found
    )
)

:found
if not exist "%GAME_PATH%" (
    echo ERROR: Could not find game installation automatically.
    echo.
    echo Please make sure:
    echo   1. BepInEx is installed in your game folder
    echo   2. The game is in a standard Steam library location
    echo.
    echo If you have a custom Steam library path, edit this file and set GAME_PATH manually.
    echo.
    pause
    exit /b 1
)

echo Found game at: "%GAME_PATH%"
echo.

REM Copy BepInEx DLLs
echo Copying BepInEx DLLs...
if exist "%GAME_PATH%\BepInEx\core\BepInEx.dll" (
    copy "%GAME_PATH%\BepInEx\core\BepInEx.dll" "lib\"
    echo   - BepInEx.dll copied
) else (
    echo   - ERROR: BepInEx.dll not found
    echo   - Make sure BepInEx is installed in your game folder
)

REM Copy Unity DLLs
echo Copying Unity DLLs...
if exist "%GAME_PATH%\Flipping is Hard Demo_Data\Managed\UnityEngine.dll" (
    copy "%GAME_PATH%\Flipping is Hard Demo_Data\Managed\UnityEngine.dll" "lib\"
    echo   - UnityEngine.dll copied
) else (
    echo   - ERROR: UnityEngine.dll not found
)

if exist "%GAME_PATH%\Flipping is Hard Demo_Data\Managed\UnityEngine.CoreModule.dll" (
    copy "%GAME_PATH%\Flipping is Hard Demo_Data\Managed\UnityEngine.CoreModule.dll" "lib\"
    echo   - UnityEngine.CoreModule.dll copied
) else (
    echo   - ERROR: UnityEngine.CoreModule.dll not found
)

echo.
if exist "lib\BepInEx.dll" (
    echo Setup complete! You can now build the project.
    echo.
) else (
    echo ERROR: Setup failed. Check the errors above.
    echo.
    pause
)
