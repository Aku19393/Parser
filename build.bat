@echo off
rem Usage: build.bat - build the program into the build folder
cd /d "%~dp0"

dotnet build src\TaskParser\TaskParser.csproj -c Release -o build
