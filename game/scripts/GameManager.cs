using Godot;

// Keeps score and gold for one run, drops coins, and runs the upgrade shop.
// Add one Node named "GameManager" to Game.tscn and attach this script.
// Anything can reach it with GameManager.Instance.
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Signal] public delegate void ScoreChangedEventHandler(int score);
	[Signal] public delegate void GoldChangedEventHandler(int gold);
	[Signal] public delegate void ShopChangedEventHandler();

	[ExportGroup("Score")]
	[Export] public float PointsPerSecond = 2f;  // survival points, on top of kill points

	[ExportGroup("Gold")]
	[Export] public int StartingGold = 0;
	[Export] public Texture2D CoinSheet;         // drag CoinSpin.png here
	[Export] public int CoinFrames = 8;          // frames in the coin sheet (in one row)
	[Export] public float CoinPixelSize = 0.02f; // coin size in the world
	[Export] public int MaxCoinsPerDrop = 5;     // big drops are split into at most this many coins

	public int Score { get; private set; }
	public int Gold { get; private set; }
	public static int HighScore { get; private set; } // survives scene reloads (not game restarts)
	public UpgradeShop Shop { get; private set; }

	private float _survivalPoints;
	private SpriteFrames _coinFrames;

	public override void _EnterTree()
	{
		Instance = this;
		Shop = new UpgradeShop();
	}

	public override void _ExitTree()
	{
		if (Score > HighScore)
			HighScore = Score;
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		Gold = StartingGold;
		_coinFrames = BuildCoinFrames();
		if (_coinFrames == null)
			GD.PushWarning("GameManager: drag CoinSpin.png into Coin Sheet, or coins will be invisible.");
	}

	public override void _Process(double delta)
	{
		// Points for staying alive
		_survivalPoints += PointsPerSecond * (float)delta;
		if (_survivalPoints >= 1f)
		{
			int whole = (int)_survivalPoints;
			_survivalPoints -= whole;
			AddScore(whole);
		}
	}

	public void AddScore(int amount)
	{
		Score += amount;
		EmitSignal(SignalName.ScoreChanged, Score);
	}

	public void AddGold(int amount)
	{
		Gold += amount;
		EmitSignal(SignalName.GoldChanged, Gold);
		EmitSignal(SignalName.ShopChanged);      // affordability changed
	}

	// Called by Spider.cs when an arrow kills it
	public void EnemyKilled(int points, int gold, Vector3 position)
	{
		AddScore(points);
		DropGold(position, gold);
	}

	// Splits the gold into a few coins that pop out and roll toward the player
	public void DropGold(Vector3 position, int amount)
	{
		if (amount <= 0)
			return;

		int coins = Mathf.Clamp(amount, 1, MaxCoinsPerDrop);
		int each = amount / coins;
		int extra = amount % coins;

		for (int i = 0; i < coins; i++)
		{
			var coin = new Coin
			{
				Value = each + (i < extra ? 1 : 0),
				Frames = _coinFrames,
				PixelSize = CoinPixelSize,
				SpawnPosition = position,
			};
			// Deferred: this can be called from inside a physics callback (arrow hit)
			GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, coin);
		}
	}

	// Called by the HUD buttons / number keys
	public bool TryBuyUpgrade(int index)
	{
		if (index < 0 || index >= Shop.Upgrades.Count)
			return false;

		var player = GetTree().GetFirstNodeInGroup("player") as PlayerArcher;
		Upgrade upgrade = Shop.Upgrades[index];

		if (player == null || upgrade.IsMaxed || Gold < upgrade.Cost)
			return false;

		Gold -= upgrade.Cost;
		upgrade.Buy(player);

		EmitSignal(SignalName.GoldChanged, Gold);
		EmitSignal(SignalName.ShopChanged);
		return true;
	}

	private SpriteFrames BuildCoinFrames()
	{
		if (CoinSheet == null)
			return null;

		var frames = new SpriteFrames();
		frames.SetAnimationLoop("default", true);
		frames.SetAnimationSpeed("default", 12);

		int w = CoinSheet.GetWidth() / CoinFrames;
		int h = CoinSheet.GetHeight();
		for (int i = 0; i < CoinFrames; i++)
		{
			var frame = new AtlasTexture { Atlas = CoinSheet, Region = new Rect2(i * w, 0, w, h) };
			frames.AddFrame("default", frame);
		}
		return frames;
	}
}
