using Godot;

namespace Deadset.Enemies;

/// <summary>
/// A visible version of an enemy (an armoured wombat, a boxer kangaroo...). Later waves bring
/// these in instead of only piling on health. Every variant has one readable tell (its gear)
/// and one changed answer (armour to shoot off, a different move, a ranged attack).
/// Applied by whoever spawns the enemy, before it enters the tree (Enemy.ApplyVariant).
/// Behaviour fields are read by the behaviour scripts that support them; the rest ignore them.
/// </summary>
[GlobalClass]
public partial class EnemyVariant : Resource
{
	/// <summary>Short id for flags and logs, e.g. "armoured" (spawn with --spawn=wombat.armoured:2).</summary>
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";

	[ExportGroup("Stats")]
	[Export] public float HealthMultiplier { get; set; } = 1f;
	[Export] public float SpeedMultiplier { get; set; } = 1f;
	[Export] public float DamageMultiplier { get; set; } = 1f;
	[Export] public float CooldownMultiplier { get; set; } = 1f;
	[Export] public float BountyMultiplier { get; set; } = 1f;
	/// <summary>Overall size (model and collision). Heads move with it.</summary>
	[Export] public float Size { get; set; } = 1f;
	/// <summary>Extra width of the model only (a thin sprinter is under 1).</summary>
	[Export] public float Width { get; set; } = 1f;
	/// <summary>How much of a shove (hammer hit) moves it, on top of its type's own resistance.</summary>
	[Export] public float ShoveTaken { get; set; } = 1f;

	[ExportGroup("Gear")]
	/// <summary>Visible kit: headbands, collars, plates. Pieces with Armour are plates you shoot off.</summary>
	[Export] public Godot.Collections.Array<GearPiece> Gear { get; set; } = new();

	[ExportGroup("Melee (chasers)")]
	/// <summary>Strikes per attack (2 = a one-two).</summary>
	[Export] public int ComboHits { get; set; } = 1;
	[Export] public float BackOffMultiplier { get; set; } = 1f;
	[Export] public float RecoverMultiplier { get; set; } = 1f;
	[Export] public float KnockbackMultiplier { get; set; } = 1f;
	/// <summary>Below this share of health it enrages (0 = never).</summary>
	[Export(PropertyHint.Range, "0,1")] public float EnrageAtHealth { get; set; }
	[Export] public float EnrageSpeedMultiplier { get; set; } = 1.4f;
	[Export] public int EnrageComboHits { get; set; } = 2;
	/// <summary>Speed while calm (a brooder guarding chicks). Enraging lifts it.</summary>
	[Export] public float CalmSpeedMultiplier { get; set; } = 1f;
	/// <summary>Hits and backs off after every bite (a mangy runner).</summary>
	[Export] public float HitAndRunTime { get; set; }

	[ExportGroup("Pack")]
	/// <summary>Leads its pack: packmates move with it and only close in when it does.</summary>
	[Export] public bool PackLeader { get; set; }

	[ExportGroup("Spawns")]
	/// <summary>Comes with these (a brooder's chicks). They use this variant on the same enemy scene.</summary>
	[Export] public EnemyVariant Escort { get; set; }
	[Export] public int EscortCount { get; set; }
	/// <summary>Enrages when one of its escorts dies.</summary>
	[Export] public bool EnrageWhenEscortDies { get; set; }
	/// <summary>Leaves these behind when it dies (a joey from the pouch).</summary>
	[Export] public EnemyVariant SpawnOnDeath { get; set; }
	[Export] public int SpawnOnDeathCount { get; set; }

	[ExportGroup("Death")]
	/// <summary>A head-shot kill bursts and hurts enemies within this radius (a glowneck).</summary>
	[Export] public float HeadPopSplashRadius { get; set; }
	[Export] public float HeadPopSplashDamage { get; set; }

	[ExportGroup("Wombat")]
	/// <summary>Ramming cover doesn't stun it: it backs off and goes again (a bulldozer).</summary>
	[Export] public bool NoStunOnCover { get; set; }
	/// <summary>Travels underground towards the player and pops up close to charge.</summary>
	[Export] public bool Burrows { get; set; }

	[ExportGroup("Magpie")]
	/// <summary>Dives one after another with the other squadron birds instead of on its own timer.</summary>
	[Export] public bool Squadron { get; set; }

	[ExportGroup("Ranged")]
	[Export] public RangedAttack Ranged { get; set; }
}
