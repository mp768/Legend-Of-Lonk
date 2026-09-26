using System.Text.RegularExpressions;
using Godot;

[GlobalClass]
// Inherenting from Area2D, because all affectables will be Area2D anyways.
// Would've used a tagged union instead if I could.
public partial class Affectables : Area2D
{
	public enum EffectType
	{
		DAMAGE,
		HEALTH,
		RUPEES,
		KEYS,
	}

	public int Value { get; set; }
	public Vector2? Direction { get; set; }
	public EffectType Type { get; set; }
}
