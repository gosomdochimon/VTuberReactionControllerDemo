@echo off
setlocal

set "PROJECT_ROOT=%~dp0.."
set "APP_PATH=%PROJECT_ROOT%\Builds\Windows\VTuberReactionControllerDemo.exe"

if not exist "%APP_PATH%" (
    echo VTuberReactionControllerDemo.exe was not found.
    echo Expected path:
    echo %APP_PATH%
    echo.
    echo Build the Windows Standalone app first, then run this file again.
    pause
    exit /b 1
)

pushd "%PROJECT_ROOT%"
start "" "%APP_PATH%" --obs-overlay -screen-fullscreen 0 -screen-width 960 -screen-height 540
popd

endlocal
