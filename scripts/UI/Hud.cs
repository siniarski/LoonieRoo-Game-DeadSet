using Deadset.Combat;
using Deadset.Player;
using Deadset.Weapons;
using Godot;

namespace Deadset.UI;

/// <summary>
/// Display only: reads weapon and health state through signals and shows it. Holds no game state.
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
	[Export] public Color WeakSpotColor { get; set; } = new(1f, 0.8f, 0.15f);

	[ExportGroup("Ammo")]
	/// <summary>Magazine fraction at or below which the ammo counter turns red.</summary>
	[Export] public float LowAmmoFraction { get; set; } = 0.25f;
	[Export] public Color LowAmmoColor { get; set; } = new(1f, 0.35f, 0.25f);

	[ExportGroup("Health")]
	/// <summary>Health fraction at or below which the health readout turns red.</summary>
	[Export] public float LowHealthFraction { get; set; } = 0.3f;
	[Export] public Color HealthColor { get; set; } = new(0.45f, 0.9f, 0.4f);
	[Export] public Color LowHealthColor { get; set; } = new(1f, 0.3f, 0.25f);
	/// <summary>Red edge flash strength for a hit, before scaling by damage.</summary>
	[Export] public float DamageFlashBase { get; set; } = 0.35f;
	/// <summary>Damage that produces a full-strength flash.</summary>
	[Export] public float DamageForFullFlash { get; set; } = 25f;
	[Export] public float DamageFlashFadeSpeed { get; set; } = 3f;

	private Control _crosshair;
	private ColorRect[] _crosshairLines;
	private Control _hitmarker;
	private Label _ammoLabel;
	private Label _reloadLabel;
	private Label _healthLabel;
	private ColorRect _healthFill;
	private Control _damageVignette;
	private Label _diedLabel;

	private Weapon _weapon;
	private HealthComponent _playerHealth;
	private float _damageFlash;
	private float _bloom;
	private float _hitmarkerTimer;
	private bool _lastHitWasKill;
	private bool _lastHitWasWeakSpot;

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
		_healthLabel = GetNode<Label>("Health");
		_healthFill = GetNode<ColorRect>("HealthBar/Fill");
		_damageVignette = GetNode<Control>("DamageVignette");
		_diedLabel = GetNode<Label>("Died");

		LayoutHitmarker();
		_hitmarker.Modulate = Colors.Transparent;
		_reloadLabel.Visible = false;
		_damageVignette.Modulate = Colors.Transparent;
		_diedLabel.Visible = false;

		// Wait one frame so the player has equipped its weapon.
		CallDeferred(MethodName.BindToPlayer);
	}

	private void BindToPlayer()
	{
		Node player = GetTree().GetFirstNodeInGroup("player");
		BindHealth(HealthComponent.FindOn(player));

		var input = player?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		if (input == null)
		{
			GD.PushWarning("Hud: no player with a WeaponInput node found.");
			return;
		}

		input.WeaponEquipped += SetWeapon;
		if (input.EquippedWeapon != null)
			SetWeapon(input.EquippedWeapon);
	}

	private void BindHealth(HealthComponent health)
	{
		if (health == null)
		{
			GD.PushWarning("Hud: player has no HealthComponent.");
			return;
		}

		_playerHealth = health;
		health.Damaged += OnPlayerDamaged;
		health.Died += () => _diedLabel.Visible = true;
		health.HealthReset += OnPlayerHealthReset;
		UpdateHealth();
	}

	private void OnPlayerDamaged(float amount, float remaining)
	{
		float flash = DamageFlashBase + (1f - DamageFlashBase) * Mathf.Clamp(amount / DamageForFullFlash, 0f, 1f);
		_damageFlash = Mathf.Max(_damageFlash, flash);
		UpdateHealth();
	}

	private void OnPlayerHealthReset()
	{
		_diedLabel.Visible = false;
		UpdateHealth();
	}

	private void UpdateHealth()
	{
		float fraction = _playerHealth.Current / _playerHealth.MaxHealth;
		_healthLabel.Text = Mathf.CeilToInt(_playerHealth.Current).ToString();
		Color color = fraction <= LowHealthFraction ? LowHealthColor : HealthColor;
		_healthLabel.Modulate = color;
		_healthFill.Color = color;
		_healthFill.AnchorRight = fraction;
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

	private void OnHitLanded(bool killed, bool weakSpot)
	{
		_hitmarkerTimer = HitmarkerTime;
		_lastHitWasKill = killed;
		_lastHitWasWeakSpot = weakSpot;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_bloom *= Mathf.Exp(-BloomRecoverySpeed * dt);
		LayoutCrosshair(CrosshairGap + _bloom);

		// Stays fully red while dead.
		bool dead = _playerHealth != null && _playerHealth.IsDead;
		_damageFlash = dead ? 1f : Mathf.Max(0f, _damageFlash - DamageFlashFadeSpeed * dt);
		_damageVignette.Modulate = new Color(1f, 1f, 1f, _damageFlash);

		// Iron sights replace the crosshair while aiming.
		float aim = _weapon?.AimBlend ?? 0f;
		_crosshair.Modulate = new Color(1f, 1f, 1f, 1f - aim);

		if (_hitmarkerTimer > 0f)
		{
			_hitmarkerTimer -= dt;
			float alpha = Mathf.Clamp(_hitmarkerTimer / HitmarkerTime, 0f, 1f);
			Color color = _lastHitWasKill ? KillColor : _lastHitWasWeakSpot ? WeakSpotColor : HitColor;
			float size = (_lastHitWasKill ? 1.5f : 1f) * (_lastHitWasWeakSpot ? 1.3f : 1f);
			_hitmarker.Modulate = new Color(color, alpha);
			_hitmarker.Scale = Vector2.One * size * (1f + 0.3f * alpha);
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
