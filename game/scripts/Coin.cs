using Godot;

// A gold coin. Created by GameManager.DropGold (no scene needed).
// Pops up out of the spider, lands, rolls down the road toward the player,
// and gets pulled in (magnet) when the player is close. Missed coins are lost.
public partial class Coin : Node3D
{
	public int Value = 1;
	public SpriteFrames Frames;
	public float PixelSize = 0.02f;
	public Vector3 SpawnPosition;

	public float RoadSpeed = 4f;      // rolls toward the player at this speed
	public float MagnetRange = 2.5f;  // gets pulled in when this close
	public float MagnetSpeed = 9f;
	public float CollectRange = 0.7f;
	public float DespawnBehind = 3f;
	public float Gravity = 18f;

	private Node3D _player;
	private Vector3 _velocity;
	private float _groundY;
	private bool _landed;

	public override void _Ready()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		_groundY = SpawnPosition.Y + 0.15f;      // coin's center sits a little above the road
		GlobalPosition = SpawnPosition + Vector3.Up * 0.4f;
		_velocity = new Vector3(rng.RandfRange(-1.5f, 1.5f), rng.RandfRange(3f, 5f), rng.RandfRange(-1f, 0.5f));

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;

		if (Frames != null)
		{
			var sprite = new AnimatedSprite3D
			{
				SpriteFrames = Frames,
				PixelSize = PixelSize,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
				TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
			};
			AddChild(sprite);
			sprite.Play();
			sprite.Frame = rng.RandiRange(0, Frames.GetFrameCount("default") - 1); // not all in sync
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		Vector3 pos = GlobalPosition;

		if (!_landed)
		{
			// Pop out with a little arc
			_velocity.Y -= Gravity * dt;
			pos += _velocity * dt;
			if (pos.Y <= _groundY)
			{
				pos.Y = _groundY;
				_landed = true;
			}
		}
		else
		{
			pos.Z += RoadSpeed * dt;
		}

		if (_player != null)
		{
			Vector3 target = _player.GlobalPosition;
			var flat = new Vector2(target.X - pos.X, target.Z - pos.Z);
			float dist = flat.Length();

			if (_landed && dist < MagnetRange)
			{
				Vector2 step = flat.Normalized() * Mathf.Min(MagnetSpeed * dt, dist);
				pos.X += step.X;
				pos.Z += step.Y;
				dist = new Vector2(target.X - pos.X, target.Z - pos.Z).Length();
			}

			if (dist < CollectRange)
			{
				GameManager.Instance?.AddGold(Value);
				QueueFree();
				return;
			}

			if (pos.Z > target.Z + DespawnBehind)
			{
				QueueFree();
				return;
			}
		}

		GlobalPosition = pos;
	}
}
