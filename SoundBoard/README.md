# 🎵 SoundBoard Mod for Gorilla Tag

A mod that allows you to play audio files from your PC both locally and broadcast them through your microphone to other players in the game.

## Features

- **File Browser**: Navigate your PC's file system to find audio files
- **Playlist Management**: Save your favorite sounds for quick access
- **Local Playback**: Hear the audio on your end
- **Mic Broadcasting**: Other players hear the audio as if it's coming from your microphone
- **Volume Controls**: Separate volume sliders for local and broadcast audio
- **Quick Hotkeys**: Number keys 1-9 for instant sound playback
- **Persistent Playlist**: Your playlist is saved between sessions

## Supported Audio Formats

- `.mp3` - MP3 audio files
- `.wav` - WAV audio files  
- `.ogg` - OGG Vorbis files

## Installation

1. Make sure BepInEx is installed in your Gorilla Tag folder
2. Copy `SoundBoard.dll` to `<Gorilla Tag>/BepInEx/plugins/`
3. Launch the game

## Controls

| Key | Action |
|-----|--------|
| **F1** | Toggle the SoundBoard menu |
| **F2** | Play the currently selected sound |
| **F3** | Stop playback immediately |
| **1-9** | Quick play sounds from playlist (position 1-9) |

## Usage

1. Press **F1** to open the SoundBoard menu
2. Click **"Browse for Audio File"** to open the file browser
3. Navigate to your audio files and click on them to add to your playlist
4. Select a sound from the playlist and click **"▶ Play"** or press **F2**
5. Toggle **"📡 Broadcast to Other Players"** to enable/disable mic injection

## Settings

- **Local Volume**: How loud the sound plays for you (0-100%)
- **Mic Volume**: How loud the sound is when broadcast to others (0-200%)
- **Broadcast Toggle**: Enable/disable broadcasting to other players

## How It Works

This mod leverages the Photon Voice system (the same system used for in-game voice chat) to inject your audio files directly into the voice stream. When broadcasting is enabled:

1. The audio file is loaded from your PC
2. It temporarily replaces your microphone input source with the audio file
3. The audio is transmitted to other players as if it were your voice
4. After playback, your microphone is restored to normal

## Building from Source

```bash
cd SoundBoard
./build.sh
```

The compiled DLL will be in the `dist/` folder.

## Credits

- Inspired by **WalkSimModern** and **RedLobbies** mods
- Uses Photon Voice for audio broadcasting
- Built with BepInEx and Harmony

## Disclaimer

Use this mod responsibly. Spamming audio or playing inappropriate content may result in reports from other players. Always respect the Gorilla Tag community guidelines.
