using Godot;

// Main character: moves left/right only and shoots nonstop.
// Damage, fire rate (AttackSpeed) and max health can be upgraded with gold (see Upgrades.cs).
// Scene layout:
//   Player (CharacterBody3D)     <- attach this script
//   ├── AnimatedSprite3D         <- looping "shoot" animation (ArcherShoot.png)
//   ├── CollisionShape3D         <- CapsuleShape3D
//   └── Muzzle (Node3D)          <- where arrows spawn (near the bow)
public partial class PlayerArcher : CharacterBody3D
{
	[Signal] public delegate void HealthChangedEventHandler(int health, int maxHealth);

	[Export] public float MoveSpeed = 6f;        // left/right speed
	[Export] public float LimitX = 4f;           // how far from the middle he can go
	[Export] public PackedScene ArrowScene;      // drag arrow.tscn here in the Inspector
	[Export] public int ReleaseFrame = 5;        // frame in ArcherShoot where the arrow leaves the bow

	[ExportGroup("Upgradable")]
	[Export] public int Damage = 1;              // damage per arrow
	[Export] public float AttackSpeed = 1f;      // 1 = normal, 2 = shoots twice as fast
	[Export] public int MaxHealth = 3;
	[Export] public float HitInvincibility = 0.5f; // seconds of safety after being bitten

	public int Health { get; private set; }

	private AnimatedSprite3D _sprite;
	private Node3D _muzzle;
	private float _invincibleTimer;

	public override void _EnterTree()
	{
		AddToGroup("player");                    // Spider, Coin and HUD find the player through this
	}

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
		_muzzle = GetNode<Node3D>("Muzzle");
		Health = MaxHealth;

		_sprite.FrameChanged += OnFrameChanged;
		_sprite.SpeedScale = AttackSpeed;
		_sprite.Play("shoot");                   // loops forever = shoots forever

		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// A / Left Arrow = left, D / Right Arrow = right (set up in Project Settings > Input Map)
		float dir = Input.GetAxis("move_left", "move_right");

		Vector3 pos = Position;
		pos.X = Mathf.Clamp(pos.X + dir * MoveSpeed * dt, -LimitX, LimitX);
		Position = pos;

		// Blink while invincible
		if (_invincibleTimer > 0f)
		{
			_invincibleTimer -= dt;
			_sprite.Visible = _invincibleTimer <= 0f || Mathf.PosMod(_invincibleTimer, 0.1f) < 0.05f;
		}
	}

	// Called by Spider.cs on each bite
	public void TakeHit()
	{
		if (_invincibleTimer > 0f || Health <= 0)
			return;

		Health--;
		_invincibleTimer = HitInvincibility;
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

		if (Health <= 0)
		{
			// Deferred: reloading inside a physics callback can crash
			GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
		}
	}

	// ---- Called by upgrades ----

	public void SetAttackSpeed(float value)
	{
		AttackSpeed = value;
		_sprite.SpeedScale = value;
	}

	public void AddMaxHealth(int amount)
	{
		MaxHealth += amount;
		Health = Mathf.Min(Health + amount, MaxHealth);  // also heals
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	// Spawn an arrow exactly when the animation releases the string
	private void OnFrameChanged()
	{
		if (_sprite.Frame != ReleaseFrame || ArrowScene == null)
			return;

		var arrow = ArrowScene.Instantiate<Node3D>();
		if (arrow is Arrow a)
			a.Damage = Damage;

		GetParent().AddChild(arrow);
		arrow.GlobalPosition = _muzzle.GlobalPosition;
	}
}
