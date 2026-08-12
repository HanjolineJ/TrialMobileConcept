# Audio

## Music_MainTheme.mp3

"Cut and Run" — Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

Attribution (include in game credits/about screen):
  Music: "Cut and Run" by Kevin MacLeod (incompetech.com)

## Replacing the track

1. Drop any loopable music file (`.mp3`, `.wav`, `.ogg`) into this folder.
2. Either name it `Music_MainTheme` (any extension), or select the
   **AudioManager** object in the Level_01 scene and assign your file to its
   **Music Track** field in the Inspector.
3. If you rebuild the scene via **TNT → Build Level 01 Scene**, the builder
   auto-wires the first audio clip it finds in this folder.

The `AudioManager` plays the track on game start, loops it, and survives scene
restarts (`DontDestroyOnLoad`). Default volume is 0.45 so sound effects stay
on top; adjust via `AudioManager.Instance.SetMusicVolume(0..1)`.
