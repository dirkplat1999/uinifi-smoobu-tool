using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class TestModeRuleRowViewModel : ObservableObject
{
    public required TestModeRuleType Type { get; init; }
    public required string Value { get; init; }
}

/// <summary>
/// Backs the Test Mode screen (Feature 9): when enabled, automation only runs for reservations
/// matching one of these phone numbers, emails, or guest names, so the app can be exercised safely
/// against a real Smoobu/UniFi Access account without messaging or provisioning real guests.
/// </summary>
public sealed partial class TestModeViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly ITestModeRuleStore _ruleStore;

    [ObservableProperty]
    private bool _testModeEnabled;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private TestModeRuleType _newRuleType = TestModeRuleType.Email;

    [ObservableProperty]
    private string _newRuleValue = "";

    public ObservableCollection<TestModeRuleRowViewModel> Rules { get; } = new();
    public TestModeRuleType[] RuleTypeOptions { get; } = Enum.GetValues<TestModeRuleType>();

    public TestModeViewModel(IAppSettingsStore settingsStore, ITestModeRuleStore ruleStore)
    {
        _settingsStore = settingsStore;
        _ruleStore = ruleStore;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsStore.GetAsync();
            TestModeEnabled = settings.TestModeEnabled;

            var rules = await _ruleStore.GetAllAsync();
            Rules.Clear();
            foreach (var rule in rules)
            {
                Rules.Add(new TestModeRuleRowViewModel { Type = rule.Type, Value = rule.Value });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load test mode settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTestModeAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsStore.GetAsync();
            settings.TestModeEnabled = TestModeEnabled;
            await _settingsStore.SaveAsync(settings);
            StatusMessage = TestModeEnabled
                ? "Test mode is ON - only matching reservations below will be processed."
                : "Test mode is OFF - all reservations are processed normally.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRuleValue))
        {
            StatusMessage = "Enter a value first.";
            return;
        }

        IsBusy = true;
        try
        {
            var rule = new TestModeRule { Type = NewRuleType, Value = NewRuleValue.Trim() };
            await _ruleStore.SaveAsync(rule);
            Rules.Add(new TestModeRuleRowViewModel { Type = rule.Type, Value = rule.Value });
            NewRuleValue = "";
            StatusMessage = "Rule added.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't add rule: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteRuleAsync(TestModeRuleRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _ruleStore.DeleteAsync(new TestModeRule { Type = row.Type, Value = row.Value });
            Rules.Remove(row);
            StatusMessage = "Rule removed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't remove rule: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
