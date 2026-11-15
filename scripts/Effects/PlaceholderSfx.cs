using System;
using Godot;

namespace Deadset.Effects;

public enum EnemyCue { None, Squeak, Nip, Peck, Thump, Squawk, Grunt, Croak, Growl }

/// <summary>
/// Placeholder sound effects synthesised in code, so the prototype needs no audio imports.
/// Swap these for real recordings later; callers only see AudioStream.
/// </summary>
public static class PlaceholderSfx
{
	private const int SampleRate = 22050;
	private const float Tau = MathF.PI * 2f;
	private static readonly Random Rng = new(1234);

	/// <summary>
	/// Most copies of one sound playing at once through PlayAt. Past that, extra copies are
	/// dropped: a horde pecking in unison adds nothing audible, only players to mix.
	/// </summary>
	public static int MaxVoicesPerSound { get; set; } = 8;
	private static readonly System.Collections.Generic.Dictionary<AudioStream, int> Voices = new();

	private static AudioStreamWav _nailShot;
	private static AudioStreamWav _nailImpact;
	private static AudioStreamWav _hitTick;
	private static AudioStreamWav _killTick;
	private static AudioStreamWav _reloadStart;
	private static AudioStreamWav _reloadEnd;
	private static AudioStreamWav _dryFire;
	private static AudioStreamWav _bunnySqueak;
	private static AudioStreamWav _bunnyNip;
	private static AudioStreamWav _playerHurt;
	private static AudioStreamWav _headshotTink;
	private static AudioStreamWav _peck;
	private static AudioStreamWav _thump;
	private static AudioStreamWav _squawk;
	private static AudioStreamWav _grunt;
	private static AudioStreamWav _croak;
	private static AudioStreamWav _growl;
	private static AudioStreamWav _dashWhoosh;
	private static AudioStreamWav _ricochet;
	private static AudioStreamWav _brushcutter;
	/// <summary>Two-stroke buzz of one brushcutter swing.</summary>
	public static AudioStreamWav Brushcutter => _brushcutter ??= Build(0.26f, t =>
		(MathF.Sign(Sine(170f + 40f * t, t)) * 0.35f + Sine(340f, t) * 0.25f + Noise() * 0.3f)
		* (0.6f + 0.4f * Sine(32f, t)) * MathF.Min(1f, t * 60f) * MathF.Exp(-t * 6f));
	private static AudioStreamWav _footLight;
	private static AudioStreamWav _footHeavy;
	private static AudioStreamWav _playerStep;
	private static AudioStreamWav _playerLand;

	/// <summary>Claw scratch on dirt: light runners (emus, dingoes, bunnies).</summary>
	public static AudioStreamWav FootLight => _footLight ??= Build(0.07f, t => (Noise() * 0.7f + Sine(420f, t) * 0.3f) * MathF.Exp(-t * 60f) * 0.8f);
	/// <summary>Low padded thud: heavy runners (wombats, cassowaries, roos).</summary>
	public static AudioStreamWav FootHeavy => _footHeavy ??= Build(0.14f, t => (Sweep(t, 110f, 55f, 30f) * 0.9f + Noise() * MathF.Exp(-t * 80f) * 0.35f) * MathF.Exp(-t * 24f));
	/// <summary>The player's boot on dirt.</summary>
	public static AudioStreamWav PlayerStep => _playerStep ??= Build(0.09f, t => (Noise() * 0.5f + Sweep(t, 160f, 90f, 40f) * 0.5f) * MathF.Exp(-t * 45f) * 0.7f);
	/// <summary>Both boots hitting the ground after a jump.</summary>
	public static AudioStreamWav PlayerLand => _playerLand ??= Build(0.16f, t => (Noise() * 0.45f + Sweep(t, 140f, 60f, 25f) * 0.7f) * MathF.Exp(-t * 26f));

	/// <summary>Short rising chirp when a Glow Bunny pops.</summary>
	public static AudioStreamWav BunnySqueak => _bunnySqueak ??= Build(0.14f, t => Sweep(t, 900f, 2200f, 20f) * MathF.Exp(-t * 22f) * 0.5f);
	/// <summary>Little snap of teeth.</summary>
	public static AudioStreamWav BunnyNip => _bunnyNip ??= Build(0.06f, t => (Noise() * 0.5f + Sine(1500f, t) * 0.4f) * MathF.Exp(-t * 90f));
	/// <summary>Dull body thud when the player takes damage.</summary>
	public static AudioStreamWav PlayerHurt => _playerHurt ??= Build(0.22f, PlayerHurtSample());

