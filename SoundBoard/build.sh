#!/bin/bash

# SoundBoard Build Script
cd "$(dirname "$0")"

echo "Building SoundBoard..."

# Build the mod
cd src
dotnet build -c Release -o ../dist

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Build successful!"
    echo ""
    echo "Output: dist/SoundBoard.dll"
    echo ""
    echo "To install:"
    echo "1. Copy SoundBoard.dll to: <Gorilla Tag>/BepInEx/plugins/"
    echo "2. Launch Gorilla Tag"
    echo ""
    echo "Controls:"
    echo "  F1 - Toggle SoundBoard menu"
    echo "  F2 - Play selected sound"
    echo "  F3 - Stop playback"
    echo "  1-9 - Quick play sounds from playlist"
else
    echo ""
    echo "❌ Build failed!"
    exit 1
fi
