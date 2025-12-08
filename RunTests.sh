#!/bin/bash
# FFT Color Mod - Test Runner
# This script ensures tests run with the exact correct command every time
# Exits immediately if any step fails

set -e  # Exit on any error

echo -e "\033[33m[1/3] Restoring packages...\033[0m"
dotnet restore FFTColorMod.Tests.csproj
if [ $? -ne 0 ]; then
    echo -e "\033[31mERROR: Failed to restore packages\033[0m"
    exit 1
fi

echo -e "\033[33m[2/3] Building test project...\033[0m"
dotnet build FFTColorMod.Tests.csproj
if [ $? -ne 0 ]; then
    echo -e "\033[31mERROR: Failed to build test project\033[0m"
    exit 1
fi

echo -e "\033[32m[3/3] Running tests...\033[0m"
dotnet test FFTColorMod.Tests.csproj --no-build --verbosity minimal > test_output.tmp 2>&1
TEST_RESULT=$?

# Display only the test summary line if file exists
if [ -f test_output.tmp ]; then
    grep -E "Failed:|Passed!" test_output.tmp || cat test_output.tmp
    rm -f test_output.tmp
else
    echo -e "\033[31mERROR: Test output file not created\033[0m"
fi

if [ $TEST_RESULT -ne 0 ]; then
    echo -e "\033[31mERROR: Tests failed\033[0m"
    exit 1
fi

echo -e "\n\033[36m✓ All tests passed!\033[0m"