	/// <summary>Bright metallic tink layered on a weak-spot (head) hit.</summary>
	public static AudioStreamWav HeadshotTink => _headshotTink ??= Build(0.2f, t => (Sine(3100f, t) * 0.5f + Sine(4650f, t) * 0.3f) * MathF.Exp(-t * 18f));
	/// <summary>Hard woody knock of a beak.</summary>
	public static AudioStreamWav Peck => _peck ??= Build(0.07f, t => (Sine(900f, t) * 0.6f + Noise() * 0.4f) * MathF.Exp(-t * 70f));
	/// <summary>Heavy kick or slam.</summary>
	public static AudioStreamWav Thump => _thump ??= Build(0.16f, t => Sweep(t, 180f, 60f, 25f) * MathF.Exp(-t * 20f) + Noise() * MathF.Exp(-t * 60f) * 0.4f);
	/// <summary>Harsh falling bird cry.</summary>
	public static AudioStreamWav Squawk => _squawk ??= Build(0.22f, t => (Sweep(t, 1300f, 500f, 10f) * 0.6f + Noise() * 0.35f) * MathF.Exp(-t * 11f));
	/// <summary>Low animal grunt.</summary>
	public static AudioStreamWav Grunt => _grunt ??= Build(0.25f, t => (Sweep(t, 220f, 90f, 9f) * 0.8f + Noise() * 0.2f) * MathF.Exp(-t * 9f));
	/// <summary>Wet toad croak.</summary>
	public static AudioStreamWav Croak => _croak ??= Build(0.2f, t => Sine(130f, t) * (0.5f + 0.5f * Sine(28f, t)) * MathF.Exp(-t * 10f));

	/// <summary>Rising growl with dirt-scrape noise: the wombat's charge tell.</summary>
	public static AudioStreamWav Growl => _growl ??= Build(0.75f, GrowlSample());

	/// <summary>Placeholder sound for each enemy cue, picked in the enemy scene's inspector.</summary>
	public static AudioStream ForCue(EnemyCue cue) => cue switch
	{
		EnemyCue.Squeak => BunnySqueak,
		EnemyCue.Nip => BunnyNip,
		EnemyCue.Peck => Peck,
		EnemyCue.Thump => Thump,
		EnemyCue.Squawk => Squawk,
		EnemyCue.Grunt => Grunt,
		EnemyCue.Croak => Croak,
		EnemyCue.Growl => Growl,
		_ => null,
	};

	/// <summary>Pneumatic "thunk-pssh".</summary>
	public static AudioStreamWav NailShot => _nailShot ??= Build(0.18f, NailShotSample());
	/// <summary>Dull thud of a nail hitting a wall.</summary>
	public static AudioStreamWav NailImpact => _nailImpact ??= Build(0.12f, NailImpactSample());
	/// <summary>Crisp tick confirming a hit on a target.</summary>
	public static AudioStreamWav HitTick => _hitTick ??= Build(0.06f, t => Sine(1900f, t) * MathF.Exp(-t * 55f) * 0.6f);
	/// <summary>Rising two-tone ding for a kill.</summary>
	public static AudioStreamWav KillTick => _killTick ??= Build(0.18f, t => Sine(t < 0.07f ? 1400f : 2100f, t) * MathF.Exp(-t * 14f) * 0.55f);
	public static AudioStreamWav ReloadStart => _reloadStart ??= Build(0.09f, ReloadStartSample());
	public static AudioStreamWav ReloadEnd => _reloadEnd ??= Build(0.14f, t => Clack(t, 900f) + Clack(t - 0.06f, 1200f) * 1.2f);
	public static AudioStreamWav DryFire => _dryFire ??= Build(0.05f, t => Sine(2400f, t) * MathF.Exp(-t * 160f) * 0.5f + Noise() * MathF.Exp(-t * 220f) * 0.3f);

	/// <summary>Airy swell of wind for the dash.</summary>
	public static AudioStreamWav DashWhoosh => _dashWhoosh ??= Build(0.3f, DashWhooshSample());
	/// <summary>Bright falling "pew" of a nail glancing off a surface.</summary>
	public static AudioStreamWav Ricochet => _ricochet ??= Build(0.2f, t => (Sweep(t, 3600f, 1500f, 14f) * 0.5f + Noise() * MathF.Exp(-t * 120f) * 0.4f) * MathF.Exp(-t * 16f));

