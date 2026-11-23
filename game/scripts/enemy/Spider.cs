using Godot;

// Spider enemy: walks down the road toward the player, stops in front of them and
// plays an attack animation that damages the player on a chosen frame.
// Flashes white when shot and dies after MaxHealth arrows.
// Scene layout (spider.tscn):
//   Spider (Area3D)              <- attach this script
//   ├── AnimatedSprite3D         <- SpriteFrames with a walk animation and an "attack" animation
//   └── CollisionShape3D         <- BoxShape3D, about 1 m deep on Z and tall enough to reach arrow height
public partial class Spider : Area3D
{
	[ExportGroup("Stats")]
	[Export] public int MaxHealth = 3;           // arrows needed to kill it
	[Export] public float Speed = 4f;            // meters per second down the road (toward +Z)
	[Export] public Color Tint = Colors.White;   // set by EnemySpawner per variant (e.g. red for big spiders)
	[Export] public int Points = 25;             // score for killing it
	[Export] public int GoldMin = 1;             // gold dropped on death (random between min and max)
	[Export] public int GoldMax = 3;

	[ExportGroup("Movement")]
	[Export] public float RoadLimitX = 4f;       // wanders within -RoadLimitX..RoadLimitX
	[Export] public float WanderSpeed = 1.2f;    // sideways speed while wandering
	[Export] public float AggroRange = 7f;       // starts chasing when this close to the player (on Z)
	[Export] public float SteerSpeed = 2.5f;     // sideways speed while chasing the player

	[ExportGroup("Attack")]
	[Export] public float AttackRange = 1.0f;    // stops this far in front of the player (on Z)
	[Export] public float AttackWidth = 0.8f;    // must be this close on X to land a hit
	[Export] public string WalkAnimation = "default";
	[Export] public string AttackAnimation = "attack";
	[Export] public int AttackHitFrame = 3;      // frame of the attack animation where the bite lands
	[Export] public float AttackSpeed = 1f;      // 1 = normal, 2 = twice as many bites per second
	[Export] public bool InvulnerableWhileAttacking = true; // arrows can't hurt it mid-attack
	[Export] public float FallbackBiteInterval = 1f; // seconds between bites if there's no attack animation

	[ExportGroup("Other")]
	[Export] public float DespawnBehind = 3f;    // safety cleanup if it ever gets behind the player
	[Export] public float FlashTime = 0.1f;      // seconds it stays white when hit
	[Export(PropertyHint.Range, "1,32")] public int EnemyLayer = 2;

	public bool IsDead { get; private set; }     // Arrow.cs ignores dead spiders

	private PlayerArcher _player;
	private AnimatedSprite3D _sprite;
	private ShaderMaterial _flashMaterial;
	private bool _hasAttackAnim;
	private bool _attacking;
	private int _health;
	private float _flashTimer;
	private float _biteTimer;                    // only used when there's no attack animation

	private readonly RandomNumberGenerator _rng = new();
	private float _wanderTargetX;
	private float _wanderTimer;

