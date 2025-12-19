#!/bin/bash
# Build script for Gorilla Anti-Cheat

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$SCRIPT_DIR"
OUTPUT_DIR="/home/lane/.steam/debian-installation/steamapps/common/Gorilla Tag/BepInEx/plugins"

echo "========================================"
echo "  Gorilla Anti-Cheat Build Script"
echo "========================================"
echo ""

# Build the project
echo "[1/3] Building project..."
cd "$SRC_DIR"

dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "[ERROR] Build failed!"
    exit 1
fi

echo "[BUILD] Build successful!"

# Find the dll
DLL_PATH="$SRC_DIR/bin/Release/netstandard2.1/GorillaAntiCheat.dll"

if [ ! -f "$DLL_PATH" ]; then
    echo "[ERROR] DLL not found at $DLL_PATH"
    exit 1
fi

# Copy to plugins directory
echo ""
echo "[2/3] Copying to plugins directory..."

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

cp "$DLL_PATH" "$OUTPUT_DIR/"

echo "[DEPLOY] Copied to: $OUTPUT_DIR/GorillaAntiCheat.dll"

# Verify
echo ""
echo "[3/3] Verifying installation..."

if [ -f "$OUTPUT_DIR/GorillaAntiCheat.dll" ]; then
    echo "[SUCCESS] Anti-Cheat installed successfully!"
    echo ""
    echo "========================================"
    echo "  Installation Complete!"
    echo "========================================"
    echo ""
    echo "  Press F1 in-game for the control panel"
    echo ""
    ls -la "$OUTPUT_DIR/GorillaAntiCheat.dll"
else
    echo "[ERROR] Installation verification failed!"
    exit 1
fi
