using FloodLink.Contracts;
using FloodLink.Domain.Enums;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Placeholder test class confirming the test project compiles and xUnit is wired up.
/// Real tests will be added per member's scope starting Week 3-5.
/// </summary>
public class PlaceholderTests
{
    /// <summary>
    /// Trivial smoke test: confirms the test runner works and the Domain/Contracts
    /// projects can be referenced. This test must always pass.
    /// </summary>
    [Fact]
    public void WorkflowState_HasExpectedValues()
    {
        // The spec defines exactly 9 workflow states (Section 3.4).
        // This test will catch any accidental addition or removal of states.
        var states = Enum.GetValues<WorkflowState>();
        Assert.Equal(9, states.Length);

        // Verify the ordered pipeline states are defined
        Assert.Contains(WorkflowState.Triage, states);
        Assert.Contains(WorkflowState.Matching, states);
        Assert.Contains(WorkflowState.Routing, states);
        Assert.Contains(WorkflowState.Validating, states);
        Assert.Contains(WorkflowState.PendingApproval, states);
        Assert.Contains(WorkflowState.Approved, states);
        Assert.Contains(WorkflowState.Rejected, states);
        Assert.Contains(WorkflowState.RevisionRequested, states);
        Assert.Contains(WorkflowState.Failed, states);
    }

    /// <summary>
    /// Confirms <see cref="AgentResult{T}.Ok"/> factory creates a successful result.
    /// </summary>
    [Fact]
    public void AgentResult_Ok_SetsSuccessTrue()
    {
        var result = AgentResult<string>.Ok("hello");

        Assert.True(result.Success);
        Assert.Equal("hello", result.Data);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// Confirms <see cref="AgentResult{T}.Fail"/> factory creates a failed result.
    /// </summary>
    [Fact]
    public void AgentResult_Fail_SetsSuccessFalse()
    {
        var result = AgentResult<string>.Fail("SOME_ERROR", "Something went wrong");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("SOME_ERROR", result.ErrorCode);
        Assert.Equal("Something went wrong", result.ErrorMessage);
    }
}
