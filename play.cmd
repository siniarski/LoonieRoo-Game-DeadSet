@echo off
rem Runs Deadset with jump-in flags, for example:
rem   play.cmd --wave=5 --weapon=rivet --cash=800
rem   play.cmd --nowaves --god --weapon=cutter --spawn=emu:12
rem All flags are listed at the top of scripts\Debug\LaunchFlags.cs.
set GODOT=C:\Program Files\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe
if not "%GODOT_EXE%"=="" set GODOT=%GODOT_EXE%
dotnet build "%~dp0Deadset.sln" -nologo -v q || exit /b 1
start "" "%GODOT%" --path "%~dp0." -- %*
