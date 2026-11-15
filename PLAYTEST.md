# Playtest guide: full core loop

Branch `prototype-loop`. Open the project in Godot and press F5 (or run `Main.tscn`).

## Controls

| Key | Action |
|---|---|
| WASD, Shift, Space | Move, sprint, jump |
| Mouse, LMB, RMB | Look, fire, iron sights |
| R | Reload (also restarts on the game-over screen) |
| Enter | Ready up: skip the break countdown to 3 s |
| B | Close or reopen the trader during a break |
| Q | Dash (only after taking **Oi! Watch It!**) |
| Esc | Free the mouse (click to recapture) |
| F1 | Show the debug spawn legend (keys 1–0 and minus spawn test enemies) |

## The loop

1. A 6 s countdown, then **wave 1** (10 emus). The top of the screen shows the wave number and enemies left.
2. **Kills pay cash** (top right): emus $10, wombats $35, specials more.
3. **Wave cleared** opens the **trader**. Buy nails and armour, then **pick 1 of 3 upgrades**. You get 30 s, or press Enter.
4. Waves escalate. **Wombats** arrive from wave 3, and **dingoes, magpies, kangaroos and cassowaries** mix in later. After wave 7 it keeps going, with bigger counts.
5. Die and you get **"You carked it"** with a summary. R restarts.

## What to look for (please note your answers)

1. **Is the core loop fun?** Does wave → trader → wave pull you into "one more wave"?
2. **Emus:** does resting your crosshair at head height feel good? Are headshots satisfying (yellow hitmarker plus tink)?
3. **Wombat:** can you read the wind-up (growl, glowing patches, rearing) and sidestep the charge after the grunt? Is getting hit too punishing, or not enough?
4. **Trader:** are prices sensible? Do you ever run out of nails mid-wave? Is the upgrade choice interesting or obvious?
5. **Upgrades:** which ones feel good and which don't you notice? The dash on Q in particular.
6. **Pacing:** do waves drag, or end too fast? Is the 30 s break too long?
7. **Specials:** do magpies (overhead) and dingoes (low) break the rhythm in a fun way or an annoying way?

Anything that feels flat, tell me the moment and what you expected instead.
