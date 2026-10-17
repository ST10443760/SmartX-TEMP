namespace SmartX.Shared.Enums;

/// <summary>
/// The physical quantity a live reading carries. Each maps to a different
/// native value type on the wire: float moisture, int wattage, bool valve state.
/// </summary>
public enum ReadingKind
{
    Moisture = 0,
    Wattage = 1,
    ValveState = 2
}
