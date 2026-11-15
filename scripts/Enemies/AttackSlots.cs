using System.Collections.Generic;

namespace Deadset.Enemies;

/// <summary>
/// Limits how many enemies can be mid-attack (wind-up to recovery) at once, so a horde of
/// 200 surrounds the player without everyone landing a hit in the same second. Enemies
/// without a slot hold at reach and wait their turn. Solo for now: one shared pool.
/// </summary>
public static class AttackSlots
{
	/// <summary>Enemies allowed to attack at the same time. The WaveDirector sets this.</summary>
	public static int MaxAttackers { get; set; } = 6;

	private static readonly HashSet<Enemy> Holders = new();

	public static int InUse => Holders.Count;

	/// <summary>Takes a slot if one is free (or already held). Returns false if the enemy must wait.</summary>
	public static bool TryAcquire(Enemy enemy)
	{
		if (Holders.Contains(enemy))
			return true;
		if (Holders.Count >= MaxAttackers)
			return false;
		Holders.Add(enemy);
		return true;
	}

	public static void Release(Enemy enemy) => Holders.Remove(enemy);
}
