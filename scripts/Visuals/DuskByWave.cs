using Deadset.Core;
using Godot;

namespace Deadset.Visuals;

/// <summary>
/// Darkens the look as the run goes on: each wave steps the StyleApplier's Dusk from
/// poster sunset (wave 1) toward purple night (FullDuskWave and beyond).
/// </summary>
public partial class DuskByWave : Node
{
	[Export] public NodePath StyleApplierPath { get; set; } = "../StyleApplier";
	/// <summary>The wave at which the sky reaches full dusk.</summary>
	[Export] public int FullDuskWave { get; set; } = 7;

	private StyleApplier _applier;

	public override void _Ready()
	{
		_applier = GetNodeOrNull<StyleApplier>(StyleApplierPath);
		GameEvents.Instance.WaveStarted += OnWaveStarted;
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance != null)
			GameEvents.Instance.WaveStarted -= OnWaveStarted;
	}

	private void OnWaveStarted(int waveNumber, int enemyCount)
	{
		if (_applier == null)
			return;
		_applier.Dusk = Mathf.Clamp((waveNumber - 1f) / Mathf.Max(FullDuskWave - 1f, 1f), 0f, 1f);
	}
}
