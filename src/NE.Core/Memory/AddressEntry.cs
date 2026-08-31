namespace NEManager.Core.Memory;

/// <summary>地址簿条目（RH Editor 的收藏列表）。</summary>
public record AddressEntry(
    string Description,
    IntPtr Address,
    ValueType Type,
    string OriginalValue,
    string CurrentValue,
    string FrozenValue,
    bool IsFrozen,
    string Module)
{
    public int Size => Type switch
    {
        ValueType.Int32 or ValueType.Float => 4,
        ValueType.Int64 or ValueType.Double => 8,
        ValueType.String => CurrentValue.Length * 2,
        _ => OriginalValue.Length
    };
}
