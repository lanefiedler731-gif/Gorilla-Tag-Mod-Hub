#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_BUILD_DIR="$ROOT_DIR/.build/plugin"
PAYLOAD_DIR="$ROOT_DIR/installer/Payload"
WIN_DIST_DIR="$ROOT_DIR/dist/win"
LINUX_DIST_DIR="$ROOT_DIR/dist/linux"

echo "==> Cleaning old artifacts"
rm -rf "$PLUGIN_BUILD_DIR" "$ROOT_DIR/dist"
mkdir -p "$PLUGIN_BUILD_DIR" "$PAYLOAD_DIR" "$WIN_DIST_DIR" "$LINUX_DIST_DIR"

echo "==> Building WalkSimModern.dll"
dotnet build "$ROOT_DIR/src/WalkSimModern.csproj" -c Release -o "$PLUGIN_BUILD_DIR"

echo "==> Exporting to Steam BepInEx"
STEAM_PLUGINS="/home/lane/.steam/debian-installation/steamapps/common/Gorilla Tag/BepInEx/plugins"
mkdir -p "$STEAM_PLUGINS"
cp "$PLUGIN_BUILD_DIR/WalkSimModern.dll" "$STEAM_PLUGINS/WalkSimModern.dll"

echo "==> Preparing installer payload"
cp "$PLUGIN_BUILD_DIR/WalkSimModern.dll" "$PAYLOAD_DIR/WalkSimModern.dll"

echo "==> Publishing Windows installer"
dotnet publish "$ROOT_DIR/installer/WalkSimInstaller.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$WIN_DIST_DIR"

echo "==> Publishing Linux tester build"
dotnet publish "$ROOT_DIR/installer/WalkSimInstaller.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$LINUX_DIST_DIR"

echo
echo "Windows installer: $WIN_DIST_DIR/WalkSimInstaller.exe"
echo "Linux tester: $LINUX_DIST_DIR/WalkSimInstaller"
