@echo off
rem Deploys the existing web build (HorrorGame\Builds\Web, from build.cmd) to Cloudflare Pages via deploy.ps1.
rem First run asks for the Cloudflare account ID + API token and keeps them in Windows Credential Manager.
rem   deploy.cmd          deploy
rem   deploy.cmd reset    forget the saved token, ask again, deploy
setlocal
cd /d "%~dp0"
if /i "%1"=="reset" (set RESET=-Reset) else (set RESET=)
powershell -NoProfile -ExecutionPolicy Bypass -File deploy.ps1 %RESET%
echo.
pause
