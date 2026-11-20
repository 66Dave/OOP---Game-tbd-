using Godot;

// Arrow projectile: flies forward and kills the first enemy it touches.
// Scene layout:
//   Arrow (Area3D)            <- attach this script
//   ├── Sprite3D              <- Arrow.png (Alpha Cut = Discard)
//   └── CollisionShape3D      <- BoxShape3D, about 0.5 m long on Z so it can't skip past spiders
public partial class Arrow : Area3D
{
	[Export] public float Speed = 25f;      // meters per second
	[Export] public float Lifetime = 2f;    // seconds before it disappears

	// Physics layer numbers (match Project Settings > Layer Names > 3D Physics)
	[Export(PropertyHint.Range, "1,32")] public int ProjectileLayer = 3;
	[Export(PropertyHint.Range, "1,32")] public int EnemyLayer = 2;

	private float _age = 0f;
	private bool _hit = false;              // stops one arrow from killing two spiders

	public override void _Ready()
	{
		// Arrow lives on the projectile layer and only looks for enemies
		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionLayerValue(ProjectileLayer, true);
		SetCollisionMaskValue(EnemyLayer, true);
		Monitorable = false;                // nothing needs to detect the arrow

		// Works whether the spider's hitbox is an Area3D or a physics body
		AreaEntered += OnHit;
		BodyEntered += OnHit;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Vector3.Forward is (0, 0, -1) = away from the camera, up the road
		Position += Vector3.Forward * Speed * (float)delta;

		_age += (float)delta;
		if (_age >= Lifetime)
			QueueFree();
	}

	private void OnHit(Node3D other)
	{
		if (_hit)
			return;

		Node enemy = FindEnemy(other);
		if (enemy == null)
			return;

		if (enemy is Spider spider)
		{
			if (spider.IsDead)
				return;              // already dying: let the arrow fly on
			spider.TakeDamage(1);    // flashes white, dies after MaxHealth hits
		}
		else
		{
			enemy.QueueFree();       // any other enemy type: one-hit kill
		}

		_hit = true;
		QueueFree();                 // remove the arrow
	}

	// Walks up from the touched node until it finds the node in the "enemies" group.
	// So it works if the group is on the hitbox OR on the spider's root.
	private static Node FindEnemy(Node node)
	{
		while (node != null)
		{
			if (node.IsInGroup("enemies"))
				return node;
			node = node.GetParent();
		}
		return null;
	}
}
