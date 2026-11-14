# Deadset – Prototype

## What this is

Deadset is a first-person wave shooter in the spirit of Killing Floor 1 and 2, set in a mutated, post-apocalyptic 1980s Australia. Mutant pests and native wildlife attack in waves; the player fights back with repurposed tradie tools.

This repo is a **prototype only**. Its single job is to answer: *is the core loop fun?* The full design (heroes, enemy roster, upgrade tags, lore) lives in a separate design note and is deliberately out of scope here.

The previous project failed because too many systems buried the fun. **Keep this lean.** If a feature isn't listed under Prototype scope, don't build it. Suggest it instead.

## Tech

- Godot 4 (.NET build), C#, desktop (Windows first).
- Placeholder art only: primitive meshes and flat colours. No asset imports yet.
- Keyboard and mouse controls.
- Solo play only. Don't add networking, but avoid choices that would make co-op impossible later (for example, keep game state out of UI scripts).

## Prototype scope

1. **Arena:** one small enclosed map with some cover and a few spawn points around the edges.
2. **Player (The Tradie):** first-person movement (walk, sprint, jump), mouse look, health.
3. **Weapon (Nail Gun):** projectile weapon with fire rate, magazine and reload. It must feel punchy: recoil, muzzle flash, hit feedback, sound placeholders.
4. **Enemies:**
   - **Emus (swarm):** fast, fragile, come in flocks, chase and peck. Mutated glowing heads sit at about eye height.
   - **Wombats (charger):** tanky; wind up, then charge in a straight line that the player can sidestep.
5. **Waves:** a wave director spawns escalating waves, then a short break between them.
6. **Trader break:** during the break, the player spends cash earned from kills on ammo and armour, then **picks 1 of 3 random upgrades**.
7. **Upgrades (start with about 6):** player-level, not tied to a weapon. Examples: Smoko Shield (damage resistance at low health), Hot Chips & Gravy (heal on wave end), Quick Loader (faster reload), Bouncing Shot (nails ricochet once), Oi! Watch It! (dash), Sprint boost.
8. **UI:** health, ammo, wave number, cash, a simple upgrade picker, and a game-over screen with restart.

## Design rules

- **Common enemies only need horizontal aim.** Swarm and fodder enemies keep their heads at about player eye height (1.4–1.7 m; the camera is at 1.6 m), so the crosshair can rest at head height like Killing Floor. Low, tall or flying enemies are rare specials that deliberately break the rhythm.
- **Heads are the skill target.** Head hits deal extra damage (one nail kills an emu; two to the body).

## Architecture rules

- Keep weapon behaviour separate from player input. The player tells the equipped weapon to fire; the weapon decides how. Later firing types (hitscan, cone, beam, melee, thrown) should slot in without touching player code.
- Define upgrades as data (Godot `Resource`s) that modify player stats or hook into events. Adding an upgrade should not require editing the player class.
- Define enemies with shared health and damage components plus a per-type behaviour script.
- Use signals and events for game flow (wave started, enemy killed, wave cleared) rather than direct cross-references.
- Keep tunable numbers (speeds, damage, wave sizes, prices) in exported properties or resources, not hard-coded.

## Suggested structure

```
res://
├── scenes/        (Main, Arena, Player, enemies, UI)
├── scripts/
│   ├── Player/
│   ├── Weapons/
│   ├── Enemies/
│   ├── Waves/
│   ├── Upgrades/
│   └── UI/
├── resources/     (upgrade and wave definitions)
└── audio/         (placeholder sounds)
```

## Build order

Each milestone should end in something playable. Stop after each one so I can test it.

1. Project setup, arena, first-person movement and mouse look.
2. Nail gun with feedback and a target dummy.
3. Swarm enemies (now Emus) chasing the player, plus health and damage on both sides.
4. Wave director with escalating waves and a break between them.
5. Wombats with the charge behaviour.
6. Cash, trader break and the 1-of-3 upgrade picker.
7. Game over, restart and a tuning pass.

## Working style

- Before each milestone, give a short plan and wait for my go-ahead.
- Afterwards, tell me exactly what to test and what to look for.
- Prioritise game feel over features. If something feels flat, say so and suggest a fix.
- Flag scope creep, including mine.

## Out of scope for now

Other heroes, other weapons and firearms, other enemy types, bosses, companions, meta-progression and the Shed, map unlocking and objectives, co-op, the lore, voice lines and final art.
