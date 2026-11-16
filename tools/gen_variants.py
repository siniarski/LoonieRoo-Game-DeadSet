"""Writes the enemy variant resources (resources/enemies/variants/*.tres) and the wave set's
variant rules. Run from the project root: python tools/gen_variants.py

Each variant is plain data: stat multipliers, gear (primitive meshes, some of them armour
plates) and behaviour flags read by the enemy scripts. Gear offsets are in the attach node's
space; models face -Z.
"""
import os
import re

OUT = 'resources/enemies/variants'
SHAPES = {'box': 0, 'cyl': 1, 'torus': 2, 'sphere': 3, 'prism': 4}
KINDS = {'lob': 0, 'spread': 1, 'drop': 2, 'howl': 3}
CUES = {'none': 0, 'squeak': 1, 'nip': 2, 'peck': 3, 'thump': 4, 'squawk': 5, 'grunt': 6,
        'croak': 7, 'growl': 8, 'warble': 9, 'howl': 10, 'hork': 11}


def v3(t):
    return 'Vector3(%s, %s, %s)' % tuple(repr(float(x)) for x in t)


def col(t):
    return 'Color(%s, %s, %s, 1)' % tuple(repr(float(x)) for x in t)


def gear(shape, size, attach, offset=(0, 0, 0), color=(0.5, 0.5, 0.5), rot=(0, 0, 0), glow=False,
         armour=0, heavy=False, head=False, enraged=False):
    return dict(Shape=SHAPES[shape], Size=v3(size), Attach='NodePath("%s")' % attach, Offset=v3(offset),
                RotationDegrees=v3(rot), Color=col(color), Glow=glow, ShowWhenEnraged=enraged,
                Armour=float(armour), Heavy=heavy, CoversHead=head)


def ranged(kind, **kw):
    d = dict(Kind=KINDS[kind])
    for k, val in kw.items():
        if k == 'Cue':
            d[k] = CUES[val]
        elif k == 'Color':
            d[k] = col(val)
        elif k == 'LaunchOffset':
            d[k] = v3(val)
        else:
            d[k] = val
    return d


def fmt(val):
    if isinstance(val, bool):
        return 'true' if val else 'false'
    if isinstance(val, float):
        return repr(val)
    if isinstance(val, int):
        return str(val)
    if isinstance(val, str) and (val.startswith(('Vector3', 'Color', 'NodePath', 'ExtResource', 'SubResource'))):
        return val
    return '"%s"' % val


def write(enemy, vid, name, stats=None, gears=(), rng=None, refs=None, **flags):
    exts = ['[ext_resource type="Script" path="res://scripts/Enemies/EnemyVariant.cs" id="1_v"]',
            '[ext_resource type="Script" path="res://scripts/Enemies/GearPiece.cs" id="2_g"]']
    subs = []
    body = ['script = ExtResource("1_v")', 'Id = "%s"' % vid, 'DisplayName = "%s"' % name]
    for k, val in (stats or {}).items():
        body.append('%s = %s' % (k, fmt(float(val))))
    ids = []
    for i, g in enumerate(gears):
        sid = 'g%d' % i
        ids.append('SubResource("%s")' % sid)
        lines = ['[sub_resource type="Resource" id="%s"]' % sid, 'script = ExtResource("2_g")']
        lines += ['%s = %s' % (k, fmt(val)) for k, val in g.items()]
        subs.append('\n'.join(lines))
    if ids:
        body.append('Gear = Array[ExtResource("2_g")]([%s])' % ', '.join(ids))
    if rng:
        exts.append('[ext_resource type="Script" path="res://scripts/Enemies/RangedAttack.cs" id="3_r"]')
        lines = ['[sub_resource type="Resource" id="ranged"]', 'script = ExtResource("3_r")']
        lines += ['%s = %s' % (k, fmt(val)) for k, val in rng.items()]
        subs.append('\n'.join(lines))
        body.append('Ranged = SubResource("ranged")')
    for i, (field, other) in enumerate((refs or {}).items()):
        rid = '%d_ref' % (4 + i)
        exts.append('[ext_resource type="Resource" path="res://%s/%s.tres" id="%s"]' % (OUT, other, rid))
        body.append('%s = ExtResource("%s")' % (field, rid))
    for k, val in flags.items():
        body.append('%s = %s' % (k, fmt(val)))
    steps = len(exts) + len(subs) + 1
    text = '[gd_resource type="Resource" script_class="EnemyVariant" load_steps=%d format=3]\n\n' % steps
    text += '\n'.join(exts) + '\n\n'
    if subs:
        text += '\n\n'.join(subs) + '\n\n'
    text += '[resource]\n' + '\n'.join(body) + '\n'
    path = os.path.join(OUT, '%s_%s.tres' % (enemy, vid))
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    return path


