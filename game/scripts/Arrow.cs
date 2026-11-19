using Godot;

// Arrow projectile: flies forward and destroys anything in the "enemies" group it touches.
// Scene layout:
//   Arrow (Area3D)            <- attach this script
//   ├── Sprite3D              <- Arrow.png
//   └── CollisionShape3D      <- small BoxShape3D
public partial class Arrow : Area3D
{
	[Export] public float Speed = 25f;      // meters per second
	[Export] public float Lifetime = 2f;    // seconds before it disappears

	private float _age = 0f;

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Vector3.Forward is (0, 0, -1) = away from the camera, up the road
		Position += Vector3.Forward * Speed * (float)delta;

		_age += (float)delta;
		if (_age >= Lifetime)
			QueueFree();
	}

	private void OnAreaEntered(Area3D area)
	{
		if (area.IsInGroup("enemies"))
		{
			area.QueueFree();   // kill the enemy
			QueueFree();        // remove the arrow
		}
	}
}
