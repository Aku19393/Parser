#!/usr/bin/env sh
# Сборка: ./build.sh - собрать программу в папку build/
set -e
cd "$(dirname "$0")"

dotnet build src/TaskParser/TaskParser.csproj -c Release -o build
