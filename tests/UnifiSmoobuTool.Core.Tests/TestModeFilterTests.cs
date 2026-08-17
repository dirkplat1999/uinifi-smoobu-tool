using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class TestModeFilterTests
{
    private static Reservation MakeReservation(string firstName = "Alex", string lastName = "Doe",
        string? email = "alex@example.com", string? phone = "+31 6 1234 5678") => new()
    {
        Id = 1,
        ApartmentId = 1,
        ApartmentName = "Canal View",
        GuestFirstName = firstName,
        GuestLastName = lastName,
        GuestEmail = email,
        GuestPhone = phone,
        Arrival = new DateOnly(2026, 1, 1),
        Departure = new DateOnly(2026, 1, 3),
    };

    [Fact]
    public void ShouldProcess_ReturnsTrue_WhenTestModeDisabled()
    {
        var reservation = MakeReservation();
        Assert.True(TestModeFilter.ShouldProcess(reservation, testModeEnabled: false, rules: Array.Empty<TestModeRule>()));
    }

    [Fact]
    public void ShouldProcess_ReturnsFalse_WhenTestModeEnabledAndNoRuleMatches()
    {
        var reservation = MakeReservation();
        var rules = new[] { new TestModeRule { Type = TestModeRuleType.Email, Value = "someone-else@example.com" } };

        Assert.False(TestModeFilter.ShouldProcess(reservation, testModeEnabled: true, rules));
    }

    [Fact]
    public void ShouldProcess_ReturnsTrue_WhenEmailMatchesAllowList()
    {
        var reservation = MakeReservation(email: "Alex@Example.com");
        var rules = new[] { new TestModeRule { Type = TestModeRuleType.Email, Value = "alex@example.com" } };

        Assert.True(TestModeFilter.ShouldProcess(reservation, testModeEnabled: true, rules));
    }

    [Fact]
    public void ShouldProcess_ReturnsTrue_WhenPhoneMatchesIgnoringFormatting()
    {
        var reservation = MakeReservation(phone: "(06) 1234-5678");
        var rules = new[] { new TestModeRule { Type = TestModeRuleType.PhoneNumber, Value = "0612345678" } };

        Assert.True(TestModeFilter.ShouldProcess(reservation, testModeEnabled: true, rules));
    }

    [Fact]
    public void ShouldProcess_ReturnsTrue_WhenGuestNameContainsRuleValue()
    {
        var reservation = MakeReservation(firstName: "Dirk", lastName: "Plat Test");
        var rules = new[] { new TestModeRule { Type = TestModeRuleType.GuestName, Value = "Plat Test" } };

        Assert.True(TestModeFilter.ShouldProcess(reservation, testModeEnabled: true, rules));
    }
}
