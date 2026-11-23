using Godot;

// One kind of spider (small, normal, big...). EnemySpawner picks one at random by Weight.
// [GlobalClass] lets you create these in the editor: FileSystem > right-click > New > Resource > SpiderVariant.
[GlobalClass]
public partial class SpiderVariant : Resource
{
	[Export] public string Name = "Normal";
	[Export] public float Weight = 1f;          // higher = more common (relative to the other variants)
	[Export] public int Health = 3;
	[Export] public float Speed = 4f;
	[Export] public float Scale = 1f;           // size multiplier (also scales the hitbox)
	[Export] public Color Tint = Colors.White;  // multiplies the sprite's colors
	[Export] public float AttackSpeed = 1f;     // bites per second multiplier
	[Export] public int Points = 25;            // score when killed
	[Export] public int GoldMin = 1;            // gold dropped when killed
	[Export] public int GoldMax = 3;

	public SpiderVariant() { }

	public SpiderVariant(string name, float weight, int health, float speed, float scale, Color tint,
						 float attackSpeed, int points, int goldMin, int goldMax)
	{
		Name = name;
		Weight = weight;
		Health = health;
		Speed = speed;
		Scale = scale;
		Tint = tint;
		AttackSpeed = attackSpeed;
		Points = points;
		GoldMin = goldMin;
		GoldMax = goldMax;
	}
}
