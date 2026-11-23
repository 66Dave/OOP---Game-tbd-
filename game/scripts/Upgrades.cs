using Godot;
using System.Collections.Generic;

// Upgrades bought with gold. Each kind of upgrade is its own class that inherits from Upgrade
// and only says what it does to the player (Apply). Cost and level logic is shared in the base class.

public abstract class Upgrade
{
	public string Name { get; }
	public int Level { get; private set; }
	public int MaxLevel { get; }

	private readonly int _baseCost;
	private readonly float _costGrowth;

	protected Upgrade(string name, int baseCost, float costGrowth, int maxLevel)
	{
		Name = name;
		_baseCost = baseCost;
		_costGrowth = costGrowth;
		MaxLevel = maxLevel;
	}

	// Each level costs more: baseCost, baseCost x growth, baseCost x growth^2, ...
	public int Cost => Mathf.RoundToInt(_baseCost * Mathf.Pow(_costGrowth, Level));
	public bool IsMaxed => Level >= MaxLevel;

	// Short text for the shop button, e.g. "+1 damage"
	public abstract string Effect { get; }

	public void Buy(PlayerArcher player)
	{
		Level++;
		Apply(player);
	}

	protected abstract void Apply(PlayerArcher player);
}

public class DamageUpgrade : Upgrade
{
	public DamageUpgrade() : base("Damage", baseCost: 10, costGrowth: 1.6f, maxLevel: 10) { }
	public override string Effect => "+1 damage";
	protected override void Apply(PlayerArcher player) => player.Damage += 1;
}

public class FireRateUpgrade : Upgrade
{
	public FireRateUpgrade() : base("Fire Rate", baseCost: 15, costGrowth: 1.6f, maxLevel: 8) { }
	public override string Effect => "+15% fire rate";
	protected override void Apply(PlayerArcher player) => player.SetAttackSpeed(player.AttackSpeed + 0.15f);
}

public class HealthUpgrade : Upgrade
{
	public HealthUpgrade() : base("Health", baseCost: 20, costGrowth: 1.8f, maxLevel: 5) { }
	public override string Effect => "+1 max HP & heal";
	protected override void Apply(PlayerArcher player) => player.AddMaxHealth(1);
}

// The list of upgrades for sale. Add a new upgrade class here and the HUD shows it automatically.
public class UpgradeShop
{
	public List<Upgrade> Upgrades { get; } = new()
	{
		new DamageUpgrade(),
		new FireRateUpgrade(),
		new HealthUpgrade(),
	};
}
