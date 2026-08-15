@echo off
setlocal ENABLEDELAYEDEXPANSION
REM Put the below in your post-Build event command line in your project properties:
REM Replace {pluginPath} with your path to your (development?) launchbox plugin directory e.g. "C:\LaunchBox\Plugins\MyPlugin"
REM Replace {buildPath} with the relative path from the solution folder to your debug build files e.g. "src\RommStar.Core\bin\Debug\net9.0"
REM call "$(SolutionDir)build\scripts\moveDebugBuild.bat" "$(ConfigurationName)" "$(SolutionDir)" "{pluginPath}" "{buildPath}"

set "ConfigurationName=%~1"
set "SolutionDir=%~2"
set "PluginPath=%~3"
set "BuildPath=%~4"

echo  -------------- Starting PLUGIN Post-build Script --------------

:: ------ COPY PLUGIN FOLDER IN DEBUG MODE ------------

if [!ConfigurationName!]==[Debug]  (
	echo Build is [debug], therefore copying build from debug folder to Launchbox plugin path: !PluginPath!
	if not exist "!PluginPath!" (
		echo Creating plugin path: !PluginPath!
		mkdir "!PluginPath!"
	)
	robocopy "!SolutionDir!\!BuildPath!" "!PluginPath!"
)

exit /b 0