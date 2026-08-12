# Audio

## Music_MainTheme.mp3 — main theme (gameplay)

"Cut and Run" — Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

## Music_Ambience.mp3 — secondary layer (djinn magic / future menus)

"Myst on the Moor" — Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

Attribution (include in game credits/about screen):
  Music: "Cut and Run" and "Myst on the Moor" by Kevin MacLeod (incompetech.com)

## Replacing a track

1. Drop any loopable music file (`.mp3`, `.wav`, `.ogg`) into this folder.
2. Name it `Music_MainTheme` (gameplay) or `Music_Ambience` (secondary layer),
   any extension — or select the **AudioManager** object in the Level_01 scene
   and assign files to its fields in the Inspector.
3. If you rebuild the scene via **TNT → Build Level 01 Scene**, the builder
   auto-wires clips by those names (any clip as fallback for the main theme).

The `AudioManager` plays both layers on game start, loops them, and survives
scene restarts (`DontDestroyOnLoad`). Volumes: theme 0.45, ambience 0.18, so
sound effects stay on top. Adjust at runtime via
`AudioManager.Instance.SetMusicVolume(0..1)` / `SetAmbienceVolume(0..1)`.
