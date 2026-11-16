@echo off
rem Automated checks: build, then run the game headless through every check.
rem Exit code 0 = all passed. Details are the CHECK and SIM lines.
setlocal
set GODOT=C:\Program Files\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe
if not "%GODOT_EXE%"=="" set GODOT=%GODOT_EXE%
set FAILED=0

echo == build
dotnet build "%~dp0Deadset.sln" -nologo -v q || exit /b 1

echo == flags
"%GODOT%" --headless --path "%~dp0." -- --nowaves --weapon=rivet --mod=rivet_pierce:2 --upgrade=quick_loader --cash=100 --check=flags > "%TEMP%\deadset_flags.txt" 2>&1
if not %errorlevel%==0 set FAILED=1
findstr /b /c:"CHECK" "%TEMP%\deadset_flags.txt"

echo == reach, flank, weapons, trader, perf
"%GODOT%" --headless --path "%~dp0." -- --nowaves --check > "%TEMP%\deadset_check.txt" 2>&1
if not %errorlevel%==0 set FAILED=1
findstr /b /c:"CHECK" "%TEMP%\deadset_check.txt"

echo == short simulated run (nail gun, 2 waves)
"%GODOT%" --headless --path "%~dp0." -- --sim=nail --simspeed=4 --simwaves=2 > "%TEMP%\deadset_sim.txt" 2>&1
findstr /b /c:"SIM" "%TEMP%\deadset_sim.txt"
findstr /c:"SIM end: reached wave 2" "%TEMP%\deadset_sim.txt" >nul || set FAILED=1

if %FAILED%==0 (echo == ALL CHECKS PASSED) else (echo == SOME CHECKS FAILED)
exit /b %FAILED%
