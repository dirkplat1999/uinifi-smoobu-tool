namespace UnifiSmoobuTool.Core.Models;

public sealed class TestModeRule
{
    public required TestModeRuleType Type { get; init; }
    public required string Value { get; init; }
}

public enum TestModeRuleType
{
    PhoneNumber,
    Email,
    GuestName,
}
