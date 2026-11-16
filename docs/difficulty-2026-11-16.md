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

1. **Late cash still has no sink.** FIXED: level 3 mods cost double; leftover cash is now
   usually under $300 a trader. Options: a steeper taper (to 40%); higher prices for level 3
   mods; or buy the second weapon too (the bot only buys one).
2. **Running dry can soft-lock a wave.** FIXED: the Tradie's claw hammer (always on hand) plus
   3 nails back per hammer kill. Starting wave 3 with no ammo at all, the bot cleared it on
   hammer kills in 2 of 4 runs; no stalls in 42 runs since. With chunkier late waves, a
   nail-gun-only run fires all ~540 nails it can carry during wave 8 or 9, then has no way to
   kill what's left: no kills for 45 s, enemies alive right next to it, and the wave can't
   end until the player dies. 3 of 10 nail-only runs hit this. Needs a design call: a bigger
   nail reserve, ammo dropped by kills, or a weak melee you always have (like KF's knife).
3. **One run's log stopped mid-wave 4** (rivet loadout). CLOSED: not seen in 22 more rivet runs. Eight reruns did not reproduce it. The
   bot now reports a stall (no kill for 45 s mid-wave) with every live enemy's position, so if a
   wave ever can't end, the next run will say where the enemy is stuck.

## Flankers (added later the same day)

From wave 2, some of each wave path round to a spot behind the player. The `flank` check
(a 24-emu pack from the gate nearest each player spot) went from 0% of arrivals from behind
to 25% at every spot, Container Alley and the shed included (South excepted: its gate is
right next to it). Full logs: `sim-2026-11-16-flankers.txt`.

- A flat 25% from wave 2 hit hard: median waves cleared fell to nail 7, rivet 6, cutter 8, and
  4 of 24 runs died on wave 5.
- Ramping it (10% in wave 2, 25% by wave 6) flattened that: every loadout's median is 7
  (range 6-9), nothing dies before wave 6, no stalls in 18 runs.
- The bot fights all round equally well, so it can't tell how much harder flanking feels to a
  person who has to turn round. That's what the play card is for.
