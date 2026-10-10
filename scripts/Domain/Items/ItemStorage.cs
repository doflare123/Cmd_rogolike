namespace CmdRoguelike.Domain.Items;

/// <summary>Base-owned items survive the death of a hero. Transfers preserve item identity.</summary>
public sealed class ItemStorage
{
	private readonly List<ItemInstance> _items = new();
	public Guid Id { get; }
	public int Capacity { get; }
	public IReadOnlyList<ItemInstance> Items => _items.AsReadOnly();
	public ItemStorage(int capacity = 64, Guid? id = null)
	{
		if (capacity < 1 || capacity > 1000) throw new ArgumentOutOfRangeException(nameof(capacity));
		Id = id ?? Guid.NewGuid();
		if (Id == Guid.Empty) throw new ArgumentException("Empty storage identity.");
		Capacity = capacity;
	}
	internal void Attach(ItemInstance item)
	{
		if (item.OwnerId != Id || item.Location != ItemLocation.Storage || item.Slot is not null
			|| _items.Count >= Capacity || _items.Any(i => i.Id == item.Id))
			throw new ArgumentException("Invalid storage item.");
		_items.Add(item);
	}
	internal void Detach(ItemInstance item) => _items.Remove(item);
	internal void Supply(ItemRoll roll, int quantity = 1) => Attach(new ItemInstance(Id, roll, quantity)
		{ Location = ItemLocation.Storage });
}
