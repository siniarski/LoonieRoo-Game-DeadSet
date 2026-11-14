using Deadset.Player;
using Deadset.Weapons;
using Godot;

namespace Deadset.UI;

/// <summary>
/// Display only: reads weapon state through signals and shows it. Holds no game state.
/// </summary>
public partial class Hud : CanvasLayer
{
	[ExportGroup("Crosshair")]
	[Export] public float CrosshairGap { get; set; } = 6f;
	[Export] public float CrosshairLength { get; set; } = 8f;
	[Export] public float CrosshairThickness { get; set; } = 2f;
	/// <summary>Extra gap added per shot, in pixels.</summary>
	[Export] public float BloomPerShot { get; set; } = 5f;
	[Export] public float MaxBloom { get; set; } = 18f;
	[Export] public float BloomRecoverySpeed { get; set; } = 10f;

	[ExportGroup("Hit marker")]
	[Export] public float HitmarkerTime { get; set; } = 0.15f;
	[Export] public Color HitColor { get; set; } = Colors.White;
	[Export] public Color KillColor { get; set; } = new(1f, 0.25f, 0.2f);

	[ExportGroup("Ammo")]
	/// <summary>Magazine fraction at or below which the ammo counter turns red.</summary>
	[Export] public float LowAmmoFraction { get; set; } = 0.25f;
	[Export] public Color LowAmmoColor { get; set; } = new(1f, 0.35f, 0.25f);

	private Control _crosshair;
	private ColorRect[] _crosshairLines;
	private Control _hitmarker;
	private Label _ammoLabel;
	private Label _reloadLabel;

	private Weapon _weapon;
	private float _bloom;
	private float _hitmarkerTimer;
	private bool _lastHitWasKill;

	public override void _Ready()
	{
		_crosshair = GetNode<Control>("Crosshair");
		_crosshairLines = new[]
		{
			GetNode<ColorRect>("Crosshair/Top"),
			GetNode<ColorRect>("Crosshair/Bottom"),
			GetNode<ColorRect>("Crosshair/Left"),
			GetNode<ColorRect>("Crosshair/Right"),
		};
		_hitmarker = GetNode<Control>("Hitmarker");
		_ammoLabel = GetNode<Label>("Ammo");
		_reloadLabel = GetNode<Label>("Reload");

		LayoutHitmarker();
		_hitmarker.Modulate = Colors.Transparent;
		_reloadLabel.Visible = false;

		// Wait one frame so the player has equipped its weapon.
		CallDeferred(MethodName.BindToPlayer);
	}

	private void BindToPlayer()
	{
		var input = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		if (input == null)
		{
			GD.PushWarning("Hud: no player with a WeaponInput node found.");
			return;
		}

		input.WeaponEquipped += SetWeapon;
		if (input.EquippedWeapon != null)
			SetWeapon(input.EquippedWeapon);
	}

	private void SetWeapon(Weapon weapon)
	{
		if (_weapon != null)
		{
			_weapon.AmmoChanged -= OnAmmoChanged;
			_weapon.Fired -= OnFired;
			_weapon.HitLanded -= OnHitLanded;
		}

		_weapon = weapon;
		_weapon.AmmoChanged += OnAmmoChanged;
		_weapon.Fired += OnFired;
		_weapon.HitLanded += OnHitLanded;
		OnAmmoChanged(_weapon.AmmoInMagazine, _weapon.ReserveAmmo);
	}

	private void OnAmmoChanged(int inMagazine, int reserve)
	{
		_ammoLabel.Text = $"{inMagazine} / {reserve}";
		bool low = inMagazine <= Mathf.CeilToInt(_weapon.Stats.MagazineSize * LowAmmoFraction);
		_ammoLabel.Modulate = low ? LowAmmoColor : Colors.White;
	}

	private void OnFired(float recoilPitchDegrees, float recoilYawDegrees)
	{
		_bloom = Mathf.Min(_bloom + BloomPerShot, MaxBloom);
	}

	private void OnHitLanded(bool killed)
	{
		_hitmarkerTimer = HitmarkerTime;
		_lastHitWasKill = killed;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_bloom *= Mathf.Exp(-BloomRecoverySpeed * dt);
		LayoutCrosshair(CrosshairGap + _bloom);

		// Iron sights replace the crosshair while aiming.
		float aim = _weapon?.AimBlend ?? 0f;
		_crosshair.Modulate = new Color(1f, 1f, 1f, 1f - aim);

		if (_hitmarkerTimer > 0f)
		{
			_hitmarkerTimer -= dt;
			float alpha = Mathf.Clamp(_hitmarkerTimer / HitmarkerTime, 0f, 1f);
			Color color = _lastHitWasKill ? KillColor : HitColor;
			_hitmarker.Modulate = new Color(color, alpha);
			_hitmarker.Scale = Vector2.One * (_lastHitWasKill ? 1.5f : 1f) * (1f + 0.3f * alpha);
		}
		else
		{
			_hitmarker.Modulate = Colors.Transparent;
		}

		bool reloading = _weapon != null && _weapon.IsReloading;
		_reloadLabel.Visible = reloading;
		if (reloading)
			_reloadLabel.Text = $"RELOADING {Mathf.RoundToInt(_weapon.ReloadProgress * 100f)}%";
	}

	private void LayoutCrosshair(float gap)
	{
		float len = CrosshairLength;
		float half = CrosshairThickness * 0.5f;
		Vector2 vertical = new(CrosshairThickness, len);
		Vector2 horizontal = new(len, CrosshairThickness);

		SetRect(_crosshairLines[0], new Vector2(-half, -gap - len), vertical);
		SetRect(_crosshairLines[1], new Vector2(-half, gap), vertical);
		SetRect(_crosshairLines[2], new Vector2(-gap - len, -half), horizontal);
		SetRect(_crosshairLines[3], new Vector2(gap, -half), horizontal);
	}

	private void LayoutHitmarker()
	{
		const float innerRadius = 7f;
		const float length = 9f;
		for (int i = 0; i < 4; i++)
		{
			var line = _hitmarker.GetChild<ColorRect>(i);
			float angle = Mathf.DegToRad(45f + 90f * i);
			line.Size = new Vector2(length, 2f);
			line.PivotOffset = new Vector2(0f, 1f);
			line.Rotation = angle;
			line.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius - line.PivotOffset;
		}
	}

	private static void SetRect(ColorRect rect, Vector2 position, Vector2 size)
	{
		rect.Position = position;
		rect.Size = size;
	}
}
