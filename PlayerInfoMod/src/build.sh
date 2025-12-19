#!/bin/bash

# Build script for Player Info Mod

echo "================================"
echo "Building Player Info Mod..."
echo "================================"

cd "$(dirname "$0")"

# Clean previous build
echo "Cleaning previous build..."
dotnet clean PlayerInfoMod.csproj

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore PlayerInfoMod.csproj

# Build the project
echo "Building project..."
dotnet build PlayerInfoMod.csproj -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "================================"
    echo "✅ Build successful!"
    echo "================================"
    echo ""
    echo "DLL copied to: /home/lane/.steam/debian-installation/steamapps/common/Gorilla Tag/BepInEx/plugins/"
    echo ""
    echo "To use the mod:"
    echo "1. Launch Gorilla Tag"
    echo "2. Press F5 to open the player info browser"
    echo "3. Click on any player to see ALL their information"
    echo "4. Press ESC to go back, F5 to close"
    echo ""
else
    echo ""
    echo "================================"
    echo "❌ Build failed!"
    echo "================================"
    exit 1
fi
