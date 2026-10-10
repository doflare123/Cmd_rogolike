using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Rewards;
using Godot;

namespace CmdRoguelike.World;

public enum RewardClaimResult { Success, Full, AlreadyClaimed, Unavailable }

/// <summary>Unowned loot properties become an inventory-owned instance only on successful pickup.</summary>
public sealed class RewardEntry
{
	public Guid Id { get; } = Guid.NewGuid();
	public RewardItem Item { get; }
	public bool IsClaimed { get; private set; }
	internal RewardEntry(RewardItem item) => Item = item;
	internal RewardClaimResult Claim(ActorInventory inventory)
	{
		if (IsClaimed) return RewardClaimResult.AlreadyClaimed;
		if (inventory.TryAcquireRolled(Item.Roll, Item.Quantity) != InventoryResult.Success) return RewardClaimResult.Full;
		IsClaimed = true;
		return RewardClaimResult.Success;
	}
}

public sealed class RewardCache
{
	public Guid Id { get; } = Guid.NewGuid();
	public Vector2I Position { get; }
	public CombatReward Reward { get; }
	public IReadOnlyList<RewardEntry> Entries { get; }
	public bool HasRemaining => Entries.Any(e => !e.IsClaimed);
	public bool IsRevealed { get; private set; }
	internal RewardCache(Vector2I position, CombatReward reward)
	{
		Position = position; Reward = reward;
		Entries = Array.AsReadOnly(reward.Items.Select(i => new RewardEntry(i)).ToArray());
	}
	internal void Reveal() => IsRevealed = true;
}
