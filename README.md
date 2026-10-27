# HorrorGame

A first-person horror game. Find the key, escape the maze and stay away from the monsters that hunt your flashlight. The maze is different every time. Design: `PLAN.md`.

## Setup
1. Install Unity **6000.3.27f1 LTS** via Unity Hub.
2. Open the `HorrorGame/` folder in Unity Hub.
3. Open `Assets/Scenes/Game.unity` and press Play.

## Controls
| Key | Action |
|---|---|
| WASD / left stick | Move |
| Mouse / right stick | Look |
| Shift | Sprint (stamina) |
| F / left mouse | Flashlight on/off |
| E | Interact (chest, key, locker, door, shortcut door) |
| 1-4 | Select hotbar slot (Battery, Meat, Shortcut Key, Key) |
| R | Use selected item (battery recharges, meat is thrown) |
| M | Toggle the full map (you stand still while it is open; monsters keep moving) |
| Esc / Start | Pause menu (resume, controls, settings, exit to menu, quit) |
| F1 / F2 (web: F8 / F9) | Cheats: god mode / fullbright |

## Build for Windows
1. In Unity: File > Build Profiles > Windows.
2. Only the scene `Assets/Scenes/Game.unity`, Development Build off.
3. Build. The game ends up in `HorrorGame/Builds/Windows/HorrorGame.exe`.

## Build for the web
Needs the Web Build Support module in Unity Hub.

**Important:** switch the platform to Web first (File > Build Profiles > Web > Switch Platform). Building while Windows is active makes the web version render black.

**Play it locally**
1. Start Docker Desktop.
2. Double-click `build.cmd`. It builds the game (the first time takes 10-20 minutes) and opens http://localhost:8080.
3. Other devices on your network can use `http://<pc-name>:8080`.
4. `build.cmd serve` restarts the server without building. `docker compose down` stops it.

Opening `index.html` directly does not work; it needs a server.

**Put it online (Cloudflare Pages)**
1. Create a Cloudflare API token with only Account > Cloudflare Pages > Edit.
2. Double-click `deploy.cmd`. The first time it asks for your account ID and token and saves them in Windows Credential Manager.
3. `deploy.cmd reset` forgets the saved login.

Cloudflare allows at most 25 MiB per file, so the web version uses smaller textures and audio.

**itch.io:** zip the contents of `Builds/Web` (with `index.html` at the top), upload it as "played in the browser" and set the size to 1280x720.

In the browser, click PLAY to capture the mouse. Esc releases it and pauses the game.

## Branches
`develop` for work, `main` for releases.

## Credits
Wall, floor and metal textures: Bricks075A, Concrete034 and Rust004 from [ambientCG](https://ambientcg.com), licensed CC0 1.0.
