# Playtest cards

Each feature you can feel gets a card. Play the **Unplayed** ones (newest first), then tell me a
verdict for each: **fun / dull / tweak / cut**, plus a line on why. I move it to **Played**.

## How to jump in

From the project root, `play.cmd` builds and starts the game with jump-in flags:

```
play.cmd --wave=5 --weapon=rivet --cash=800
```

| Flag | Does |
|---|---|
| `--wave=N` | Start at wave N after a 3 s countdown |
| `--trader` | Open the trader break before that wave instead |
| `--cash=N` | Start with N extra cash |
| `--weapon=nail\|cutter\|rivet` | Own that weapon and hold it (repeatable; the last one is in your hands) |
| `--mod=ID[:LEVELS]` | Give a weapon mod, for example `nail_pierce:2` (ids in `resources/weapons/mods/`) |
| `--upgrade=ID[:N]` | Give a player upgrade, for example `oi_watch_it` (ids in `resources/upgrades/`) |
| `--spawn=ENEMY:N` | N enemies in a ring 8 m around you (`emu`, `wombat`, `dingo`, `kangaroo`, ...) |
| `--at=SPOT` | Start at `alley`, `shed`, `tank`, `north`, `south` or `start` |
| `--god` | Take no damage |
| `--nowaves` | No waves: an empty arena for `--spawn` tests |

## Unplayed

### Vents (2026-11-16)
- What changed: a few enemies can climb out of glowing floor grates near you after a 1.3 s rattle; much more likely if you camp in one spot for 6 s.
- Fun bet: camping a lane stops being a free farm; you have to keep an ear out and move.
- Try it: `play.cmd --wave=5 --at=alley --weapon=rivet`
- Judge: when a vent rattles, do you notice it in time and feel it was fair, or does it feel cheap?

### Lanes opened up (2026-11-16)
- What changed: Container Alley has a back lane behind the outer row and gaps on both sides; the shed has a third door (south).
- Fun bet: a lane is a place to fight from for a bit, not a funnel you can stand at the end of forever.
- Try it: `play.cmd --wave=6 --at=alley`
- Judge: standing in the alley, do enemies now reach you from more than one side?

### Brushcutter nerf (2026-11-15)
- What changed: $300 (was $150), 16 damage (was 22), 3 enemies per swing (was 4).
- Fun bet: still a lifesaver at arm's reach, but no longer the obvious first buy.
- Try it: `play.cmd --nowaves --god --weapon=cutter --spawn=emu:12`
- Judge: does clearing a ring of 12 emus feel good but take real effort?

### Nail gun mods toned down (2026-11-15)
- What changed: pierce max 2 levels, damage kept per pierce 50% (+10% per level, max 2), ricochet one level at half damage.
- Fun bet: mods help, but the rivet gun stays the line-clearer.
- Try it: `play.cmd --wave=6 --cash=2000 --trader`
- Judge: after buying all nail mods, is the nail gun still single-target first?

### Rivet gun (2026-11-15)
- What changed: a $300 slow, heavy gun whose rivets pass through 2 enemies.
- Fun bet: lining a flock up and punching through it feels great.
- Try it: `play.cmd --wave=4 --weapon=rivet --at=alley`
- Judge: is it worth $300 next to more nails and armour?

### Hit-direction markers (2026-11-15)
- What changed: red wedges round the crosshair point at whatever just hurt you (blue while armour soaks it).
- Fun bet: you stop dying to things you never saw.
- Try it: `play.cmd --nowaves --spawn=emu:6`
- Judge: do you turn the right way when hit from behind?

### Death tumbles (2026-11-15)
- What changed: killed enemies are knocked back, topple and sink instead of vanishing.
- Fun bet: kills feel heavier.
- Try it: `play.cmd --nowaves --god --spawn=emu:10`
- Judge: do kills feel more satisfying, or does the clutter get in the way?

### Emu glow dimmed up close (2026-11-15)
- What changed: emu head glow fades to 40% within about 2 m of you.
- Fun bet: a crowd in your face is easier to read.
- Try it: `play.cmd --nowaves --god --spawn=emu:12`
- Judge: can you still pick heads out when they're on top of you?

## Played

### Bore Yard arena (2026-11-15) — tweak
- Said: "feels alright; ok to stay in the open, but in a lane it's easy to funnel enemies and lazily kill them as they line up."
- Follow-up: Lanes opened up and Vents (above).

### Weapons and mods (2026-11-15) — tweak
- Said: the brushcutter is "really good (too good) starting out"; the nail gun "seems OP once it has a few pierce and ricochet upgrades".
- Follow-up: Brushcutter nerf and Nail gun mods toned down (above).

### Footsteps (2026-11-15) — fun
- Said: "the sounds for movement are great."

### 10% slower movement (2026-11-15) — fun
- Said: "slower movement feels better."

### Horde waves and the core loop (2026-11-15) — fun
- Said: "I'm having some fun." Got snuck up on several times (footsteps fixed that); in-wave spawns felt a bit quick (slowed by 50%, then the bigger arena).

---

## Reference

### Controls

| Key | Action |
|---|---|
| WASD, Shift, Space | Move, sprint, jump |
| Mouse, LMB, RMB | Look, fire, iron sights |
| R | Reload (also restarts on the game-over screen) |
| 1 / 2 / 3, mouse wheel | Switch weapon |
| Enter | Ready up: skip the break countdown to 3 s |
| B | Close or reopen the trader during a break |
| Q | Dash (only after taking **Oi! Watch It!**) |
| Esc | Free the mouse (click to recapture) |
| F1 | Debug spawn legend (Ctrl + 1–0 and Ctrl + minus spawn test enemies) |
| F5 | Debug: spawn a mixed horde of 200 |

### The loop

1. Countdown, then **wave 1** (10 emus). Kills pay cash.
2. **Wave cleared** opens the **trader**: ammo, armour, weapons (Brushcutter $300, Rivet Gun $300), weapon mods, then **pick 1 of 3 upgrades**. 30 s, or press Enter.
3. Waves grow from 10 to 70 alive by wave 7, then +30 per wave up to **200 alive**. Wombats from wave 3; dingoes, magpies, kangaroos and cassowaries later. Only 6 attack at once.
4. The sky shifts from sunset to purple dusk as the waves go on. Die and R restarts.

### The arena: the Bore Yard (64 x 64 m)

- **Container Alley** (west): a 3.6 m lane between two container rows, a back lane behind the outer row, gaps on both sides, its own spawn gate at the north end.
- **Shearing Shed** (east): three doorways (west, north, south).
- **Bore tank** (centre-south): a tank ringed by troughs with gaps.
- **Yards:** wrecks, hay and a fuel tanker to the north; low besser walls, pallets and a ute to the south.
- **Windmill** landmark, six spawn gates (yellow frames, red pads) and eight vents (green grates).
