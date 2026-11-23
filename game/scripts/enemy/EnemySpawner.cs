using Godot;

// Spawns spiders up the road at random X positions, getting faster over time.
// Each spawn picks a SpiderVariant by weight: lots of small ones, some normal, rare big red ones.
// Place this node in Game.tscn up the road (negative Z), at the player's height.
public partial class EnemySpawner : Node3D
{
	[Export] public PackedScene SpiderScene;       // drag spider.tscn here
	[Export] public float LimitX = 4f;             // match PlayerArcher.LimitX so spiders stay on the road
	[Export] public float StartInterval = 1.5f;    // seconds between spawns at the start
	[Export] public float MinInterval = 0.5f;      // fastest it will ever spawn
	[Export] public float SpeedUpPerSpawn = 0.02f; // interval shrinks by this much each spawn

	// Leave empty to use the built-in Small / Normal / Big set below
	[Export] public Godot.Collections.Array<SpiderVariant> Variants = new();

	private float _interval;
	private float _timer;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		_interval = StartInterval;
		_timer = _interval;

		if (Variants.Count == 0)
		{
			//                              name    weight  hp  speed scale  tint                          atkSpd points gold
			Variants.Add(new SpiderVariant("Small",  6f,   1, 5.5f, 0.7f, Colors.White,                  1.5f,  10,  1, 1));
			Variants.Add(new SpiderVariant("Normal", 3f,   3, 4.0f, 1.0f, Colors.White,                  1.0f,  25,  2, 3));
			Variants.Add(new SpiderVariant("Big",    1f,  10, 2.5f, 1.6f, new Color(1f, 0.35f, 0.35f),   0.8f, 100,  8, 12));
		}

		if (SpiderScene == null)
			GD.PushWarning("EnemySpawner: drag spider.tscn into Spider Scene in the Inspector.");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (SpiderScene == null)
			return;

		_timer -= (float)delta;
		if (_timer > 0f)
			return;

		Spawn();

		// Difficulty ramp: spawn a little faster each time, down to MinInterval
		_interval = Mathf.Max(MinInterval, _interval - SpeedUpPerSpawn);
		_timer = _interval;
	}

	private void Spawn()
	{
		var spider = SpiderScene.Instantiate<Node3D>();

		// Apply the variant BEFORE adding to the tree so Spider._Ready sees the right values
		if (spider is Spider s)
		{
			SpiderVariant v = PickVariant();
			s.MaxHealth = v.Health;
			s.Speed = v.Speed;
			s.Tint = v.Tint;
			s.AttackSpeed = v.AttackSpeed;
			s.Points = v.Points;
			s.GoldMin = v.GoldMin;
			s.GoldMax = v.GoldMax;
			s.RoadLimitX = LimitX;
			s.Scale = Vector3.One * v.Scale;
		}

		GetParent().AddChild(spider);

		float x = _rng.RandfRange(-LimitX, LimitX);
		spider.GlobalPosition = GlobalPosition + new Vector3(x, 0f, 0f);
	}

	// Weighted random pick: Weight 6 is six times as likely as Weight 1
	private SpiderVariant PickVariant()
	{
		float total = 0f;
		foreach (SpiderVariant v in Variants)
			total += v.Weight;

		float roll = _rng.RandfRange(0f, total);
		foreach (SpiderVariant v in Variants)
		{
			roll -= v.Weight;
			if (roll <= 0f)
				return v;
		}
		return Variants[Variants.Count - 1];
	}
}
