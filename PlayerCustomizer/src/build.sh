#!/bin/bash

# Build script for Player Customizer

echo "================================"
echo "Building Player Customizer..."
echo "================================"

cd "$(dirname "$0")"

# Clean previous build
echo "Cleaning previous build..."
dotnet clean PlayerCustomizer.csproj

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore PlayerCustomizer.csproj

# Build the project
echo "Building project..."
dotnet build PlayerCustomizer.csproj -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "================================"
    echo "Build successful!"
    echo "================================"
    echo ""
    echo "DLL copied to: /home/lane/.steam/debian-installation/steamapps/common/Gorilla Tag/BepInEx/plugins/"
    echo ""
    echo "To use the mod:"
    echo "1. Launch Gorilla Tag"
    echo "2. Press F3 to open the customizer"
    echo "3. Change your name, join rooms, or customize your color"
    echo ""
else
    echo ""
    echo "================================"
    echo "Build failed!"
    echo "================================"
    exit 1
fi
