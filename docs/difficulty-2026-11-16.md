# Difficulty and economy report (2026-11-16)

Simulated player (`--sim`, smart bot): aims at heads with 0.22 m error and 0.35 s reaction, kites
for open space, shops by a fixed plan. 9 runs, 3 per loadout, up to wave 12. Raw log:
`sim-2026-11-16-smartbot.txt`. Not a substitute for a human verdict: it can't read tells (wombat
wind-up, vent rattle) and never uses the dash.

## Where runs end

| Loadout | Died on wave |
|---|---|
| Nail gun only | 8, 8, 9 |
| + Brushcutter | 7, 10, 10 |
| + Rivet gun | 9, 11, (one log stopped mid-wave 4, see below) |

The earlier "wall at waves 4-6" was the old bot backing into walls, not the waves. With
kiting, runs last 6-12 game minutes and end around waves 8-11. The three loadouts are within a
wave or two of each other, so no weapon is clearly dominant any more.

## Damage taken per wave (all runs)

| Waves | Typical damage taken |
|---|---|
| 1-3 | 0-36 |
| 4-5 | 0-155 (wide spread: a bad wombat or vent moment) |
| 6-7 | 15-130 |
| 8+ | 85-185, where runs end |

A steady climb without a cliff. Waves 4-5 have the widest spread.

## Economy

- Start: $200 + $96 from wave 1. The bot spends it all on nails and armour; a person could buy a
  $300 weapon at the first or second trader.
- Mid: waves 4-7 pay $418, $655, $742, $1,052. Mods are bought steadily.
- Late: the taper holds pay flat at about $1,250-1,300 per wave from wave 8. **Every mod for the
  bot's weapon is maxed by the wave 9 trader**, after which it banks $900-1,300 a wave with
  nothing to buy.

## Open issues

1. **Late cash still has no sink.** Options: a steeper taper (to 40%); higher prices for level 3
   mods; or buy the second weapon too (the bot only buys one).
2. **Running dry can soft-lock a wave (found later the same day).** With chunkier late waves, a
   nail-gun-only run fires all ~540 nails it can carry during wave 8 or 9, then has no way to
   kill what's left: no kills for 45 s, enemies alive right next to it, and the wave can't
   end until the player dies. 3 of 10 nail-only runs hit this. Needs a design call: a bigger
   nail reserve, ammo dropped by kills, or a weak melee you always have (like KF's knife).
3. **One run's log stopped mid-wave 4** (rivet loadout). Eight reruns did not reproduce it. The
   bot now reports a stall (no kill for 45 s mid-wave) with every live enemy's position, so if a
   wave ever can't end, the next run will say where the enemy is stuck.
