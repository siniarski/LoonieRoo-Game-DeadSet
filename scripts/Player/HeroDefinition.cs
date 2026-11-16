using Godot;

namespace Deadset.Player;

/// <summary>
/// Who the player is playing. For the prototype it only carries what makes a hero different
/// in the core loop: their always-on melee. The Tradie is the only hero for now.
/// </summary>
[GlobalClass]
public partial class HeroDefinition : Resource
{
	[Export] public string DisplayName { get; set; } = "The Tradie";
	/// <summary>A scene whose root is a QuickMeleeWeapon: the melee this hero always carries.</summary>
	[Export] public PackedScene QuickMelee { get; set; }
}
