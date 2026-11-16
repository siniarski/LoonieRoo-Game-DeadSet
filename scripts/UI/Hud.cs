using Deadset.Combat;
using Deadset.Core;
using Deadset.Economy;
using Deadset.Player;
using Deadset.Waves;
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
	[Export] public Color HealthDamageTint { get; set; } = new(0.85f, 0f, 0f);
	/// <summary>Edge flash colour when armour soaked the whole hit.</summary>
	[Export] public Color ArmourDamageTint { get; set; } = new(0.3f, 0.6f, 1f);

	[ExportGroup("Dash")]
	[Export] public Color DashReadyColor { get; set; } = new(0.6f, 0.9f, 1f, 0.9f);
	[Export] public Color DashCoolingColor { get; set; } = new(1f, 1f, 1f, 0.35f);

	[ExportGroup("Waves")]
	[Export] public float AnnouncementTime { get; set; } = 2.2f;
	[Export] public Color WaveStartColor { get; set; } = new(1f, 0.55f, 0.2f);
	[Export] public Color WaveClearedColor { get; set; } = new(0.5f, 1f, 0.45f);

	private Control _crosshair;
	private ColorRect[] _crosshairLines;
	private Control _hitmarker;
	private Label _ammoLabel;
	private Label _reloadLabel;
	private Label _healthLabel;
	private ColorRect _healthFill;
	private Control _damageVignette;
	private Label _diedLabel;
	private Label _cashLabel;
	private Label _cashPopup;
	private ColorRect _armourFill;
	private Control _armourBar;
	private float _cashPopupTimer;
	private int _cashPopupTotal;
	private Label _waveInfo;
	private Label _announcement;
	private WaveDirector _director;
	private float _announcementTimer;

	private Weapon _weapon;
	private HealthComponent _playerHealth;
	private float _damageFlash;
	private Color _damageTint;
	private float _lastHealth = -1f;
	private PlayerController _controller;
	private Control _dashBar;
	private ColorRect _dashFill;
	private float _dashReadyPulse;
	private bool _dashWasReady = true;
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
		_damageTint = HealthDamageTint;
		BuildDashBar();
		_diedLabel = GetNode<Label>("Died");
		_cashLabel = GetNode<Label>("Cash");
		_cashPopup = GetNode<Label>("CashPopup");
		_cashPopup.Modulate = Colors.Transparent;
		_armourBar = GetNode<Control>("ArmourBar");
		_armourFill = GetNode<ColorRect>("ArmourBar/Fill");
		_armourBar.Visible = false;
		_waveInfo = GetNode<Label>("WaveInfo");
		_announcement = GetNode<Label>("Announcement");
		_announcement.Modulate = Colors.Transparent;
		_waveInfo.Text = "";

		GameEvents.Instance.WaveStarted += OnWaveStarted;
		GameEvents.Instance.WaveCleared += OnWaveCleared;

		LayoutHitmarker();
		_hitmarker.Modulate = Colors.Transparent;
		_reloadLabel.Visible = false;
		_damageVignette.Modulate = Colors.Transparent;
		_diedLabel.Visible = false;

		// Wait one frame so the player has equipped its weapon.
		CallDeferred(MethodName.BindToPlayer);
	}

	public override void _ExitTree()
	{
		if (GameEvents.Instance == null)
			return;
		GameEvents.Instance.WaveStarted -= OnWaveStarted;
		GameEvents.Instance.WaveCleared -= OnWaveCleared;
	}

	private void OnWaveStarted(int waveNumber, int enemyCount) => Announce($"WAVE {waveNumber}", WaveStartColor);

	private void OnWaveCleared(int waveNumber) => Announce("WAVE CLEARED", WaveClearedColor);

	private void Announce(string text, Color color)
	{
		_announcement.Text = text;
		_announcement.SelfModulate = color;
		_announcementTimer = AnnouncementTime;
	}

	private void UpdateWaveInfo(float dt)
	{
		if (_announcementTimer > 0f)
		{
			_announcementTimer -= dt;
			float t = _announcementTimer / AnnouncementTime;
			float alpha = Mathf.Clamp(t * 3f, 0f, 1f);
			_announcement.Modulate = new Color(1f, 1f, 1f, alpha);
			_announcement.Scale = Vector2.One * (1f + 0.25f * Mathf.Pow(Mathf.Max(t - 0.85f, 0f) / 0.15f, 2f));
		}
		else
		{
			_announcement.Modulate = Colors.Transparent;
		}

		_director ??= GetTree().GetFirstNodeInGroup("wave_director") as WaveDirector;
		if (_director == null)
			return;

		int seconds = Mathf.CeilToInt(Mathf.Max(_director.TimeRemaining, 0f));
		_waveInfo.Text = _director.CurrentPhase switch
		{
			WaveDirector.Phase.Wave => $"WAVE {_director.WaveNumber}   ·   {_director.EnemiesRemaining} LEFT",
			WaveDirector.Phase.Break => $"BREAK   ·   WAVE {_director.WaveNumber} IN {seconds}s   ·   [ENTER] READY",
			WaveDirector.Phase.Countdown => $"WAVE {_director.WaveNumber} IN {seconds}s   ·   [ENTER] READY",
			_ => "",
		};
	}

	private void BindToPlayer()
	{
		Node player = GetTree().GetFirstNodeInGroup("player");
		_controller = player as PlayerController;
		BindHealth(HealthComponent.FindOn(player));
		BindWallet(Wallet.FindOn(player));

		var input = player?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		if (input == null)
		{
			GD.PushWarning("Hud: no player with a WeaponInput node found.");
			return;
		}

		input.WeaponEquipped += SetWeapon;
		if (input.QuickMelee != null)
			input.QuickMelee.HitLanded += OnHitLanded;
		_quickMeleeKey = input.QuickMelee != null ? $"V / FIRE: {input.QuickMelee.Stats.DisplayName.ToUpperInvariant()}" : null;
		if (input.EquippedWeapon != null)
			SetWeapon(input.EquippedWeapon);
	}

	private void BindWallet(Wallet wallet)
	{
		if (wallet == null)
		{
			_cashLabel.Visible = false;
			return;
		}
		_cashLabel.Text = $"${wallet.Cash}";
		wallet.CashChanged += OnCashChanged;
	}

	private void OnCashChanged(int cash, int delta)
	{
		_cashLabel.Text = $"${cash}";
		if (delta <= 0)
			return;
		// Kills in quick succession add up into one popup.
		_cashPopupTotal = _cashPopupTimer > 0f ? _cashPopupTotal + delta : delta;
		_cashPopupTimer = 1.2f;
		_cashPopup.Text = $"+{_cashPopupTotal}";
	}

	private void OnArmourChanged(float armour, float maxArmour)
	{
		_armourBar.Visible = armour > 0f;
		_armourFill.AnchorRight = maxArmour > 0f ? armour / maxArmour : 0f;
	}

	private void BindHealth(HealthComponent health)
	{
		if (health == null)
		{
			GD.PushWarning("Hud: player has no HealthComponent.");
			return;
		}

		_playerHealth = health;
		if (health.GetParent() is Node3D body)
		{
			var indicator = new DamageDirectionIndicator { Name = "DamageDirection" };
			AddChild(indicator);
			indicator.Bind(body, health);
		}
		health.Damaged += OnPlayerDamaged;
		health.HealthReset += OnPlayerHealthReset;
		health.Healed += (_, _) => UpdateHealth();
		health.ArmourChanged += OnArmourChanged;
		OnArmourChanged(health.Armour, health.MaxArmour);
		UpdateHealth();
	}

	private void OnPlayerDamaged(float amount, float remaining)
	{
		// Armour soaked it all if health didn't move: flash blue instead of red.
		bool armourOnly = _lastHealth >= 0f && Mathf.IsEqualApprox(remaining, _lastHealth);
		_damageTint = armourOnly ? ArmourDamageTint : HealthDamageTint;
		float flash = DamageFlashBase + (1f - DamageFlashBase) * Mathf.Clamp(amount / DamageForFullFlash, 0f, 1f);
		_damageFlash = Mathf.Max(_damageFlash, armourOnly ? flash * 0.7f : flash);
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
		_lastHealth = _playerHealth.Current;
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
		ShowWeaponName(weapon);
	}

	private Label _weaponName;
	private string _quickMeleeKey;
	private float _weaponNameFlash;

	/// <summary>The equipped weapon's name above the ammo count; it flares up on a switch.</summary>
	private void ShowWeaponName(Weapon weapon)
	{
		if (_weaponName == null)
		{
			_weaponName = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Right,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
				OffsetLeft = -420f, OffsetRight = -40f, OffsetTop = -130f, OffsetBottom = -98f,
			};
			_weaponName.AddThemeFontSizeOverride("font_size", 22);
			_weaponName.AddThemeConstantOverride("outline_size", 6);
			_weaponName.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
			AddChild(_weaponName);
		}
		var input = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerWeaponInput>("WeaponInput");
		int slot = 1;
		for (int i = 0; input != null && i < input.Weapons.Count; i++)
			if (input.Weapons[i] == weapon)
				slot = i + 1;
		_weaponName.Text = input != null && input.Weapons.Count > 1
			? $"[{slot}] {weapon.Stats.DisplayName.ToUpper()}"
			: weapon.Stats.DisplayName.ToUpper();
		_weaponNameFlash = 1f;
	}

	private void UpdateWeaponName(float dt)
	{
		if (_weaponName == null)
			return;
		_weaponNameFlash = Mathf.MoveToward(_weaponNameFlash, 0f, dt * 1.2f);
		_weaponName.Modulate = new Color(1f, 0.85f + 0.15f * _weaponNameFlash, 0.6f + 0.4f * _weaponNameFlash,
			0.55f + 0.45f * _weaponNameFlash);
		_weaponName.Scale = Vector2.One;
	}

	private void OnAmmoChanged(int inMagazine, int reserve)
	{
		if (!_weapon.Stats.UsesAmmo)
		{
			_ammoLabel.Text = "\u221E";
			_ammoLabel.Modulate = Colors.White;
			return;
		}
		_ammoLabel.Text = $"{inMagazine} / {reserve}";
		bool low = inMagazine <= Mathf.CeilToInt(_weapon.MagazineSize * LowAmmoFraction);
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

		UpdateWaveInfo(dt);
		UpdateWeaponName(dt);

		if (_cashPopupTimer > 0f)
		{
			_cashPopupTimer -= dt;
			_cashPopup.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_cashPopupTimer / 0.4f, 0f, 1f));
		}

		_bloom *= Mathf.Exp(-BloomRecoverySpeed * dt);
		LayoutCrosshair(CrosshairGap + _bloom);

		// Stays fully red while dead.
		bool dead = _playerHealth != null && _playerHealth.IsDead;
		_damageFlash = dead ? 1f : Mathf.Max(0f, _damageFlash - DamageFlashFadeSpeed * dt);
		Color tint = dead ? HealthDamageTint : _damageTint;
		_damageVignette.Modulate = new Color(tint, _damageFlash);
		UpdateDashBar(dt);

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
		bool dry = _weapon != null && _weapon.IsDry && _quickMeleeKey != null;
		_reloadLabel.Visible = reloading || dry;
		if (reloading)
			_reloadLabel.Text = $"{_weapon.Stats.ReloadLabel} {Mathf.RoundToInt(_weapon.ReloadProgress * 100f)}%";
		else if (dry)
			_reloadLabel.Text = $"EMPTY  -  {_quickMeleeKey}";
	}

	private void BuildDashBar()
	{
		_dashBar = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_dashBar.SetAnchorsPreset(Control.LayoutPreset.Center);
		_dashBar.OffsetLeft = -28; _dashBar.OffsetRight = 28; _dashBar.OffsetTop = 30; _dashBar.OffsetBottom = 34;
		var background = new ColorRect { Color = new Color(0f, 0f, 0f, 0.4f), MouseFilter = Control.MouseFilterEnum.Ignore };
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_dashBar.AddChild(background);
		_dashFill = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
		_dashFill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_dashBar.AddChild(_dashFill);
		AddChild(_dashBar);
	}

	private void UpdateDashBar(float dt)
	{
		if (_controller == null || !_controller.DashUnlocked)
		{
			_dashBar.Visible = false;
			return;
		}
		_dashBar.Visible = true;
		float remaining = _controller.DashCooldownRemaining;
		bool ready = remaining <= 0f;
		if (ready && !_dashWasReady)
			_dashReadyPulse = 1f;
		_dashWasReady = ready;
		_dashReadyPulse = Mathf.Max(0f, _dashReadyPulse - dt * 4f);

		float fraction = ready ? 1f : 1f - remaining / Mathf.Max(_controller.DashCooldown, 0.01f);
		_dashFill.AnchorRight = fraction;
		_dashFill.Color = ready ? DashReadyColor : DashCoolingColor;
		_dashBar.Scale = Vector2.One * (1f + 0.5f * _dashReadyPulse);
		_dashBar.PivotOffset = _dashBar.Size * 0.5f;
		// Fade out when ready and idle, so it doesn't clutter the crosshair.
		_dashBar.Modulate = new Color(1f, 1f, 1f, ready ? 0.35f + 0.65f * _dashReadyPulse : 1f);
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
