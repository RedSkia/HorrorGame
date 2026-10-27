# HorrorGame - Design

## Concept
A first-person horror game. You are trapped in a dark maze that is different every time, and monsters are hunting you. Your flashlight lets you see, but it also lets the monsters see you. Batteries, hiding and noise all play into that choice.

## Core loop
Find the key in a chest, walk to the exit door, hold E to unlock it and walk through. Every escape makes the next maze a bit bigger and lets you pick one reward (+1 life, meat, battery or shortcut key). The run ends when you have no lives left. Your best level is saved.

## Mechanics
- **Maze:** built randomly every level, with a few open rooms and loops so there is more than one way around.
- **Flashlight:** F turns it on and off. The battery runs down. Below 20% it flickers, below 10% it cuts out now and then. Monsters can see the light. A battery (R) recharges it.
- **Stamina:** sprinting uses stamina. When it is empty you cannot sprint until it comes back, and you start breathing heavily. Monsters can hear you sprint. Hiding lets you rest.
- **Hiding:** step into a locker and monsters cannot catch you. A monster that saw you go in may wait outside. Each locker works once: the door falls off when you leave.
- **Monsters:** they wander, chase you when they see you, search where they lost you and attack when close. There are three kinds: Normal, Blind (cannot see but hears far) and Light-shy (runs away from your flashlight). Deeper levels have more of them. Getting caught costs a life and gives you a jump scare.
- **Meat:** throw it (R) and monsters run to it and eat it, ignoring you for a while.
- **Chests:** they all look the same. One holds the key, most hold loot, some are rare and some are trapped (an alarm goes off).
- **Map fragments:** show the whole maze on the map (M).
- **Shortcut doors:** locked doors that open a shorter path. They need a shortcut key.
- **Exit door:** needs the key. Hold E to unlock it, then walk through.
- **Lives and heartbeat:** hearts in the corner show your lives. The closer a monster gets, the louder your heartbeat and the darker the edges of the screen.
- **Settings:** head bob, mouse sensitivity, field of view and volume.

## How it works
- **The maze:** it starts as a grid where every wall is standing. The computer walks through the grid in random directions and knocks down walls as it goes. When it hits a dead end it steps back and tries another way, until every room is connected. Then it knocks down a few extra walls to make loops and small rooms.
- **Placing things:** the exit door goes as far from the start as possible. Chests, lockers, shortcut doors and monsters are spread out so they are not bunched together. The start room is safe: monsters cannot enter it until you step out.
- **The player:** moves with WASD and looks with the mouse. The same script handles sprinting, stamina, footsteps and hiding.
- **Inventory:** four fixed slots (battery, meat, shortcut key, key). Keys 1-4 pick a slot, R uses it.
- **Light:** the scene is almost pitch black with fog, so your flashlight is the only real light. The walls and floor have textures that catch the light, which makes the beam look real. That darkness is most of the horror.
- **Monster brain:** a monster is always in one "mode" at a time: wander, chase, search, attack, eat or flee. Simple rules switch between them, for example "I see the player, so I chase". To find its way around walls it uses Unity's built-in pathfinding, like a GPS for the maze.
- **Interacting:** when you look at something and press E, the game checks what is in front of you (a chest, locker or door) and tells it to do its thing.
- **Messages between systems:** parts of the game do not talk to each other directly. Instead they send out messages like "the player was caught", and the camera, sound and screen each react on their own. That keeps every part simple and separate.
- **Game manager:** the boss of the game. It handles the menu, pause, levels, lives, rewards and game over.
- **Tuning:** numbers like monster speed, battery life and loot chances are kept in settings files, so the game can be balanced without touching the code.

## Tech
Unity 6, Universal Render Pipeline, Input System, AI Navigation, UI Toolkit for menus and HUD. One scene (`Game.unity`). Project layout and code rules: `CLAUDE.md`.
