using Godot;

// Main character: moves left/right only and shoots nonstop.
// Scene layout:
//   Player (CharacterBody3D)     <- attach this script
//   ├── AnimatedSprite3D         <- looping "shoot" animation (ArcherShoot.png)
//   ├── CollisionShape3D         <- CapsuleShape3D
//   └── Muzzle (Node3D)          <- where arrows spawn (near the bow)
public partial class PlayerArcher : CharacterBody3D
{
	[Export] public float MoveSpeed = 6f;      // left/right speed
	[Export] public float LimitX = 4f;         // how far from the middle he can go
	[Export] public PackedScene ArrowScene;    // drag arrow.tscn here in the Inspector
	[Export] public int ReleaseFrame = 5;      // frame in ArcherShoot where the arrow leaves the bow

	private AnimatedSprite3D _sprite;
	private Node3D _muzzle;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
		_muzzle = GetNode<Node3D>("Muzzle");

		_sprite.FrameChanged += OnFrameChanged;
		_sprite.Play("shoot");                 // loops forever = shoots forever
	}

	public override void _PhysicsProcess(double delta)
	{
		// A / Left Arrow = left, D / Right Arrow = right (set up in Project Settings > Input Map)
		float dir = Input.GetAxis("move_left", "move_right");

		Vector3 pos = Position;
		pos.X = Mathf.Clamp(pos.X + dir * MoveSpeed * (float)delta, -LimitX, LimitX);
		Position = pos;
	}

	// Spawn an arrow exactly when the animation releases the string
	private void OnFrameChanged()
	{
		if (_sprite.Frame != ReleaseFrame || ArrowScene == null)
			return;

		var arrow = ArrowScene.Instantiate<Node3D>();
		GetParent().AddChild(arrow);
		arrow.GlobalPosition = _muzzle.GlobalPosition;
	}
}
