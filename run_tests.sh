#!/bin/bash
# FFT Color Mod - Test Runner
# This script ensures tests run with the exact correct command every time

echo -e "\033[33mRestoring all project packages...\033[0m"
dotnet restore FFTColorMod.csproj
dotnet restore FFTColorMod.Tests.csproj

echo -e "\033[33mBuilding main project...\033[0m"
dotnet build FFTColorMod.csproj --no-restore -c Debug

echo -e "\033[33mBuilding test project...\033[0m"
dotnet build FFTColorMod.Tests.csproj --no-restore -c Debug

# Check if the test DLL actually exists
TEST_DLL="bin/Debug/net8.0-windows/FFTColorMod.Tests.dll"
if [ ! -f "$TEST_DLL" ]; then
    echo -e "\033[31mERROR: Test DLL not found at $TEST_DLL\033[0m"
    echo -e "\033[31mTrying to rebuild test project explicitly...\033[0m"
    dotnet build FFTColorMod.Tests.csproj -c Debug

    if [ ! -f "$TEST_DLL" ]; then
        echo -e "\033[31mFATAL: Test DLL still not found after rebuild!\033[0m"
        exit 1
    fi
fi

echo -e "\033[32mRunning tests...\033[0m"
dotnet test "$TEST_DLL" --verbosity minimal

echo -e "\n\033[36mTest run complete!\033[0m"