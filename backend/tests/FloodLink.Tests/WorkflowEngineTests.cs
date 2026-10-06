using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Unit tests for <see cref="WorkflowEngine.TryTransition"/>.
///
/// Coverage (Phase 2, Task 8):
///   - One test per valid transition (all 12 rows from the Phase 1 table)
///   - Sample of invalid transitions that must be rejected
///   - Failed branch from at least two different source states (verifies FailedAtState)
/// </summary>
public class WorkflowEngineTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates a WorkflowRun starting in the given state.</summary>
    private static WorkflowRun RunIn(WorkflowState state) =>
        new() { Objective = "Test", CurrentState = state };

    // ═══════════════════════════════════════════════════════════════════════════
    // Valid transitions — all 12 rows from the Phase 1 transition table
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Triage_To_Matching_IsValid()
    {
        var run = RunIn(WorkflowState.Triage);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Matching));
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
    }

    [Fact]
    public void Triage_To_Failed_IsValid()
    {
        var run = RunIn(WorkflowState.Triage);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Failed));
        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Triage, run.FailedAtState); // must record origin stage
    }

    [Fact]
    public void Matching_To_Routing_IsValid()
    {
        var run = RunIn(WorkflowState.Matching);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Routing));
        Assert.Equal(WorkflowState.Routing, run.CurrentState);
    }

    [Fact]
    public void Matching_To_Failed_IsValid()
    {
        var run = RunIn(WorkflowState.Matching);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Failed));
        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Matching, run.FailedAtState);
    }

    [Fact]
    public void Routing_To_Validating_IsValid()
    {
        var run = RunIn(WorkflowState.Routing);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Validating));
        Assert.Equal(WorkflowState.Validating, run.CurrentState);
    }

    [Fact]
    public void Routing_To_Failed_IsValid()
    {
        var run = RunIn(WorkflowState.Routing);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Failed));
        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Routing, run.FailedAtState);
    }

    [Fact]
    public void Validating_To_PendingApproval_IsValid()
    {
        var run = RunIn(WorkflowState.Validating);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.PendingApproval));
        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);
    }

    [Fact]
    public void Validating_To_Failed_IsValid()
    {
        var run = RunIn(WorkflowState.Validating);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Failed));
        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Validating, run.FailedAtState);
    }

    [Fact]
    public void PendingApproval_To_Approved_IsValid()
    {
        var run = RunIn(WorkflowState.PendingApproval);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Approved));
        Assert.Equal(WorkflowState.Approved, run.CurrentState);
    }

    [Fact]
    public void PendingApproval_To_Rejected_IsValid()
    {
        var run = RunIn(WorkflowState.PendingApproval);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Rejected));
        Assert.Equal(WorkflowState.Rejected, run.CurrentState);
    }

    [Fact]
    public void PendingApproval_To_RevisionRequested_IsValid()
    {
        var run = RunIn(WorkflowState.PendingApproval);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.RevisionRequested));
        Assert.Equal(WorkflowState.RevisionRequested, run.CurrentState);
    }

    [Fact]
    public void RevisionRequested_To_Matching_IsValid()
    {
        var run = RunIn(WorkflowState.RevisionRequested);
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.Matching));
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Invalid transitions — must return false, run must be unchanged
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Triage_To_Approved_IsInvalid()
    {
        var run = RunIn(WorkflowState.Triage);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Approved));
        Assert.Equal(WorkflowState.Triage, run.CurrentState); // unchanged
    }

    [Fact]
    public void Triage_To_Validating_IsInvalid()
    {
        var run = RunIn(WorkflowState.Triage);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Validating));
        Assert.Equal(WorkflowState.Triage, run.CurrentState);
    }

    [Fact]
    public void Matching_To_Approved_IsInvalid()
    {
        var run = RunIn(WorkflowState.Matching);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Approved));
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
    }

    [Fact]
    public void PendingApproval_To_Triage_IsInvalid()
    {
        var run = RunIn(WorkflowState.PendingApproval);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Triage));
        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);
    }

    [Fact]
    public void Approved_To_Anything_IsInvalid()
    {
        // Approved is a terminal state — no further transitions are allowed
        var run = RunIn(WorkflowState.Approved);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Rejected));
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Failed));
        Assert.Equal(WorkflowState.Approved, run.CurrentState);
    }

    [Fact]
    public void Rejected_To_Anything_IsInvalid()
    {
        // Rejected is a terminal state — no further transitions are allowed
        var run = RunIn(WorkflowState.Rejected);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Approved));
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Triage));
        Assert.Equal(WorkflowState.Rejected, run.CurrentState);
    }

    [Fact]
    public void Failed_To_Anything_IsInvalid()
    {
        // Failed is a terminal state — no automatic recovery, retry is a new orchestrator action
        var run = RunIn(WorkflowState.Failed);
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Triage));
        Assert.False(WorkflowEngine.TryTransition(run, WorkflowState.Matching));
        Assert.Equal(WorkflowState.Failed, run.CurrentState);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FailedAtState — verified from multiple source states (Phase 2, Task 8)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FailedAtState_IsNull_BeforeAnyFailure()
    {
        var run = RunIn(WorkflowState.Triage);
        Assert.Null(run.FailedAtState);
    }

    [Fact]
    public void FailedAtState_RecordsRoutingStage()
    {
        var run = RunIn(WorkflowState.Routing);
        WorkflowEngine.TryTransition(run, WorkflowState.Failed);
        Assert.Equal(WorkflowState.Routing, run.FailedAtState);
    }

    [Fact]
    public void FailedAtState_RecordsValidatingStage()
    {
        var run = RunIn(WorkflowState.Validating);
        WorkflowEngine.TryTransition(run, WorkflowState.Failed);
        Assert.Equal(WorkflowState.Validating, run.FailedAtState);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UpdatedAt — must be set on every successful transition
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SuccessfulTransition_SetsUpdatedAt()
    {
        var run = RunIn(WorkflowState.Triage);
        var before = run.UpdatedAt;

        // Tiny sleep so the timestamp is guaranteed to differ
        System.Threading.Thread.Sleep(10);
        WorkflowEngine.TryTransition(run, WorkflowState.Matching);

        Assert.True(run.UpdatedAt > before);
    }

    [Fact]
    public void InvalidTransition_DoesNotChangeUpdatedAt()
    {
        var run = RunIn(WorkflowState.Triage);
        var before = run.UpdatedAt;

        WorkflowEngine.TryTransition(run, WorkflowState.Approved); // invalid
        Assert.Equal(before, run.UpdatedAt); // must not mutate the run at all
    }
}
