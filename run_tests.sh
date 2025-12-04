#!/bin/bash
# FFT Color Mod - Test Runner
# This script ensures tests run with the exact correct command every time

echo -e "\033[33mRestoring test project packages...\033[0m"
dotnet restore FFTColorMod.Tests.csproj

echo -e "\033[33mBuilding test project...\033[0m"
dotnet build FFTColorMod.Tests.csproj --no-restore

echo -e "\033[32mRunning tests...\033[0m"
dotnet test FFTColorMod.Tests.csproj --verbosity minimal --no-build

echo -e "\n\033[36mTest run complete!\033[0m"