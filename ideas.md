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
| 5 | Kangaroo | ~1.65 m, bobs | Mid-tier bruiser | Hop-in, box, hop-out (BUILT 2026-11-16) |
| 6 | Cassowary | ~1.75 m | Mid-tier tank | Kick knockback (BUILT 2026-11-16) |
| 7 | Dingo | ~0.75 m | Low fast special | Pack flanking (BUILT 2026-11-16: always flanks) |
| 8 | Cane toad | ~0.3 m | Low crawler | Poison puddle on death? |
| 9 | Drop bear | ~1.0 m | Ambusher special | Drops from cover/trees |
| 0 | Magpie | ~2.4 m, flying | Overhead special | Swooping dive (BUILT 2026-11-16) |

The wombat (charger) stays in the scope as Milestone 5 and isn't in this roster.

## Polish ideas from the long build session (not built)

- BUILT: dash bar, armour flash, wave summary (c8903ec); head pop and wombat dust/shake (2026-11-16).
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

## From playtest 3 (2026-11-15): lanes are too easy to exploit (not built, brainstorm)

The open yard is fine, but standing at the end of a lane lets you funnel a flock into a line and
farm it. Rule of thumb to aim for: **no spot on the map should be fed from only one direction.**

Ideas to explore:
- **Loops instead of dead ends:** every lane has two exits, so a flock can split and come round
  behind you (Container Alley could open at both ends and halfway on both sides).
- **Vents and skylights:** enemies drop in through roof hatches, drains, gaps under containers or
  holes in the shed roof, right next to a camper.
- **Wall and roof climbers:** an enemy type that runs over containers and walls instead of round
  them (a goanna or possum special), so cover isn't a wall at your back.
- **Flankers:** BUILT 2026-11-16 (play card "Flankers"). A quarter of each wave paths round to a spot
  behind the player, away from the rest of the pack.
- **Spawn awareness:** BUILT 2026-11-16 (play card "Spawns come round behind a camper"). When the player has been holding one spot for a while, favour spawns and
  entry points that come at it from behind.
- **Breakable cover:** enemies (wombats) knock down besser walls or barricades you're hiding behind.

## Variants instead of plain health growth (BUILT 2026-11-16: every variant below, plus all five ranged ideas; see the four play cards. Simplified: the casque has no rearing neck window, the bulldozer shrugs off cover instead of breaking it (there is no breakable cover), and the howler hurries everyone nearby instead of calling a new group)

Today, from wave 6 every enemy just gets +10% body health a wave (up to a cap) and looks a bit
wider ("chunky"). Heads stay one-shot. The ask: replace most of that with **visible variants of the
same animals**, each with a tell you can read at a glance and a different answer.

Rules of thumb for every variant:
- **One readable tell** (gear, colour, size, sound) visible from 15 m, so you know what's coming before it reaches you.
- **One changed answer**: aim somewhere else, use a different weapon, dodge differently, or kill it first.
  Variants should make the weapons' jobs matter: rivets punch armour, the brushcutter shreds
  light gear, the nail gun picks weak spots.
- **Common enemies keep heads at eye height.** A variant may cover the head, but there's always a
  way to hit it (shoot the gear off, go round it).
- Waves bring variants in gradually (a share per wave, like flankers), instead of the health ramp.
  A small health bump can stay for the very late waves.

### Emu (swarm)
- **Collared emu:** a corrugated-iron collar round the neck. Nails glance off it; aim at the head above it or shoot the collar off (2–3 nails, it clangs off and spins away).
- **Glowneck:** the head glows hot. One-shot as usual, but it bursts on death and splashes nearby emus (a chain-kill reward for headshots in a crowd).
- **Scrawny sprinter:** thinner and faster, comes in a burst ahead of the flock. Dies to anything, but you have to track it.
- **Old bull:** bigger, scarred, keeps coming after a body hit that would stagger others. Rare, one per flock.

