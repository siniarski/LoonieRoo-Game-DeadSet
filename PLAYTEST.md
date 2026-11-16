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
| `--spawn=ENEMY:N` | N enemies in a ring 8 m around you (`emu`, `wombat`, `dingo`, `kangaroo`, ...). `ENEMY.VARIANT:N` spawns a variant, e.g. `wombat.armoured:2` (ids in `resources/enemies/variants/`, named `enemy_variant.tres`) |
| `--at=SPOT` | Start at `alley`, `shed`, `tank`, `north`, `south` or `start` |
| `--god` | Take no damage |
| `--nowaves` | No waves: an empty arena for `--spawn` tests |
| `--noammo` | Every gun you carry starts empty (try the hero's melee) |
| `--novariants` | Waves use plain enemies only (compare with and without variants) |
| `--variants=X` | Every variant rolls at share X (0 to 1) from wave 1, e.g. `--variants=0.5` |
| `--camptime=S` | Seconds holding one spot before vents and spawns start coming at your back (default 6) |
| `--flankers=X` | Share of every wave that flanks, 0 to 1 (default ramps 0.1 to 0.25 by wave 6; `0` turns flanking off to compare) |
| `--sim=nail\|rivet\|cutter` | A bot plays the run and prints `SIM` lines (tuning data); watch it or add `--simspeed=3` |
| `--simwaves=N` | The bot quits after wave N (default 10) |

## Unplayed

### Variant behaviours (2026-11-16)
- What changed: variants that move or fight differently:
  - Emus: scrawny sprinter (thin, fast, fragile, yellow tag), glowneck (glowing head; a head kill bursts and hurts the flock round it), old bull (big, barbed-wire wrapped, tough, hard to shove).
  - Wombats: bulldozer (orange blade; ramming cover doesn't stun it), burrower (mud-caked, big claws; goes underground as a moving mound and pops up 7 m away into a charge).
  - Kangaroos: boxer (red headband and gloves; a one-two combo, comes back faster), Big Red (huge, knocks you further, slow to recover), joey-pouch (a joey hops out when it dies).
  - Cassowaries: short fuse (dark neck bands; flares red and goes berserk at half health), brooder (comes with 3 chicks and moves slowly; kill a chick and she goes berserk).
  - Dingoes: alpha (studded collar; its pack trails it and goes in together; kill it first and they hesitate and come one at a time), mangy runner (patchy, fast, bites and backs off).
  - Magpies: squadron (orange leg bands; take turns diving, one after another).
- Fun bet: you read the tell and change what you do (glowneck: wait for the flock to bunch; alpha: shoot it first; brooder: leave the chicks).
- Try it: `play.cmd --nowaves --spawn=dingo.alpha:1 --spawn=dingo:3` (swap in any `enemy.variant`; add `--god` to watch)
- Judge: which ones make you change what you do, and which ones are just a different hat?

### Ranged variants (2026-11-16)
- What changed: five variants attack from range. Each stops, glows and makes a sound before it throws, and the throws are slow arcs. At most 2 throw at once.
  - Mortar wombat (glowing cubes piled on its back): lobs a glowing cube high; a ring marks where it'll land.
  - Spitter emu (green throat): spits a short arc that leaves a stinging puddle.
  - Rock-kicking kangaroo (yellow bandana): flicks a spray of stones at you while it's out of reach.
  - Bomber magpie (stone in its claws): circles without diving and drops stones; a ring shows where.
  - Howler dingo (glowing blue throat): stops and howls, and everything nearby speeds up for 4 s.
- Fun bet: ranged threats make you move and pick targets, without turning the game into dodging bullets.
- Try it: `play.cmd --nowaves --spawn=wombat.mortar:2` (or `emu.spitter:3`, `kangaroo.rockkick:2`, `magpie.bomber:2`, `dingo.howler:1`)
- Judge: is it always clear something is about to hit you, and does sidestepping always work?

### Armour you shoot off (2026-11-16)
- What changed: some variants wear armour plates. Shots that hit a plate ping off (nails can ricochet off it if you have the mod). Rivets crack plates fast and punch through to what's behind. The brushcutter shreds light gear (wire, ties, wraps) but not metal. Enough damage knocks a plate off with a clang, and it spins away.
  - Armoured wombat: steel face and side plates. Bulldozer wombat: a big orange blade on its face.
  - Collared emu: a tin collar round the neck (aim at the head above it).
  - Bandaged buck kangaroo: wrapped forearms guarding the chest.
  - Casque cassowary: a bone helmet over the face (shoot it off, or hit the head from the side).
  - Cable-tie magpie: a cable-tie mask over the face.
- Fun bet: the rivet gun has a clear job against armour, and aiming round or through gear is a skill.
- Try it: `play.cmd --nowaves --spawn=wombat.armoured:2 --weapon=rivet --weapon=nail`
- Judge: do you read the armour and switch weapons or angle, or does it feel like a damage sponge?

### Variants replace most of the health ramp (2026-11-16)
- What changed: from wave 3 on, a growing share of each enemy type turns up as visible variants (23 of them, each with gear you can spot). The late-wave health ramp is now small (+4% a wave, max +40%, was +10% to +100%).
- Fun bet: late waves get harder because there's more to read and react to, not because everything takes more nails.
- Try it: `play.cmd --wave=7` (compare `play.cmd --wave=7 --novariants`), or `--wave=3 --variants=0.5` to see lots early.
- Judge: are late waves more interesting than before, or just busier?

### Specials get their own moves (2026-11-16)
- What changed: the four wave specials stop being reskinned emus.
  - Magpie (wave 4+): circles overhead, warbles, then dives straight at your face and climbs away. Sidestep or step back and it misses; shoot it on the way in.
  - Kangaroo (wave 5+): hops in, boxes you back a few metres, then hops out to come again.
  - Cassowary (wave 6+): a slower, clearer wind-up, then a kick that sends you flying.
  - Dingo (wave 3+): every dingo flanks, so they always come at you from the side the pack isn't on.
- Fun bet: each special forces a different reaction (dodge, keep distance, turn round) in the middle of the emu rhythm, instead of just being a tougher emu.
- Try it: `play.cmd --nowaves --spawn=magpie:2` (or `kangaroo:2`, `cassowary:1`, `dingo:4`; add `--god` to just watch)
- Judge: can you read and dodge a magpie dive? Does getting boxed/kicked away feel fair or just annoying?
- Doubt: in bot runs damage on waves 4–5 went up (rivet median 7 -> 5 waves), but the bot never dodges. If it's too much, lower the magpie's turn rate or the kangaroo's knockback.

### Spawns come round behind a camper (2026-11-16)
- What changed: once you've held one spot for about 6 s, new packs mostly come in from the spawn gates behind you (where you're not looking), not the one you're facing.
- Fun bet: a lane end stops being a safe farm; holding a spot is a choice with a cost, and moving resets it.
- Try it: `play.cmd --wave=4 --at=alley` (compare `--camptime=999` to turn it off)
- Judge: does it push you to move and turn around, or does it just feel like the game cheats?

### Wombat dust and slam shake (2026-11-16)
- What changed: a charging wombat kicks up a trail of pale dust, and when it rams cover near you (within 12 m) your view shakes.
- Fun bet: charges read from further away and feel heavy; a near miss into a wall feels like a near miss.
- Try it: `play.cmd --nowaves --spawn=wombat:2 --god` (stand by a wall and sidestep)
- Judge: can you spot a charge coming by the dust alone? Is the shake satisfying or annoying?

### Headshot kill pops the head (2026-11-16)
- What changed: killing with a head hit (nail, rivet or hammer) bursts the glowing head in a shower of chunks with a wet pop, and the body falls headless.
- Fun bet: headshots feel like a payoff, so you aim for heads because it's satisfying, not only for the damage.
- Try it: `play.cmd --nowaves --spawn=emu:12`
- Judge: does popping heads make you want to aim higher? Is the pop sound right?

### Flankers (2026-11-16)
- What changed: from wave 2 (one in ten, rising to one in four by wave 6), some enemies go the long way round and comes at you from behind (the side away from the rest of the pack), instead of queuing up in front.
- Fun bet: standing at the end of a lane is no longer a free farm; you have to check your back, and a lane is somewhere to fight from for a bit, not forever.
- Try it: `play.cmd --wave=4 --at=alley` (then compare `play.cmd --wave=4 --at=alley --flankers=0`)
- Warning: when flankers come round behind you, one of them calls out (an emu squawk from behind), about once per group.
- Judge: when an emu reaches you from behind, does it feel like you got outplayed (fair), or like it came from nowhere (cheap)? Do you hear the squawk in time to turn?

### Claw hammer: the Tradie's melee (2026-11-16)
- What changed: the Tradie always carries a claw hammer. Press V to whack, or just keep firing once your gun is completely empty. One head whack kills an emu, a body hit takes two, and each hammer kill pulls 3 nails back out for the nail gun. Hits knock enemies back, cancel an emu's peck mid wind-up (wombats barely budge) and jolt your view. Each future hero gets their own melee.
- Fun bet: running dry is a scramble, not a dead end. Whack, grab nails, keep shooting. Fixes a soft-lock where a nail-only player out of ammo couldn't finish the wave.
- Try it: `play.cmd --wave=3 --noammo`
- Judge: does the hammer feel punchy enough to rely on in a pinch, while still being something you'd rather not do?

### Late headshots stay one-shot (2026-11-16)
- What changed: late-wave toughness now only applies to body shots. A head is as easy to pop in wave 10 as in wave 1.
- Fun bet: keeps the head-height rhythm all game; late waves reward aim more, not less.
- Try it: `play.cmd --wave=10 --god`
- Judge: one nail per emu head again in late waves. Do the bodies still feel chunky?

### Chunkier late waves (2026-11-16)
- What changed: from wave 6, every enemy gets +10% health a wave (+50% by wave 10, max double) and up to 15% wider (not taller, heads stay at eye height).
- Fun bet: late waves feel heavier and take longer to chew through, instead of the same emus in bigger numbers.
- Try it: `play.cmd --wave=10 --god` (compare with `play.cmd --wave=1 --god`)
- Judge: do late waves feel heavier without feeling spongy? (Heads stay one-shot: see the card above.)

### Pricier final mod levels (2026-11-16)
- What changed: the last level of every 3-level mod costs double (now $360-715).
- Fun bet: the last upgrade is a big late-game buy you save for, so cash still matters after wave 8.
- Try it: `play.cmd --wave=8 --trader --cash=2500 --weapon=rivet`
- Judge: is saving up for a level 3 mod a satisfying goal, or just a tax?

### Drop Bear climbers (2026-11-16) — test-only, not in waves
- What changed: Drop Bears ignore paths and come straight at you, climbing up and over containers, the shed and walls; from the top they crouch and growl (0.45 s), then pounce on you for 15 damage, then sit dazed for 0.9 s.
- Fun bet: cover stops being a wall at your back; you have to watch the rooftops too.
- Try it: `play.cmd --nowaves --at=shed --spawn=dropbear:2` (add `--god` to just watch)
- Judge: is the crouch-and-growl enough warning to turn and shoot it mid-air? Should it join the waves as a rare special?

### Brushcutter trimmer line (2026-11-16)
- What changed: the brushcutter now runs on line: 36 per load, each enemy hit uses 1 (whiffs are free), reload is a 0.9 s bump feed. Also costs $300, 16 damage, 3 enemies per swing (was $150, 22, 4). New mods: Tough Line (chance a hit uses no line) and Razor Wire (+12% damage).
- Fun bet: still the panic button at arm's reach, but you can't just hold it down all wave; feeding line mid-crowd is a tense moment.
- Try it: `play.cmd --nowaves --god --weapon=cutter --spawn=emu:16`
- Judge: does running out of line in a crowd feel tense-good or annoying?

### Economy retune (2026-11-16)
- What changed: start with $200 (was $60); kill cash tapers from wave 6 down to 60% by wave 9.
- Fun bet: a first weapon or mod by wave 2-3, and still something worth buying late.
- Try it: `play.cmd` (a normal run; check what you can afford at the first two traders), then `play.cmd --wave=8 --trader --cash=1500`
- Judge: at each trader, is there a real choice, or is it either "can't afford anything" or "buy everything"?

### Rivet ammo (2026-11-16)
- What changed: carry 120 rivets (was 96), start with 48 (was 40).
- Fun bet: enough rivets to use it for a whole wave if you save it for lines and big targets.
- Try it: `play.cmd --wave=5 --weapon=rivet`
- Judge: do you run dry before the wave ends when you use it sensibly?

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
