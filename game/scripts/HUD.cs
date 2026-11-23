using Godot;
using System.Collections.Generic;

// Game HUD: hearts (top-left), score (top-center), gold (top-right), upgrade shop cards (bottom).
// Builds all of its UI in code. Add a CanvasLayer named "HUD" to Game.tscn and attach this script.
// Buy upgrades by clicking a card or pressing 1 / 2 / 3. Tab hides/shows the shop.
public partial class HUD : CanvasLayer
{
	[Export] public Key ToggleShopKey = Key.Tab;

	// ---------- Palette ----------
	private static readonly Color PanelBg = new(0.07f, 0.06f, 0.10f, 0.85f);
	private static readonly Color PanelBorder = new(0.30f, 0.26f, 0.40f);
	private static readonly Color Gold = new(1f, 0.80f, 0.25f);
	private static readonly Color Afford = new(0.45f, 0.90f, 0.50f);
	private static readonly Color CantAfford = new(1f, 0.42f, 0.42f);
	private static readonly Color TextDim = new(0.68f, 0.66f, 0.78f);
	private static readonly Color PipEmpty = new(0.24f, 0.22f, 0.30f);
	private static readonly Color HeartRed = new(0.93f, 0.26f, 0.32f);

	private class ShopCard
	{
		public Button Root;
		public Label Name, Effect, Price;
		public TextureRect PriceIcon;
		public HBoxContainer Pips;
	}

	private Control _root;
	private Label _scoreValue, _bestValue, _goldValue;
	private PanelContainer _heartsPanel, _goldPanel;
	private HBoxContainer _hearts;
	private HBoxContainer _shop;
	private readonly List<ShopCard> _cards = new();

	private Texture2D _coinIcon, _heartFull, _heartEmpty;
	private PlayerArcher _player;
	private int _lastScore, _lastGold, _lastHealth = -1;

	public override void _Ready()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_heartFull = MakeHeart(true);
		_heartEmpty = MakeHeart(false);
		_coinIcon = MakeCoinIcon();

		BuildHearts();
		BuildScore();
		BuildGold();
		BuildShop();

		var gm = GameManager.Instance;
		if (gm == null)
		{
			GD.PushWarning("HUD: no GameManager in the scene.");
			return;
		}

		gm.ScoreChanged += OnScoreChanged;
		gm.GoldChanged += OnGoldChanged;
		gm.ShopChanged += RefreshShop;

		_lastScore = gm.Score;
		_lastGold = gm.Gold;
		_scoreValue.Text = gm.Score.ToString("N0");
		_bestValue.Text = $"BEST  {GameManager.HighScore:N0}";
		_goldValue.Text = gm.Gold.ToString("N0");
		RefreshShop();

		// Player may be ready after the HUD, so connect next frame
		Callable.From(ConnectPlayer).CallDeferred();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		if (key.Keycode == ToggleShopKey)
		{
			_shop.Visible = !_shop.Visible;
			return;
		}

		int index = key.Keycode switch
		{
			Key.Key1 => 0,
			Key.Key2 => 1,
			Key.Key3 => 2,
			Key.Key4 => 3,
			_ => -1,
		};

