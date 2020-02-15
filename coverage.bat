@echo off

if not exist ".\packages\OpenCover.4.7.922\tools\OpenCover.Console.exe" goto error1
if not exist ".\packages\Codecov.1.10.0\tools\codecov.exe" goto error2
if "%WINAPPDRIVER_DIR%" == "" goto error3
if not exist "%WINAPPDRIVER_DIR%/WinAppDriver.exe" goto error3
if "%VSTESTPLATFORM_DIR%" == "" goto error4
if not exist "%VSTESTPLATFORM_DIR%/VSTest.Console.exe" goto error4
if not exist ".\TaskbarIconHost\bin\x64\Debug\TaskbarIconHost.exe" goto error5
if not exist ".\Test-TaskbarIconHost\bin\x64\Debug\Test-TaskbarIconHost.dll" goto error6

if exist .\Test-TaskbarIconHost\obj\x64\Debug\Coverage-Debug_coverage.xml del .\Test-TaskbarIconHost\obj\x64\Debug\Coverage-Debug_coverage.xml

start cmd /k .\coverage\start_winappdriver.bat

call .\coverage\app.bat TaskbarIconHost Debug
call .\coverage\wait.bat 10

rem "%VSTESTPLATFORM_DIR%\VSTest.Console.exe" ".\Test-TaskbarIconHost\bin\x64\Debug\Test-TaskbarIconHost.dll" /Tests:Test1

start cmd /c .\coverage\stop_winappdriver.bat

call ..\Certification\set_tokens.bat
if exist .\Test-TaskbarIconHost\obj\x64\Debug\Coverage-Debug_coverage.xml .\packages\Codecov.1.10.0\tools\codecov -f ".\Test-TaskbarIconHost\obj\x64\Debug\Coverage-Debug_coverage.xml" -t "%TASKBARICONHOST_CODECOV_TOKEN%"
goto end

:error1
echo ERROR: OpenCover.Console not found. Restore it with Nuget.
goto end

:error2
echo ERROR: Codecov uploader not found. Restore it with Nuget.
goto end

:error3
echo ERROR: WinAppDriver not found. Example: set WINAPPDRIVER_DIR=C:\Program Files\Windows Application Driver
goto end

:error4
echo ERROR: Visual Studio 2019 not found. Example: set VSTESTPLATFORM_DIR=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\Extensions\TestPlatform
goto end

:error5
echo ERROR: TaskbarIconHost.exe not built.
goto end

:error6
echo ERROR: Test-TaskbarIconHost.dll not built.
goto end

:end
