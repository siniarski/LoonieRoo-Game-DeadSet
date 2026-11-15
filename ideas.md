# Ideas

Ideas waiting for review. Nothing here gets built until it's been picked.

## Enemy test roster (placeholder models, keys 1–0 in the test arena)

All of these share one chase-and-strike behaviour and differ only in model and tuning.
They exist so we can compare look and aim feel. Play them, then decide which to keep. The rest get deleted.

| Key | Enemy | Head height | Possible role | Behaviour it would need later |
|---|---|---|---|---|
| 1 | Emu | ~1.6 m | **Common swarm (chosen)** | — |
| 2 | Glow Bunny (upright) | ~1.5 m | Alternative common swarm | — |
| 3 | Feral (mutated townsfolk) | ~1.6 m | Alternative common enemy | Off-theme: leans on lore |
| 4 | Glow Bunny (low) | ~0.45 m | Low "crawler" special | Swarm under your crosshair |
| 5 | Kangaroo | ~1.65 m, bobs | Mid-tier bruiser | Hop-in, box, hop-out |
| 6 | Cassowary | ~1.75 m | Mid-tier tank | Kick knockback |
| 7 | Dingo | ~0.75 m | Low fast special | Pack flanking |
| 8 | Cane toad | ~0.3 m | Low crawler | Poison puddle on death? |
| 9 | Drop bear | ~1.0 m | Ambusher special | Drops from cover/trees |
| 0 | Magpie | ~2.4 m, flying | Overhead special | Swooping dive |

The wombat (charger) stays in the scope as Milestone 5 and isn't in this roster.

## Polish ideas from the long build session (not built)

- **Dash cooldown indicator** on the HUD (the dash itself is in, on Q, once you own Oi! Watch It!).
- **Armour-only hits** could flash blue instead of red, so you can tell armour soaked it.
- **Wombat:** a dust trail during the charge; screen shake when it slams into cover nearby.
- **Trader cards** are taller than they need to be; could show an icon or the owned-upgrade list.
- **Headshot kill pop:** the head bursts off (extra sparks and a different sound) for that KF feel.
- **Wave-end summary** in the break: kills, headshot %, cash earned that wave.

## From playtest 1 (2026-11-15): weapon variety and per-weapon upgrades (not built, needs a go-ahead)

You asked for more weapons, plus upgrades for a weapon after you buy it (for example ricochet to
nearby enemies, or a chance to pierce). Both are outside the prototype scope in CLAUDE.md
("other weapons and firearms" are listed as out of scope), so this is a scope change to decide on.

The code is already set up for it: the player tells the equipped weapon to fire, and each weapon
decides how. A small version would be:

1. **Two more tradie tools, sold in the trader**, each with one clear job next to the nail gun:
   - **Brushcutter** (melee/short cone, no ammo): for the emu crush at arm's length.
   - **Rivet gun** (slow, heavy, pierces in a line): for wombats and lined-up flocks.
   Switch with 1/2/3 or the scroll wheel; carry all you've bought.
2. **Weapon mods** as data (`WeaponMod` resources, like upgrades), offered in the trader for the
   weapon in your hands: Pierce (+1 target, -20% damage each), Chain ricochet (jumps to the
   nearest enemy within 4 m), Bigger mag, Faster fire. Bought with cash, so cash has a second use.
3. Keep the 1-of-3 player upgrades as they are.

Questions to settle first: buy weapons with cash or unlock them at set waves? Can mods be
bought more than once (stacking)?