	/// <summary>Plays a one-shot positional sound that frees itself when done.</summary>
	public static void PlayAt(Node parent, AudioStream stream, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.08f)
	{
		if (stream == null)
			return;
		Voices.TryGetValue(stream, out int playing);
		if (playing >= MaxVoicesPerSound)
			return;
		Voices[stream] = playing + 1;

		var player = new AudioStreamPlayer3D
		{
			Stream = stream,
			VolumeDb = volumeDb,
			PitchScale = 1f + (float)GD.RandRange(-pitchJitter, pitchJitter),
			UnitSize = 6f,
		};
		parent.AddChild(player);
		player.GlobalPosition = position;
		player.Finished += player.QueueFree;
		// Counted down when it leaves the tree, so a scene reload mid-sound can't leak voices.
		player.TreeExiting += () => Voices[stream] = Math.Max(0, Voices[stream] - 1);
		player.Play();
	}

	private static Func<float, float> NailShotSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			lowPassed += (Noise() - lowPassed) * 0.35f;
			float body = Sweep(t, 230f, 55f, 22f) * MathF.Exp(-t * 16f) * 0.9f;
			float hiss = lowPassed * MathF.Exp(-t * 35f) * 0.7f;
			float click = Sine(2800f, t) * MathF.Exp(-t * 260f) * 0.35f;
			return body + hiss + click;
		};
	}

	private static Func<float, float> NailImpactSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			lowPassed += (Noise() - lowPassed) * 0.15f;
			return lowPassed * MathF.Exp(-t * 45f) * 1.4f + Sine(160f, t) * MathF.Exp(-t * 35f) * 0.5f;
		};
	}

	private static Func<float, float> ReloadStartSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			lowPassed += (Noise() - lowPassed) * 0.4f;
			return lowPassed * MathF.Exp(-t * 80f) * 0.9f + Sine(520f, t) * MathF.Exp(-t * 70f) * 0.5f;
		};
	}

	private static Func<float, float> DashWhooshSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			// Noise through a filter that opens then closes, with a quick swell.
			float swell = MathF.Sin(MathF.PI * Math.Clamp(t / 0.3f, 0f, 1f));
			lowPassed += (Noise() - lowPassed) * (0.05f + 0.45f * swell);
			return lowPassed * swell * 1.6f + Sweep(t, 90f, 50f, 8f) * MathF.Exp(-t * 18f) * 0.5f;
		};
	}

	private static Func<float, float> GrowlSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			lowPassed += (Noise() - lowPassed) * 0.12f;
			float swell = MathF.Min(t / 0.6f, 1f);
			float rumble = Sweep(t, 70f, 150f, 3f) * (0.6f + 0.4f * Sine(23f, t));
			float scrape = lowPassed * (0.5f + 0.5f * MathF.Max(0f, Sine(5f, t)));
			return (rumble * 0.8f + scrape * 1.2f) * swell * (t > 0.68f ? MathF.Exp(-(t - 0.68f) * 40f) : 1f);
		};
	}

	private static Func<float, float> PlayerHurtSample()
	{
		float lowPassed = 0f;
		return t =>
		{
			lowPassed += (Noise() - lowPassed) * 0.08f;
			return Sweep(t, 140f, 70f, 15f) * MathF.Exp(-t * 14f) * 0.9f + lowPassed * MathF.Exp(-t * 25f) * 1.5f;
		};
	}

	private static float Clack(float t, float pitch)
	{
		if (t < 0f)
			return 0f;
		return (Noise() * 0.6f + Sine(pitch, t) * 0.6f) * MathF.Exp(-t * 120f);
	}

	private static float Sine(float frequency, float t) => MathF.Sin(Tau * frequency * t);

	/// <summary>Sine sweeping exponentially from f0 to f1.</summary>
	private static float Sweep(float t, float f0, float f1, float rate)
	{
		float phase = f1 * t + (f0 - f1) * (1f - MathF.Exp(-rate * t)) / rate;
		return MathF.Sin(Tau * phase);
	}

	private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

	private static AudioStreamWav Build(float duration, Func<float, float> sample)
	{
		int count = (int)(duration * SampleRate);
		var data = new byte[count * 2];
		float fadeSamples = SampleRate * 0.005f;

		for (int i = 0; i < count; i++)
		{
			float t = i / (float)SampleRate;
			float fade = Math.Min(1f, (count - i) / fadeSamples);
			float value = MathF.Tanh(sample(t) * 1.2f) * fade * 0.9f;
			short pcm = (short)(value * short.MaxValue);
			data[i * 2] = (byte)(pcm & 0xFF);
			data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
		}

		return new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = SampleRate,
			Stereo = false,
			Data = data,
		};
	}
}