os.makedirs(OUT, exist_ok=True)

RUST = (0.62, 0.42, 0.28)
STEEL = (0.55, 0.58, 0.62)
BLACK = (0.08, 0.08, 0.08)
RED = (0.85, 0.08, 0.06)
BONE = (0.86, 0.8, 0.66)

# ---- Emu (head: Visual/Striker/Head, striker at neck base) -----------------------------
write('emu', 'collared', 'Collared emu',
      stats=dict(BountyMultiplier=1.4),
      gears=[gear('cyl', (0.16, 0.3, 0), 'Visual/Striker', (0, 0.21, -0.05), (0.78, 0.8, 0.82), armour=30),
             gear('torus', (0.18, 0.035, 0), 'Visual/Striker', (0, 0.34, -0.06), RUST),
             gear('torus', (0.18, 0.035, 0), 'Visual/Striker', (0, 0.22, -0.05), RUST),
             gear('torus', (0.18, 0.035, 0), 'Visual/Striker', (0, 0.08, -0.04), RUST)])
write('emu', 'glowneck', 'Glowneck emu',
      stats=dict(BountyMultiplier=1.3),
      gears=[gear('torus', (0.11, 0.04, 0), 'Visual/Striker', (0, 0.36, -0.08), (1, 0.55, 0.12), glow=True),
             gear('torus', (0.12, 0.04, 0), 'Visual/Striker', (0, 0.2, -0.05), (1, 0.55, 0.12), glow=True),
             gear('sphere', (0.17, 0, 0), 'Visual/Striker/Head', (0, 0, 0), (1, 0.6, 0.15), glow=True)],
      HeadPopSplashRadius=3.0, HeadPopSplashDamage=60.0)
write('emu', 'sprinter', 'Scrawny sprinter',
      stats=dict(Width=0.78, Size=0.96, SpeedMultiplier=1.45, HealthMultiplier=0.5, BountyMultiplier=0.8),
      gears=[gear('box', (0.1, 0.1, 0.02), 'Visual/Striker', (0, 0.18, -0.13), (1, 0.9, 0.1), glow=True)])
write('emu', 'oldbull', 'Old bull emu',
      stats=dict(Size=1.1, HealthMultiplier=2.5, DamageMultiplier=1.4, BountyMultiplier=2.5, ShoveTaken=0.3),
      gears=[gear('torus', (0.45, 0.035, 0), 'Visual', (0, 0.95, 0), (0.25, 0.25, 0.25), rot=(8, 0, 0)),
             gear('torus', (0.42, 0.035, 0), 'Visual', (0, 0.78, 0), (0.25, 0.25, 0.25), rot=(-6, 0, 0)),
             gear('box', (0.04, 0.02, 0.3), 'Visual/Striker/Head', (0.07, 0.05, 0), BONE, rot=(0, 0, 30))])
write('emu', 'spitter', 'Spitter emu',
      stats=dict(BountyMultiplier=1.6),
      gears=[gear('sphere', (0.09, 0, 0), 'Visual/Striker', (0, 0.3, -0.13), (0.55, 1, 0.3), glow=True)],
      rng=ranged('lob', MinRange=6.0, MaxRange=16.0, Windup=0.7, Cooldown=4.5, FirstDelay=1.5, Damage=8.0,
                 FlightTime=0.9, SplashRadius=1.3, PuddleSeconds=3.0, PuddleDamagePerSecond=8.0,
                 Color=(0.55, 1, 0.3), ProjectileSize=0.25, LaunchOffset=(0, 1.62, -0.4), Cue='hork'))