### Wombat (charger)
- **Armoured wombat:** sheet-metal and tyre-rubber plates bolted on its face and flanks. Shots from the front ping off; sidestep the charge and shoot the soft back, or shoot the plates off (rivets crack them in one or two). Plates fall off as visible chunks.
- **Bulldozer:** heavier armour on the front; its charge smashes through light cover (barricades, pallets) instead of stunning on it.
- **Burrower:** digs in (dust mound you can see moving), then pops up close and charges from short range. You track the mound, not the wombat.
- **Mortar wombat** (ranged, below).

### Kangaroo (bruiser)
- **Boxer (red headband):** more aggressive. A two-punch combo, shorter hop-out, comes back faster. Same health. The headband tells you to keep your distance.
- **Big Red:** taller and broader, a big kick that knocks you down (a short stagger). Slow to recover after a miss, which is your window.
- **Joey-pouch:** when it dies, a joey hops out and runs at you (small, fast, low, a deliberate break from the eye-height rule).
- **Bandaged buck:** wrapped forearms that block body shots while it guards; open the guard by hitting the head or waiting for its punch.

### Cassowary (tank)
- **Casque helmet:** a bony crest helmet that covers the head from the front. Its weak spot is the bright blue neck, which it only bares when it rears for the kick.
- **Enraged:** red wattles flare at half health. Faster and kicks twice; kill it fast or fight it from cover.
- **Brooder:** walks with 2–3 chicks; kill the chicks and it goes berserk, leave them and it stays slow and guards them.

### Dingo (flanking pack)
- **Alpha (studded collar):** the pack waits for it. Kill the alpha first and the rest scatter and come in one at a time; kill it last and they hit together.
- **Mangy runner:** patchy coat, very fast, very fragile. Bites and backs off.
- **Howler:** stops at range and howls; it calls the rest of the pack towards you (and brings a flanking group). A priority target.

### Magpie (swooper)
- **Cable-tie magpie:** spiky cable ties on its head (flipping the Aussie anti-swoop helmet). A headshot from the front is blocked, so hit it in the side as it pulls out.
- **Squadron:** 2–3 dive one after another from different sides. Each dive is easy; the rhythm is the danger.
- **Bomber magpie** (ranged, below).

## Ranged attacks (BUILT 2026-11-16 as variants: mortar wombat, spitter emu, bomber magpie, rock-kicking kangaroo, howler dingo)

Rules so ranged doesn't get annoying (the classic KF complaint about husks):
- **Rare and a variant, never the basic enemy.** At most 2 shooting at once (an "attack slot" cap, like the 6 melee slots).
- **They stop to shoot,** so they're easy targets while they do it, and the shot has a clear tell (a sound and a glow before it's thrown).
- **Slow, visible, dodgeable projectiles,** in arcs, not hitscan. Sidestepping or cover always works.
- **They still close in eventually.** A ranged enemy that kites forever is a chore.

Ideas, roughly from most to least fitting:
1. **Mortar wombat:** wombats famously make cube-shaped poo. This one stops, rears up and lobs a glowing cube in a high arc. A circle marks where it lands; it splashes for damage. Pairs with the armoured wombat for late waves.
2. **Spitter emu:** the glowing head pulses, then it spits a gob of glow-bile in a short arc (10–15 m). It leaves a puddle that hurts for a few seconds, so it denies ground and pushes you off a camping spot.
3. **Bomber magpie:** circles high and drops stones or nuts, with a shadow on the ground under the drop. Fits its overhead role, and it's a reason to look up and shoot it.
4. **Rock-kicking kangaroo:** when it's out of reach it kicks gravel and stones at you with a back-foot flick (a short-range spread). It covers the gap while it hops back in.
5. **Howler dingo:** not a projectile, but a ranged support call (above).

## Suggested first slice (if you want to try the idea before committing)

- Variant plumbing: a `EnemyVariant` resource (stat overrides, an attachment mesh, optional armour plates) and a per-wave variant share in the wave set. It replaces the chunky health ramp for the specials and wombats.
- **Armoured wombat** (armour plates you shoot off), **Boxer kangaroo** (red headband, combo) and **Collared emu**: one variant per role, enough to judge whether variants beat health growth.
- **Mortar wombat** or **Spitter emu** as the one ranged test.

Scope note: variants of existing enemies fit the prototype scope; ranged attacks change the design
rules (today every enemy is melee). That's your call.
