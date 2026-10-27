# DESIGN - simple overview

A short explanation of how the project is put together. Game design is in `PLAN.md`, technical details in `CLAUDE.md`.

## The idea in one line
First-person horror: find the key in a chest, reach the exit door and escape a random maze while monsters hunt you. The maze gets bigger every level.

## Folders
```
PLAN.md, README.md, CLAUDE.md   design, setup, rules
build.cmd / deploy.cmd          web build (local server) / upload to Cloudflare Pages
HorrorGame/                     the Unity project
  Assets/
    Scenes/Game.unity           the ONLY scene
    Scripts/                    all the code (see below)
    Prefabs/                    Player, Wall, Floor, Monster, Chest, Locker, ExitDoor, Meat
    Settings/                   settings files with all the balance numbers (GameConfig, PlayerConfig, MonsterConfig...)
    Items/                      the items: Battery, Key, Meat, ShortcutKey, MapFragment
    Models/ Textures/ Materials/ Audio/ UI/   graphics, sound and HUD
```

## Scripts - one job per file
| Folder | Files | What they do |
|---|---|---|
| Core | `GameManager` | Runs the game: menu, pause, levels, lives, game over. The only singleton (one shared instance). |
| | `GameEvents` | Messages that all systems use to talk to each other. |
| | `Settings`, `Reward`, `RunStats` | Player settings (sensitivity, volume...), the reward choices and the stats shown at game over. |
| | `Fx`, `DebugCheats` | Small helpers (animations, sounds) and test cheats (god mode, fullbright). |
| Maze | `MazeGenerator` | Makes the maze as data (a grid of walls). |
| | `MazeBuilder` | Builds the walls and floor from that grid. |
| | `MazePlacement` | Pure maths: where chests, lockers, doors and monsters should go. |
| | `MazeSpawner` | Puts the objects in the chosen spots. |
| | `MazeMap`, `SafeZone` | The map (M) and the safe start room. |
| Player | `PlayerController` | Walking, sprinting, looking around, stamina, hiding. |
| | `PlayerCamera` | Head bob, camera shake, jump scare. |
| | `Flashlight` | The flashlight: battery, flicker, blackout. |
| | `Interactor` | Pressing E on things (anything that is an `IInteractable`). |
| | `PlayerInventory`, `Meat` | The hotbar (1-4, R) and throwing meat. |
| Monsters | `MonsterAI` | The monster brain, one mode at a time: Patrol, Chase, Check, Attack, Eat, Flee. |
| | `MonsterSenses` | Sight, light and sound: can the monster notice the player? |
| Interactables | `Door`, `Chest`, `Locker` | Exit and shortcut doors, chests with loot, lockers to hide in. |
| Audio | `Atmosphere` | Heartbeat and dark screen edges that grow as a monster gets closer. |
| UI | `HUD`, `MapView`, `MenuCamera` | All on-screen UI, the map, and the camera used in menus. |
| Data | `GameConfig`, `PlayerConfig`, `MonsterConfig`, `ItemData` | Settings files that only hold numbers, no logic. |

## How a level starts
`GameManager.StartLevel()`:
1. `MazeGenerator.Generate` makes the grid (size = starting size + level).
2. `MazePlacement.PickShortcuts` picks where the shortcut doors go.
3. `MazeBuilder.Build` builds the walls and floor.
4. The NavMesh is baked, so monsters know where they can walk.
5. `MazeSpawner.Spawn` places the door, key chest, chests, lockers and monsters.
6. The player is moved to the start and `GameEvents.LevelStarted` is sent (the HUD updates itself).

## The game loop
```
Menu -> play level -> find key -> hold E on the door -> walk through
                                                            |
     <- next level (bigger) <- pick a reward <--------------+
Caught by a monster -> lose a life -> back to the start
0 lives -> Game Over -> back to the menu
```

## Code rules
- **Messages, not direct calls:** gameplay never talks to the UI directly. It sends `GameEvents`, and the HUD listens.
- **Numbers in settings files:** balance (speed, battery, number of chests) lives in `Settings/`, not in the code.
- **References set in the Inspector**, no namespaces, input through `InputActionReference`.
- **As simple as possible**, no extra layers "just in case".