# ---- Wombat (head: Visual/HeadPivot/Head) ----------------------------------------------
write('wombat', 'armoured', 'Armoured wombat',
      stats=dict(BountyMultiplier=1.6),
      gears=[gear('box', (0.8, 0.6, 0.08), 'Visual/HeadPivot', (0, 0, -0.38), STEEL, armour=120, heavy=True, head=True),
             gear('box', (0.08, 0.6, 1.0), 'Visual', (0.62, 0.85, 0), STEEL, armour=80, heavy=True),
             gear('box', (0.08, 0.6, 1.0), 'Visual', (-0.62, 0.85, 0), STEEL, armour=80, heavy=True),
             gear('torus', (0.3, 0.1, 0), 'Visual', (0, 1.42, 0.15), BLACK)])
write('wombat', 'bulldozer', 'Bulldozer wombat',
      stats=dict(HealthMultiplier=1.3, BountyMultiplier=2.0),
      gears=[gear('box', (1.1, 0.9, 0.12), 'Visual/HeadPivot', (0, -0.18, -0.44), (0.9, 0.62, 0.08), armour=220, heavy=True, head=True),
             gear('box', (0.1, 0.1, 0.5), 'Visual', (0.35, 1.1, -0.3), BLACK),
             gear('box', (0.1, 0.1, 0.5), 'Visual', (-0.35, 1.1, -0.3), BLACK)],
      NoStunOnCover=True)
write('wombat', 'burrower', 'Burrower wombat',
      stats=dict(BountyMultiplier=1.5),
      gears=[gear('box', (0.6, 0.14, 0.7), 'Visual', (0, 1.45, 0.1), (0.45, 0.3, 0.18)),
             gear('box', (0.75, 0.28, 0.55), 'Visual/HeadPivot', (0, 0.28, -0.08), (0.5, 0.34, 0.2), rot=(10, 0, 4)),
             gear('box', (0.5, 0.18, 0.08), 'Visual/HeadPivot', (0, 0.12, -0.37), (0.5, 0.34, 0.2), rot=(10, 0, 4)),
             gear('box', (0.2, 0.14, 0.06), 'Visual/HeadPivot', (-0.18, -0.15, -0.36), (0.42, 0.28, 0.16), rot=(0, 0, -12)),
             gear('prism', (0.1, 0.32, 0.1), 'Visual', (0.3, 0.2, -0.85), BONE, rot=(-70, 0, 0)),
             gear('prism', (0.1, 0.32, 0.1), 'Visual', (0.45, 0.2, -0.85), BONE, rot=(-70, 0, 0)),
             gear('prism', (0.1, 0.32, 0.1), 'Visual', (-0.3, 0.2, -0.85), BONE, rot=(-70, 0, 0)),
             gear('prism', (0.1, 0.32, 0.1), 'Visual', (-0.45, 0.2, -0.85), BONE, rot=(-70, 0, 0))],
      Burrows=True)
write('wombat', 'mortar', 'Mortar wombat',
      stats=dict(BountyMultiplier=1.8),
      gears=[gear('sphere', (0.22, 0, 0), 'Visual', (0, 1.05, 0.72), (0.6, 1, 0.35), glow=True),
             gear('box', (0.26, 0.26, 0.26), 'Visual', (0.12, 1.95, 0.2), (0.6, 1, 0.35), glow=True, rot=(20, 30, 0)),
             gear('box', (0.22, 0.22, 0.22), 'Visual', (-0.16, 1.92, 0.35), (0.6, 1, 0.35), glow=True, rot=(-15, 10, 25)),
             gear('box', (0.2, 0.2, 0.2), 'Visual', (0, 2.18, 0.28), (0.6, 1, 0.35), glow=True, rot=(5, 45, 10))],
      rng=ranged('lob', MinRange=9.0, MaxRange=26.0, Windup=1.0, Cooldown=4.5, FirstDelay=0.4, Damage=18.0,
                 FlightTime=1.5, SplashRadius=2.0, Color=(0.6, 1, 0.35), ProjectileSize=0.4, Cube=True,
                 LaunchOffset=(0, 1.1, 0.8), Cue='grunt'))

