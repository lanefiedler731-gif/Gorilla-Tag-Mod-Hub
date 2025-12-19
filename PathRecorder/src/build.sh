#!/bin/bash

echo "Building PathRecorder mod..."

# Build the project
dotnet build PathRecorder.csproj -c Release

if [ $? -eq 0 ]; then
    echo "Build successful!"
    echo "PathRecorder.dll has been copied to:"
    echo "/home/lane/.steam/debian-installation/steamapps/common/Gorilla Tag/BepInEx/plugins/"
else
    echo "Build failed!"
    exit 1
fi
