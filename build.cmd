@echo off
rem Fresh web build + local server: builds HorrorGame/Builds/Web with Unity, recreates the nginx container
rem and opens http://localhost:8080. Works while the editor is open: the build runs on a mirrored copy of
rem the project in %LOCALAPPDATA%\HorrorGameWebBuild (its own Library, so only the first build is slow).
rem The window stays open at the end so the result can be read.
rem   build.cmd          build, then serve
rem   build.cmd serve    only (re)start the server with the existing build
setlocal
cd /d "%~dp0"
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe"
set COPY=%LOCALAPPDATA%\HorrorGameWebBuild
set LOG=%~dp0HorrorGame\Builds\web-build.log

docker info >nul 2>&1 || (echo Docker Desktop is not running. Start it and try again. & goto end)

if /i "%1"=="serve" goto serve

rem Unity keeps its lockfile open while it runs: then a build is already going and we only follow it (build-web.ps1).
if exist "%COPY%\Temp\UnityLockfile" (
    2>nul (type nul >> "%COPY%\Temp\UnityLockfile") || goto build
)

echo Copying the project...
for %%D in (Assets Packages ProjectSettings) do (
    robocopy "HorrorGame\%%D" "%COPY%\%%D" /MIR /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 (echo Copy of %%D FAILED. & goto end)
)

:build
echo Building the web version (a few minutes, the first time longer; closing this window does not stop it)...
if not exist "HorrorGame\Builds" mkdir "HorrorGame\Builds"
powershell -NoProfile -ExecutionPolicy Bypass -File build-web.ps1 -Unity %UNITY% -Project "%COPY%" -Log "%LOG%"
if errorlevel 1 (
    echo.
    echo Build FAILED. Errors from %LOG%:
    findstr /i /c:"error" /c:"aborting" /c:"exception" "%LOG%" | findstr /v /i /c:"Start importing" /c:"Fail.png" /c:"Error.png"
    goto end
)
robocopy "%COPY%\Builds\Web" "HorrorGame\Builds\Web" /MIR /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (echo Copying the build FAILED. & goto end)
echo Build OK.

:serve
if not exist "HorrorGame\Builds\Web\index.html" (echo No web build found. Run build.cmd without arguments. & goto end)
docker compose down >nul 2>&1
docker compose up -d --force-recreate || goto end
echo Running on http://localhost:8080  (stop with: docker compose down)
start "" http://localhost:8080

:end
echo.
pause
