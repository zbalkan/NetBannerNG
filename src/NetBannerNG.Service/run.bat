@echo off

echo Administrative permissions required. Detecting permissions...

net session >nul 2>&1
if %errorLevel% == 0 (
	echo Success: Administrative permissions confirmed.
) else (
	echo Failure: Current permissions inadequate.
	pause >nul
	EXIT /B 1
)  

echo Running the application as SYSTEM in new window
psexec -s -i -nobanner %~dp0\NetBannerNG.Service.exe