	// One shared white-flash shader for all spiders (each spider gets its own material)
	private static Shader _flashShader;
	private const string FlashShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D texture_albedo : source_color, filter_linear;

void vertex() {
	// Billboard: always face the camera, like the normal sprite
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	// Keep the node's scale so the flash matches the sprite's size
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 tex = texture(texture_albedo, UV);
	ALBEDO = vec3(1.0);                 // solid white
	ALPHA = tex.a * COLOR.a;            // keep the spider's shape
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
";

	public override void _Ready()
	{
		AddToGroup("enemies");                   // Arrow.cs looks for this group
		_health = MaxHealth;

		// Small per-spider differences so a crowd doesn't move in lockstep
		_rng.Randomize();
		Speed *= _rng.RandfRange(0.85f, 1.15f);
		_wanderTimer = 0f;                       // pick a wander target on the first frame

		_player = GetTree().GetFirstNodeInGroup("player") as PlayerArcher;
		if (_player == null)
			GD.PushWarning("Spider: no PlayerArcher in the 'player' group. Is the new PlayerArcher.cs built?");

		// Arrows detect the spider; the spider itself doesn't need to detect anything
		CollisionLayer = 0;
		CollisionMask = 0;
		SetCollisionLayerValue(EnemyLayer, true);
		Monitoring = false;

		_sprite = GetNodeOrNull<AnimatedSprite3D>("AnimatedSprite3D");
		if (_sprite != null)
		{
			_hasAttackAnim = _sprite.SpriteFrames.HasAnimation(AttackAnimation);
			if (!_hasAttackAnim)
				GD.PushWarning($"Spider: no '{AttackAnimation}' animation in SpriteFrames, so it bites on a timer instead.");

			_sprite.Modulate = Tint;
			_sprite.FrameChanged += OnFrameChanged;
			PlayAnim(WalkAnimation);

			_flashShader ??= new Shader { Code = FlashShaderCode };
			_flashMaterial = new ShaderMaterial { Shader = _flashShader };
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		UpdateFlash(dt);

		if (IsDead || _player == null)
			return;

		Vector3 pos = GlobalPosition;
		Vector3 target = _player.GlobalPosition;
		float stopZ = target.Z - AttackRange;    // the line in front of the player where it stops

		bool aggro = target.Z - pos.Z < AggroRange;

		if (aggro)
		{
			// Close to the player: chase their lane (keeps following while attacking)
			pos.X = Mathf.MoveToward(pos.X, target.X, SteerSpeed * dt);
		}
		else
		{
			// Far away: wander between random spots on the road, pausing on new targets now and then
			_wanderTimer -= dt;
			if (_wanderTimer <= 0f)
			{
				_wanderTargetX = _rng.RandfRange(-RoadLimitX, RoadLimitX);
				_wanderTimer = _rng.RandfRange(1f, 3f);
			}
			pos.X = Mathf.MoveToward(pos.X, _wanderTargetX, WanderSpeed * dt);
		}

		// Walk forward until reaching the stop line
		if (pos.Z < stopZ)
			pos.Z = Mathf.Min(pos.Z + Speed * dt, stopZ);

		GlobalPosition = pos;

		bool atStopLine = pos.Z >= stopZ - 0.01f;
		bool linedUp = Mathf.Abs(pos.X - target.X) < AttackWidth;

		if (atStopLine && linedUp)
		{
			if (!_attacking)
			{
				_attacking = true;
				_biteTimer = FallbackBiteInterval / AttackSpeed;
				PlayAnim(AttackAnimation);
			}

			// No attack animation: bite on a timer instead of on a frame (spider stays either way)
			if (!_hasAttackAnim)
			{
				_biteTimer -= dt;
				if (_biteTimer <= 0f)
				{
					_player.TakeHit();
					_biteTimer = FallbackBiteInterval / AttackSpeed;
				}
			}
		}
		else if (_attacking)
		{
			// Player dodged sideways: go back to walking toward their lane
			_attacking = false;
			PlayAnim(WalkAnimation);
		}

		// Safety cleanup
		if (pos.Z > target.Z + DespawnBehind)
			QueueFree();
	}

	// Damage lands on one specific frame of the attack animation (like the archer's ReleaseFrame)
	private void OnFrameChanged()
	{
		if (IsDead || !_attacking || _player == null)
			return;
		if (_sprite.Animation != AttackAnimation || _sprite.Frame != AttackHitFrame)
			return;

		// Only hurts if the player is still in front of it at the moment of the bite
		if (Mathf.Abs(GlobalPosition.X - _player.GlobalPosition.X) < AttackWidth)
			_player.TakeHit();
	}

	// Called by Arrow.cs
	public void TakeDamage(int amount)
	{
		if (IsDead)
			return;

		// Mid-attack and protected: arrows bounce off, no damage
		if (_attacking && InvulnerableWhileAttacking)
			return;

		_health -= amount;
		Flash();

		if (_health <= 0)
		{
			// Stay for the flash so the hit is visible, then QueueFree in UpdateFlash
			IsDead = true;
			_attacking = false;
			SetDeferred(Area3D.PropertyName.Monitorable, false);

			// Reward: points + gold coins
			GameManager.Instance?.EnemyKilled(Points, _rng.RandiRange(GoldMin, GoldMax), GlobalPosition);
		}
	}

	private void PlayAnim(string name)
	{
		if (_sprite == null)
			return;

		// Only the attack animation is sped up by AttackSpeed
		_sprite.SpeedScale = name == AttackAnimation ? AttackSpeed : 1f;

		if (_sprite.SpriteFrames.HasAnimation(name))
			_sprite.Play(name);
		else
			_sprite.Play();                      // fall back to whatever the default animation is
	}

	private void Flash()
	{
		if (_sprite == null)
		{
			if (IsDead) QueueFree();
			return;
		}

		// Give the shader the texture the sprite is showing right now
		Texture2D tex = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		if (tex is AtlasTexture atlas)
			tex = atlas.Atlas;                   // sprite-sheet frames: use the whole sheet (UVs point into it)
		_flashMaterial.SetShaderParameter("texture_albedo", tex);

		_sprite.MaterialOverride = _flashMaterial;
		_flashTimer = FlashTime;
	}

	private void UpdateFlash(float dt)
	{
		if (_flashTimer <= 0f)
			return;

		_flashTimer -= dt;
		if (_flashTimer > 0f)
			return;

		if (_sprite != null)
			_sprite.MaterialOverride = null;     // back to the normal sprite

		if (IsDead)
			QueueFree();
	}
}
