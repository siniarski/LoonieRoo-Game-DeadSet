using System.Collections.Generic;
using Deadset.Combat;
using Godot;

namespace Deadset.UI;

/// <summary>
/// Red wedges on a ring around the crosshair, pointing at whoever just hurt the player.
/// Hits from nearly the same direction merge into one wedge, so a flock pecking from
/// behind reads as one clear "behind you" rather than a smear. Wedges follow the source's
/// position as you turn, then fade.
/// </summary>
public partial class DamageDirectionIndicator : Control
{
	[Export] public float Radius { get; set; } = 110f;
	[Export] public float Thickness { get; set; } = 14f;
	/// <summary>Arc width of a wedge, in degrees.</summary>
	[Export] public float WedgeDegrees { get; set; } = 34f;
	[Export] public float FadeTime { get; set; } = 1.1f;
	/// <summary>Hits within this many degrees of an existing wedge refresh it instead of adding one.</summary>
	[Export] public float MergeDegrees { get; set; } = 25f;
	/// <summary>Damage that makes a wedge fully opaque; smaller hits start fainter.</summary>
	[Export] public float DamageForFullAlpha { get; set; } = 10f;
	[Export] public Color HealthColor { get; set; } = new(0.95f, 0.1f, 0.05f);
	[Export] public Color ArmourColor { get; set; } = new(0.35f, 0.65f, 1f);
	[Export] public int MaxWedges { get; set; } = 8;

	private class Wedge
	{
		public Vector3 Source;
		public float Life;
		public float Strength;
		public Color Color;
	}

	private readonly List<Wedge> _wedges = new();
	private Node3D _player;
	private HealthComponent _health;

	public void Bind(Node3D player, HealthComponent health)
	{
		_player = player;
		_health = health;
		health.HitFrom += OnHitFrom;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_health))
			_health.HitFrom -= OnHitFrom;
	}

	private void OnHitFrom(Vector3 source, float amount)
	{
		if (!IsInstanceValid(_player))
			return;
		// Blue while armour is still soaking hits, red once it is gone (matches the vignette).
		Color color = _health.Armour > 0f ? ArmourColor : HealthColor;
		float strength = Mathf.Clamp(0.45f + 0.55f * amount / Mathf.Max(DamageForFullAlpha, 0.01f), 0.45f, 1f);
		float angle = AngleTo(source);
		foreach (Wedge w in _wedges)
		{
			if (Mathf.Abs(Mathf.AngleDifference(AngleTo(w.Source), angle)) <= Mathf.DegToRad(MergeDegrees))
			{
				w.Source = source;
				w.Life = FadeTime;
				w.Strength = Mathf.Max(w.Strength, strength);
				w.Color = color;
				return;
			}
		}
		if (_wedges.Count >= MaxWedges)
			_wedges.RemoveAt(0);
		_wedges.Add(new Wedge { Source = source, Life = FadeTime, Strength = strength, Color = color });
	}

	/// <summary>Screen angle of a world point: 0 = straight ahead (up on screen), clockwise.</summary>
	private float AngleTo(Vector3 source)
	{
		Vector3 local = _player.GlobalTransform.Basis.Inverse() * (source - _player.GlobalPosition);
		return Mathf.Atan2(local.X, -local.Z);
	}

	public override void _Process(double delta)
	{
		if (_wedges.Count == 0)
			return;
		float dt = (float)delta;
		for (int i = _wedges.Count - 1; i >= 0; i--)
		{
			_wedges[i].Life -= dt;
			if (_wedges[i].Life <= 0f)
				_wedges.RemoveAt(i);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!IsInstanceValid(_player))
			return;
		Vector2 centre = GetViewportRect().Size * 0.5f;
		float half = Mathf.DegToRad(WedgeDegrees) * 0.5f;
		foreach (Wedge w in _wedges)
		{
			float t = w.Life / Mathf.Max(FadeTime, 0.01f);
			// Hold, then fade over the last 60%.
			float alpha = Mathf.Clamp(t / 0.6f, 0f, 1f) * w.Strength;
			float a = AngleTo(w.Source) - Mathf.Pi * 0.5f;
			// Pop outward a little when fresh.
			float r = Radius + 10f * Mathf.Clamp((t - 0.85f) / 0.15f, 0f, 1f);
			Color c = w.Color with { A = alpha };
			DrawArc(centre, r, a - half, a + half, 16, c, Thickness, true);
			// Brighter inner edge so it reads against bright glows.
			DrawArc(centre, r - Thickness * 0.5f, a - half * 0.8f, a + half * 0.8f, 12,
				new Color(1f, 0.85f, 0.8f, alpha * 0.8f), 2f, true);
		}
	}
}
