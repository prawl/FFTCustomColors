#!/bin/bash
# FFT Color Mod - Test Runner
# Reliable test execution with proper build order

echo -e "\033[33mCleaning build artifacts...\033[0m"
rm -rf bin obj

echo -e "\033[33mRestoring and building projects...\033[0m"
# Use the single test command which handles everything properly
dotnet test FFTColorMod.Tests.csproj --verbosity minimal

echo -e "\n\033[36mTest run complete!\033[0m"