# ---- Kangaroo (head: Visual/Head, arms: Visual/Striker) --------------------------------
write('kangaroo', 'boxer', 'Boxer kangaroo',
      stats=dict(CooldownMultiplier=0.7, BountyMultiplier=1.5),
      gears=[gear('torus', (0.17, 0.06, 0), 'Visual/Head', (0, 0.07, 0.0), RED, rot=(-15, 0, 0)),
             gear('box', (0.05, 0.18, 0.03), 'Visual/Head', (0.06, 0.0, 0.17), RED, rot=(30, 0, 20)),
             gear('sphere', (0.17, 0, 0), 'Visual/Striker', (0.2, 0.02, -0.42), RED),
             gear('sphere', (0.17, 0, 0), 'Visual/Striker', (-0.2, 0.02, -0.42), RED)],
      ComboHits=2, BackOffMultiplier=0.5)
write('kangaroo', 'bigred', 'Big Red',
      stats=dict(Size=1.2, HealthMultiplier=2.2, DamageMultiplier=1.3, BountyMultiplier=2.5, ShoveTaken=0.4),
      gears=[gear('torus', (0.32, 0.05, 0), 'Visual', (0, 1.0, 0), (0.55, 0.4, 0.15)),
             gear('box', (0.12, 0.08, 0.04), 'Visual', (0, 1.0, -0.33), (0.85, 0.7, 0.2))],
      KnockbackMultiplier=1.6, RecoverMultiplier=2.0)
write('kangaroo', 'joey', 'Joey',
      stats=dict(Size=0.45, HealthMultiplier=0.12, SpeedMultiplier=1.25, DamageMultiplier=0.3, BountyMultiplier=0.2),
      BackOffMultiplier=0.5)
write('kangaroo', 'pouch', 'Joey-pouch kangaroo',
      stats=dict(BountyMultiplier=1.4),
      gears=[gear('sphere', (0.17, 0, 0), 'Visual', (0, 0.95, -0.2), (0.62, 0.42, 0.3)),
             gear('sphere', (0.08, 0, 0), 'Visual', (0, 1.13, -0.3), (0.55, 0.36, 0.25)),
             gear('prism', (0.04, 0.1, 0.03), 'Visual', (0.04, 1.22, -0.3), (0.55, 0.36, 0.25))],
      refs=dict(SpawnOnDeath='kangaroo_joey'), SpawnOnDeathCount=1)
write('kangaroo', 'bandaged', 'Bandaged buck',
      stats=dict(HealthMultiplier=1.2, BountyMultiplier=1.5),
      gears=[gear('box', (0.5, 0.36, 0.1), 'Visual/Striker', (0, 0, -0.22), (0.88, 0.85, 0.75), armour=60),
             gear('torus', (0.09, 0.03, 0), 'Visual/Striker', (0.17, 0, -0.2), (0.88, 0.85, 0.75), rot=(90, 0, 0)),
             gear('torus', (0.09, 0.03, 0), 'Visual/Striker', (-0.17, 0, -0.2), (0.88, 0.85, 0.75), rot=(90, 0, 0))])
write('kangaroo', 'rockkick', 'Rock-kicking kangaroo',
      stats=dict(BountyMultiplier=1.5),
      gears=[gear('torus', (0.13, 0.04, 0), 'Visual', (0, 1.47, -0.08), (1, 0.8, 0.1))],
      rng=ranged('spread', MinRange=5.0, MaxRange=14.0, Windup=0.6, Cooldown=4.0, FirstDelay=1.0, Damage=5.0,
                 Count=4, SpreadDegrees=14.0, FlightTime=0.55, SplashRadius=0.6, LandingMarker=False,
                 Color=(0.6, 0.5, 0.38), ProjectileSize=0.13, LaunchOffset=(0, 0.3, 0.2), Cue='grunt'))

