namespace CmdRoguelike.Domain.Combat;

public enum DamageAspect { Physical, Fire }

/// <summary>Authored innate protection. Equipment armor still supplies card block, not passive reduction.</summary>
public sealed class DamageProfile
{
	public static DamageProfile Unprotected { get; } = new();
	public int PhysicalArmor { get; }
	public int FireMultiplierPercent { get; }
	public DamageProfile(int physicalArmor = 0, int fireMultiplierPercent = 100)
	{
		if (physicalArmor < 0 || fireMultiplierPercent is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(physicalArmor));
		PhysicalArmor = physicalArmor;
		FireMultiplierPercent = fireMultiplierPercent;
	}
	public int Apply(int power, DamageAspect aspect)
	{
		if (power < 0 || !Enum.IsDefined(aspect)) throw new ArgumentOutOfRangeException(nameof(power));
		return aspect == DamageAspect.Fire
			? checked((int)((long)power * FireMultiplierPercent / 100))
			: checked((int)(((long)power * 10 + 9 + PhysicalArmor) / (10L + PhysicalArmor)));
	}
}
