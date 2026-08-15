::Error Thrown by Post-Build Script
@echo off
setlocal ENABLEDELAYEDEXPANSION

:: USER VARS
set logfile=BuildLog.txt
set IgnoreConfig=Debug

:: ===================================================================================

echo.
echo  -------------- Starting RommStar Version Update --------------

set "ConfigurationName=%~1"
set "SolutionDir=%~2"

:: Get in right Dir (Config Build Folder root)
cd ../build/release
if [!ConfigurationName!] == [Debug] (cd src/RommStar.Core/bin/Debug/net9.0-windows)

type NUL > %logfile%
call :log "Started: !DATE! !TIME!"
call :log  "Working Dir: !cd!"
call :log "Build Config Name: [!ConfigurationName!]"

set "version=0.0.0.0"

if exist "!SolutionDir!\rommstar.version.txt" (
    for /f "usebackq delims=" %%i in ("!SolutionDir!\rommstar.version.txt") do (
        set "version=%%i"
     
    )
) else (
    echo Version file not found: "!SolutionDir!\rommstar.version.txt"
)

call :log "Built Version: [!version!]"

call :log "Doing any Deployment Archive File operations..."

:: ------ PRODUCE DEPLOYMENT ARCHIVE ------------

if [!ConfigurationName!] == [%IgnoreConfig%] (
    call :log "Not produced for debug builds. Skipping."
    goto Skip_DeploymentAchive
) 

powershell -command "Compress-Archive -Path 'Unibox\*' -DestinationPath 'Unibox.zip' -Force"

call :log  "Archive created"

:Skip_DeploymentAchive

echo  -------------- Unibox Post-build Script Complete --------------
echo.

exit /b 0

REM --- Subroutine to Log to Console AND File ---
:Log
    REM %~1 gets the argument passed to the subroutine, removing quotes
    SET "MSG=%~1"

    REM Echo to the console
    ECHO !MSG!

    REM Append to the log file
    ECHO !MSG! >> %logfile%

    GOTO :EOF