# ---- Cassowary (head: Visual/Head) -----------------------------------------------------
write('cassowary', 'casque', 'Casque cassowary',
      stats=dict(BountyMultiplier=1.5),
      gears=[gear('box', (0.3, 0.3, 0.1), 'Visual/Head', (0, 0.02, -0.17), BONE, armour=90, heavy=True, head=True),
             gear('prism', (0.1, 0.3, 0.35), 'Visual/Head', (0, 0.22, 0), BONE)])
write('cassowary', 'enraged', 'Short-fused cassowary',
      stats=dict(BountyMultiplier=1.5),
      gears=[gear('torus', (0.13, 0.05, 0), 'Visual/Head', (0, -0.4, 0.05), (0.55, 0.05, 0.05)),
             gear('torus', (0.15, 0.05, 0), 'Visual/Head', (0, -0.6, 0.1), (0.55, 0.05, 0.05)),
             gear('sphere', (0.09, 0, 0), 'Visual/Head', (0.05, -0.2, -0.05), (1, 0.15, 0.1), glow=True, enraged=True),
             gear('sphere', (0.09, 0, 0), 'Visual/Head', (-0.05, -0.2, -0.05), (1, 0.15, 0.1), glow=True, enraged=True),
             gear('torus', (0.5, 0.05, 0), 'Visual', (0, 1.0, 0), (1, 0.15, 0.1), glow=True, enraged=True)],
      EnrageAtHealth=0.5, EnrageSpeedMultiplier=1.45, EnrageComboHits=2)
write('cassowary', 'chick', 'Cassowary chick',
      stats=dict(Size=0.4, HealthMultiplier=0.1, SpeedMultiplier=1.1, DamageMultiplier=0.25, BountyMultiplier=0.2),
      gears=[gear('box', (0.04, 0.5, 0.9), 'Visual', (0.2, 1.0, 0), (0.85, 0.75, 0.5)),
             gear('box', (0.04, 0.5, 0.9), 'Visual', (-0.2, 1.0, 0), (0.85, 0.75, 0.5))])
write('cassowary', 'brooder', 'Brooder cassowary',
      stats=dict(HealthMultiplier=1.2, BountyMultiplier=1.8),
      gears=[gear('box', (0.55, 0.16, 0.45), 'Visual', (0, 1.3, 0.1), (0.75, 0.62, 0.3)),
             gear('sphere', (0.09, 0, 0), 'Visual/Head', (0.05, -0.2, -0.05), (1, 0.15, 0.1), glow=True, enraged=True),
             gear('sphere', (0.09, 0, 0), 'Visual/Head', (-0.05, -0.2, -0.05), (1, 0.15, 0.1), glow=True, enraged=True)],
      refs=dict(Escort='cassowary_chick'), EscortCount=3, EnrageWhenEscortDies=True, CalmSpeedMultiplier=0.7,
      EnrageSpeedMultiplier=1.5, EnrageComboHits=2)

# ---- Dingo (head: Visual/Head) ---------------------------------------------------------
studs = [gear('sphere', (0.025, 0, 0), 'Visual', (0.13 * x, 0.66, -0.36 + 0.13 * z), (1, 0.8, 0.2), glow=True)
         for x, z in ((1, 0), (-1, 0), (0.7, -0.7), (-0.7, -0.7))]
write('dingo', 'alpha', 'Alpha dingo',
      stats=dict(Size=1.1, HealthMultiplier=1.6, BountyMultiplier=2.5),
      gears=[gear('torus', (0.14, 0.045, 0), 'Visual', (0, 0.66, -0.36), BLACK, rot=(-30, 0, 0))] + studs,
      PackLeader=True)