		if (index >= 0 && index < _cards.Count && _shop.Visible)
			TryBuy(index);
	}

	// ---------- Events ----------

	private void ConnectPlayer()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerArcher;
		if (_player == null)
			return;

		_player.HealthChanged += OnHealthChanged;
		OnHealthChanged(_player.Health, _player.MaxHealth);
	}

	private void OnScoreChanged(int score)
	{
		_scoreValue.Text = score.ToString("N0");
		if (score - _lastScore >= 10)            // only pop for kills, not the steady survival points
			Punch(_scoreValue, 1.25f);
		_lastScore = score;
	}

	private void OnGoldChanged(int gold)
	{
		_goldValue.Text = gold.ToString("N0");
		if (gold > _lastGold)
			Punch(_goldPanel, 1.12f);
		_lastGold = gold;
	}

	private void OnHealthChanged(int health, int maxHealth)
	{
		foreach (Node child in _hearts.GetChildren())
		{
			_hearts.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < maxHealth; i++)
			_hearts.AddChild(MakeIcon(i < health ? _heartFull : _heartEmpty, new Vector2(33, 30)));

		if (_lastHealth >= 0 && health < _lastHealth)
			Flash(_heartsPanel, CantAfford);      // took damage
		else if (_lastHealth >= 0 && health > _lastHealth)
			Punch(_heartsPanel, 1.1f);            // healed / upgraded
		_lastHealth = health;
	}

	private void TryBuy(int index)
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return;

		if (gm.TryBuyUpgrade(index))
		{
			Punch(_cards[index].Root, 1.08f);
			Flash(_cards[index].Root, Gold);
		}
		else
		{
			Shake(_cards[index].Root);            // can't afford or maxed
		}
	}

	private void RefreshShop()
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return;

		for (int i = 0; i < _cards.Count; i++)
		{
			Upgrade u = gm.Shop.Upgrades[i];
			ShopCard c = _cards[i];
			bool canAfford = gm.Gold >= u.Cost;

			c.Name.Text = u.Name;
			c.Effect.Text = u.Effect;

			// Level pips: filled = bought
			foreach (Node child in c.Pips.GetChildren())
			{
				c.Pips.RemoveChild(child);
				child.QueueFree();
			}
			var pipSize = u.MaxLevel <= 5 ? new Vector2(18, 6) : new Vector2(10, 6);
			for (int lv = 0; lv < u.MaxLevel; lv++)
			{
				c.Pips.AddChild(new ColorRect
				{
					Color = lv < u.Level ? Gold : PipEmpty,
					CustomMinimumSize = pipSize,
					MouseFilter = Control.MouseFilterEnum.Ignore,
				});
			}

			Color border;
			if (u.IsMaxed)
			{
				c.Price.Text = "MAXED";
				c.Price.AddThemeColorOverride("font_color", Gold);
				c.PriceIcon.Visible = false;
				border = Gold.Darkened(0.35f);
				c.Root.Modulate = Colors.White;
			}
			else
			{
				c.Price.Text = u.Cost.ToString("N0");
				c.Price.AddThemeColorOverride("font_color", canAfford ? Gold : CantAfford);
				c.PriceIcon.Visible = true;
				border = canAfford ? Afford : PanelBorder;
				c.Root.Modulate = canAfford ? Colors.White : new Color(1, 1, 1, 0.7f);
			}

			c.Root.AddThemeStyleboxOverride("normal", CardStyle(border, 0f));
			c.Root.AddThemeStyleboxOverride("hover", CardStyle(u.IsMaxed ? border : Gold, 0.05f));
			c.Root.AddThemeStyleboxOverride("pressed", CardStyle(Gold, 0.12f));
		}
	}

	// ---------- UI building ----------

	private void BuildHearts()
	{
		_heartsPanel = MakePanel(12, 10, 8);
		_heartsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		_heartsPanel.Position = new Vector2(16, 14);
		_root.AddChild(_heartsPanel);

		_hearts = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_hearts.AddThemeConstantOverride("separation", 4);
		_heartsPanel.AddChild(_hearts);
	}

	private void BuildScore()
	{
		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", -6);
		box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
		box.GrowHorizontal = Control.GrowDirection.Both;
		box.OffsetTop = 8;
		_root.AddChild(box);

		box.AddChild(MakeLabel("SCORE", 14, TextDim, HorizontalAlignment.Center));
		_scoreValue = MakeLabel("0", 40, Colors.White, HorizontalAlignment.Center);
		box.AddChild(_scoreValue);
		_bestValue = MakeLabel("BEST  0", 14, TextDim, HorizontalAlignment.Center);
		box.AddChild(_bestValue);
	}

	private void BuildGold()
	{
		_goldPanel = MakePanel(20, 14, 6);
		_goldPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		_goldPanel.GrowHorizontal = Control.GrowDirection.Begin;
		_goldPanel.OffsetRight = -16;
		_goldPanel.OffsetLeft = -16;
		_goldPanel.OffsetTop = 14;
		_root.AddChild(_goldPanel);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		_goldPanel.AddChild(row);

		row.AddChild(MakeIcon(_coinIcon, new Vector2(28, 28)));
		_goldValue = MakeLabel("0", 26, Gold);
		row.AddChild(_goldValue);
	}

	private void BuildShop()
	{
		_shop = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_shop.AddThemeConstantOverride("separation", 12);
		_shop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
		_shop.GrowHorizontal = Control.GrowDirection.Both;
		_shop.GrowVertical = Control.GrowDirection.Begin;
		_shop.OffsetBottom = -14;
		_shop.OffsetTop = -14;
		_root.AddChild(_shop);

		int count = GameManager.Instance?.Shop.Upgrades.Count ?? 0;
		for (int i = 0; i < count; i++)
			_cards.Add(BuildCard(i));
	}

	private ShopCard BuildCard(int index)
	{
		var card = new ShopCard();

		card.Root = new Button
		{
			CustomMinimumSize = new Vector2(190, 112),
			FocusMode = Control.FocusModeEnum.None,   // so A/D keep moving the player
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
		};
		card.Root.AddThemeStyleboxOverride("normal", CardStyle(PanelBorder, 0f));
		card.Root.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		card.Root.AddThemeStyleboxOverride("disabled", CardStyle(PanelBorder, 0f));
		card.Root.Pressed += () => TryBuy(index);
		_shop.AddChild(card.Root);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 9);
		margin.AddThemeConstantOverride("margin_bottom", 9);
		card.Root.AddChild(margin);

		var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 5);
		margin.AddChild(col);

		// Header: [key badge] Name
		var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		header.AddThemeConstantOverride("separation", 8);
		col.AddChild(header);

		var badge = MakePanel(5, 7, 1, new Color(1, 1, 1, 0.12f), new Color(1, 1, 1, 0.25f));
		badge.AddChild(MakeLabel((index + 1).ToString(), 13, Colors.White, HorizontalAlignment.Center));
		header.AddChild(badge);

		card.Name = MakeLabel("", 19, Colors.White);
		header.AddChild(card.Name);

		// Level pips
		card.Pips = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		card.Pips.AddThemeConstantOverride("separation", 3);
		col.AddChild(card.Pips);

		// Effect
		card.Effect = MakeLabel("", 14, TextDim);
		col.AddChild(card.Effect);

		// Price
		var priceRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		priceRow.AddThemeConstantOverride("separation", 6);
		col.AddChild(priceRow);

		card.PriceIcon = MakeIcon(_coinIcon, new Vector2(20, 20));
		priceRow.AddChild(card.PriceIcon);
		card.Price = MakeLabel("", 20, Gold);
		priceRow.AddChild(card.Price);

		return card;
	}

	// ---------- Small helpers ----------

	private static PanelContainer MakePanel(int radius, int padX, int padY, Color? bg = null, Color? border = null)
	{
		var style = new StyleBoxFlat
		{
			BgColor = bg ?? PanelBg,
			BorderColor = border ?? PanelBorder,
			ShadowColor = new Color(0, 0, 0, 0.35f),
			ShadowSize = 4,
		};
		style.SetCornerRadiusAll(radius);
		style.SetBorderWidthAll(2);
		style.ContentMarginLeft = padX;
		style.ContentMarginRight = padX;
		style.ContentMarginTop = padY;
		style.ContentMarginBottom = padY;

		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static StyleBoxFlat CardStyle(Color border, float lighten)
	{
		var style = new StyleBoxFlat
		{
			BgColor = PanelBg.Lightened(lighten),
			BorderColor = border,
			ShadowColor = new Color(0, 0, 0, 0.4f),
			ShadowSize = 5,
		};
		style.SetCornerRadiusAll(12);
		style.SetBorderWidthAll(2);
		return style;
	}

	private static Label MakeLabel(string text, int size, Color color,
								   HorizontalAlignment align = HorizontalAlignment.Left)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		label.AddThemeConstantOverride("outline_size", size >= 24 ? 8 : 5);
		return label;
	}

	private static TextureRect MakeIcon(Texture2D tex, Vector2 size)
	{
		return new TextureRect
		{
			Texture = tex,
			CustomMinimumSize = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,  // crisp pixel art
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	// ---------- Juice (little animations) ----------

	private void Punch(Control c, float amount)
	{
		c.PivotOffset = c.Size / 2f;
		c.Scale = Vector2.One * amount;
		c.CreateTween()
		 .TweenProperty(c, "scale", Vector2.One, 0.2f)
		 .SetTrans(Tween.TransitionType.Back)
		 .SetEase(Tween.EaseType.Out);
	}

	private void Flash(CanvasItem c, Color color)
	{
		Color normal = c.Modulate;
		c.Modulate = color;
		c.CreateTween().TweenProperty(c, "modulate", normal, 0.25f);
	}

	private void Shake(Control c)
	{
		float x = c.Position.X;
		var t = c.CreateTween();
		t.TweenProperty(c, "position:x", x + 8, 0.04f);
		t.TweenProperty(c, "position:x", x - 8, 0.04f);
		t.TweenProperty(c, "position:x", x + 4, 0.04f);
		t.TweenProperty(c, "position:x", x, 0.04f);
	}

	// ---------- Generated pixel icons ----------

	private static Texture2D MakeHeart(bool full)
	{
		string[] shape =
		{
			" XX   XX ",
			"XXXX XXXX",
			"XXXXXXXXX",
			"XXXXXXXXX",
			" XXXXXXX ",
			"  XXXXX  ",
			"   XXX   ",
			"    X    ",
		};

		Color fill = full ? HeartRed : new Color(0.22f, 0.20f, 0.27f);
		Color outline = new(0.06f, 0.04f, 0.07f);
		var img = Image.CreateEmpty(11, 10, false, Image.Format.Rgba8);

		for (int y = 0; y < shape.Length; y++)
			for (int x = 0; x < shape[y].Length; x++)
				if (shape[y][x] == 'X')
					img.SetPixel(x + 1, y + 1, fill);

		// 1px dark outline around the shape
		var copy = (Image)img.Duplicate();
		for (int y = 0; y < 10; y++)
			for (int x = 0; x < 11; x++)
			{
				if (copy.GetPixel(x, y).A > 0) continue;
				bool touches = false;
				for (int dy = -1; dy <= 1 && !touches; dy++)
					for (int dx = -1; dx <= 1 && !touches; dx++)
					{
						int nx = x + dx, ny = y + dy;
						if (nx >= 0 && ny >= 0 && nx < 11 && ny < 10 && copy.GetPixel(nx, ny).A > 0)
							touches = true;
					}
				if (touches) img.SetPixel(x, y, outline);
			}

		if (full)
		{
			img.SetPixel(2, 2, new Color(1f, 0.75f, 0.78f)); // shine
			img.SetPixel(3, 2, new Color(1f, 0.55f, 0.6f));
		}

		return ImageTexture.CreateFromImage(img);
	}

	private static Texture2D MakeCoinIcon()
	{
		var gm = GameManager.Instance;
		if (gm?.CoinSheet != null)
		{
			int w = gm.CoinSheet.GetWidth() / Mathf.Max(1, gm.CoinFrames);
			return new AtlasTexture { Atlas = gm.CoinSheet, Region = new Rect2(0, 0, w, gm.CoinSheet.GetHeight()) };
		}

		// Fallback: simple yellow circle if no coin sheet is set
		var img = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
		for (int y = 0; y < 12; y++)
			for (int x = 0; x < 12; x++)
				if (new Vector2(x - 5.5f, y - 5.5f).Length() <= 5.5f)
					img.SetPixel(x, y, Gold);
		return ImageTexture.CreateFromImage(img);
	}
}
