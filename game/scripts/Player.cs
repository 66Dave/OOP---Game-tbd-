using Godot;

// Attach to a CharacterBody2D that has an AnimatedSprite2D child named "AnimatedSprite2D"
// (plus a CollisionShape2D).
// Sheets: 128x128 frames, one row per direction (8 rows). The number of frames per row
// is worked out from each image's width, so sheets with different frame counts still work.
public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 100f;
	[Export] public Texture2D IdleSheet;  // drag Idle.png here in the Inspector
	[Export] public Texture2D WalkSheet;  // drag Walk.png here
	[Export] public Texture2D CastSheet;  // drag CastSpell.png here

	// Which sheet row faces each direction.
	// Index: 0=right, 1=down-right, 2=down, 3=down-left, 4=left, 5=up-left, 6=up, 7=up-right
	// Edit these in the Inspector after testing so each direction plays the right row.
	[Export] public int[] RowFor = { 0, 1, 2, 3, 4, 5, 6, 7 };

	private const int FrameSize = 128;
	private const int Rows = 8;

	private AnimatedSprite2D _sprite;
	private int _facing = 2;      // start facing down
	private bool _casting = false;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		AddSheet(frames, "idle", IdleSheet, loop: true);
		AddSheet(frames, "walk", WalkSheet, loop: true);
		AddSheet(frames, "cast", CastSheet, loop: false);
		_sprite.SpriteFrames = frames;

		_sprite.AnimationFinished += () => _casting = false;
		_sprite.Play($"idle_{RowFor[_facing]}");
	}

	// Cuts one sheet into 8 animations, e.g. walk_0 ... walk_7
	private static void AddSheet(SpriteFrames frames, string name, Texture2D sheet, bool loop)
	{
		if (sheet == null)
		{
			GD.PushError($"Player: the {name} sheet is empty. Drag it into the Inspector.");
			return;
		}

		int columns = sheet.GetWidth() / FrameSize;

		for (int row = 0; row < Rows; row++)
		{
			string anim = $"{name}_{row}";
			frames.AddAnimation(anim);
			frames.SetAnimationLoop(anim, loop);
			frames.SetAnimationSpeed(anim, 15); // frames per second

			for (int col = 0; col < columns; col++)
			{
				var frame = new AtlasTexture
				{
					Atlas = sheet,
					Region = new Rect2(col * FrameSize, row * FrameSize, FrameSize, FrameSize)
				};
				frames.AddFrame(anim, frame);
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Cast on Space/Enter (ui_accept). Character stands still while casting.
		if (!_casting && Input.IsActionJustPressed("ui_accept"))
		{
			_casting = true;
			Velocity = Vector2.Zero;
			_sprite.Play($"cast_{RowFor[_facing]}");
			return;
		}
		if (_casting) return;

		Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Velocity = input * Speed;
		MoveAndSlide();

		if (input != Vector2.Zero)
		{
			_facing = DirectionIndex(input);
			_sprite.Play($"walk_{RowFor[_facing]}");
		}
		else
		{
			_sprite.Play($"idle_{RowFor[_facing]}");
		}
	}

	// Turns a movement vector into 0-7 (0 = right, going clockwise because Godot's Y points down)
	private static int DirectionIndex(Vector2 v)
	{
		float slice = Mathf.Tau / 8f;
		return Mathf.PosMod(Mathf.RoundToInt(v.Angle() / slice), 8);
	}
}