write('dingo', 'mangy', 'Mangy runner',
      stats=dict(Width=0.85, SpeedMultiplier=1.35, HealthMultiplier=0.5, BountyMultiplier=0.8),
      gears=[gear('box', (0.02, 0.14, 0.22), 'Visual', (0.15, 0.62, 0.05), (0.85, 0.65, 0.6)),
             gear('box', (0.02, 0.12, 0.16), 'Visual', (-0.15, 0.6, 0.25), (0.85, 0.65, 0.6)),
             gear('box', (0.18, 0.02, 0.14), 'Visual', (0, 0.74, 0.12), (0.85, 0.65, 0.6))],
      HitAndRunTime=0.7)
write('dingo', 'howler', 'Howler dingo',
      stats=dict(HealthMultiplier=1.2, BountyMultiplier=2.0),
      gears=[gear('torus', (0.13, 0.05, 0), 'Visual', (0, 0.66, -0.36), (0.5, 0.75, 1), glow=True, rot=(-30, 0, 0))],
      rng=ranged('howl', MinRange=6.0, MaxRange=26.0, Windup=1.2, Cooldown=7.0, FirstDelay=0.2,
                 HowlRadius=30.0, HowlSeconds=4.0, HowlSpeedMultiplier=1.35, Color=(0.5, 0.75, 1),
                 LaunchOffset=(0, 0.7, -0.45), Cue='howl'))

# ---- Magpie (head: Visual/Striker/Head) ------------------------------------------------
ties = [gear('prism', (0.03, 0.14, 0.03), 'Visual/Striker', (0.07 * x, 0.12, 0.05 * z), (0.3, 0.95, 0.3), rot=(20 * z, 0, -25 * x))
        for x, z in ((1, 0), (-1, 0), (0.5, 1), (-0.5, 1), (0, -1))]
write('magpie', 'cabletie', 'Cable-tie magpie',
      stats=dict(BountyMultiplier=1.4),
      gears=[gear('box', (0.3, 0.26, 0.06), 'Visual/Striker', (0, 0.02, -0.16), (0.3, 0.95, 0.3), armour=20, head=True)] + ties)
write('magpie', 'squadron', 'Squadron magpie',
      stats=dict(BountyMultiplier=1.3),
      gears=[gear('torus', (0.07, 0.025, 0), 'Visual', (0.08, 2.05, 0.05), (1, 0.5, 0.05), glow=True),
             gear('torus', (0.07, 0.025, 0), 'Visual', (-0.08, 2.05, 0.05), (1, 0.5, 0.05), glow=True)],
      Squadron=True)
write('magpie', 'bomber', 'Bomber magpie',
      stats=dict(BountyMultiplier=1.5),
      gears=[gear('sphere', (0.11, 0, 0), 'Visual', (0, 2.02, 0.05), (0.62, 0.6, 0.55))],
      rng=ranged('drop', MinRange=0.0, MaxRange=9.0, Windup=0.5, Cooldown=2.5, FirstDelay=2.0, Damage=10.0,
                 FlightTime=0.6, SplashRadius=1.2, Color=(0.72, 0.7, 0.62), ProjectileSize=0.22,
                 LaunchOffset=(0, 2.0, 0), Cue='squawk'))

