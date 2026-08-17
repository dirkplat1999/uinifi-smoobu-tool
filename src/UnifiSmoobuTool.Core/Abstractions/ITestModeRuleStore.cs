using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface ITestModeRuleStore
{
    Task<IReadOnlyList<TestModeRule>> GetAllAsync(CancellationToken ct = default);

    Task SaveAsync(TestModeRule rule, CancellationToken ct = default);

    Task DeleteAsync(TestModeRule rule, CancellationToken ct = default);
}
