namespace CmdRoguelike.Domain.Items;

public enum RestorationResource { Health, Mana }
public enum ConsumableUseResult { Success, NotFound, NotConsumable, NoNeed, Unavailable }
public sealed record ItemRestoration
{
	public RestorationResource Resource { get; }
	public int Percent { get; }
	public ItemRestoration(RestorationResource resource, int percent)
	{
		if (!Enum.IsDefined(resource) || percent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
		Resource = resource; Percent = percent;
	}
}