# ---- Wave rules ------------------------------------------------------------------------
# (enemy, variant file, from wave, first share, growth per wave, max share, one per pack)
RULES = [
    ('Emu', 'emu_sprinter', 3, 0.06, 0.02, 0.15, False),
    ('Emu', 'emu_collared', 4, 0.06, 0.03, 0.2, False),
    ('Emu', 'emu_glowneck', 5, 0.05, 0.02, 0.12, False),
    ('Emu', 'emu_spitter', 5, 0.02, 0.01, 0.05, False),
    ('Emu', 'emu_oldbull', 7, 0.02, 0.01, 0.06, False),
    ('Wombat', 'wombat_armoured', 4, 0.25, 0.1, 0.5, False),
    ('Wombat', 'wombat_mortar', 6, 0.15, 0.05, 0.3, False),
    ('Wombat', 'wombat_bulldozer', 7, 0.15, 0.05, 0.3, False),
    ('Wombat', 'wombat_burrower', 8, 0.15, 0.05, 0.3, False),
    ('Kangaroo', 'kangaroo_boxer', 5, 0.3, 0.1, 0.5, False),
    ('Kangaroo', 'kangaroo_pouch', 6, 0.2, 0.05, 0.3, False),
    ('Kangaroo', 'kangaroo_bandaged', 7, 0.2, 0.05, 0.3, False),
    ('Kangaroo', 'kangaroo_rockkick', 7, 0.15, 0.05, 0.25, False),
    ('Kangaroo', 'kangaroo_bigred', 7, 0.1, 0.05, 0.2, False),
    ('Cassowary', 'cassowary_casque', 6, 0.3, 0.1, 0.5, False),
    ('Cassowary', 'cassowary_enraged', 7, 0.25, 0.05, 0.35, False),
    ('Cassowary', 'cassowary_brooder', 8, 0.2, 0.05, 0.3, False),
    ('Dingo', 'dingo_mangy', 3, 0.15, 0.05, 0.3, False),
    ('Dingo', 'dingo_alpha', 5, 0.5, 0.1, 0.8, True),
    ('Dingo', 'dingo_howler', 6, 0.08, 0.03, 0.15, False),
    ('Magpie', 'magpie_cabletie', 4, 0.25, 0.1, 0.5, False),
    ('Magpie', 'magpie_squadron', 6, 0.3, 0.1, 0.5, False),
    ('Magpie', 'magpie_bomber', 7, 0.2, 0.05, 0.3, False),
]

WAVES = 'resources/waves/DefaultWaves.tres'
s = open(WAVES, encoding='utf-8').read()
# Drop anything this script wrote before, then add it again.
s = re.sub(r'\[ext_resource type="Script" path="res://scripts/Waves/VariantRule.cs"[^\n]*\n', '', s)
s = re.sub(r'\[ext_resource type="Resource" path="res://resources/enemies/variants/[^\n]*\n', '', s)
s = re.sub(r'\[sub_resource type="Resource" id="vr_[^"]*"\]\n(?:[^\[\n][^\n]*\n)*\n', '', s)
s = re.sub(r'Variants = [^\n]*\n', '', s)
s = re.sub(r'(HealthGrowthPerWave|MaxHealthMultiplier) = [^\n]*\n', '', s)

ext = ['[ext_resource type="Script" path="res://scripts/Waves/VariantRule.cs" id="s_vrule"]']
subs, ids = [], []
for i, (enemy, vfile, frm, first, grow, mx, one) in enumerate(RULES):
    ext.append('[ext_resource type="Resource" path="res://%s/%s.tres" id="v_%s"]' % (OUT, vfile, vfile))
    sid = 'vr_%s' % vfile
    ids.append('SubResource("%s")' % sid)
    subs.append('\n'.join(['[sub_resource type="Resource" id="%s"]' % sid, 'script = ExtResource("s_vrule")',
                           'Enemy = "%s"' % enemy, 'Variant = ExtResource("v_%s")' % vfile, 'FromWave = %d' % frm,
                           'FirstShare = %r' % first, 'ShareGrowthPerWave = %r' % grow, 'MaxShare = %r' % mx,
                           'OnePerPack = %s' % ('true' if one else 'false')]) + '\n')

lines = s.split('\n')
last_ext = max(i for i, l in enumerate(lines) if l.startswith('[ext_resource'))
lines[last_ext + 1:last_ext + 1] = ext
s = '\n'.join(lines)
s = s.replace('\n[resource]\n', '\n' + '\n'.join(subs) + '\n[resource]\n', 1)
s = s.rstrip('\n') + '\nVariants = Array[ExtResource("s_vrule")]([%s])\n' % ', '.join(ids)
# Variants carry the late game now: only a small health ramp is left on top.
s += 'HealthGrowthPerWave = 0.04\nMaxHealthMultiplier = 1.4\n'
n_ext = s.count('[ext_resource')
n_sub = s.count('[sub_resource')
s = re.sub(r'load_steps=\d+', 'load_steps=%d' % (n_ext + n_sub + 1), s, count=1)
open(WAVES, 'w', encoding='utf-8', newline='\n').write(s)
print('wrote', len(os.listdir(OUT)), 'variants and', len(RULES), 'rules')
