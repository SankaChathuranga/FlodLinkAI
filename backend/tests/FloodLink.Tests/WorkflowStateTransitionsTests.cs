using FloodLink.Domain;
using FloodLink.Domain.Enums;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Tests for the legal workflow state-transition map that guards the
/// approval/dispatch flow (it delegates to WorkflowEngine, the single transition table).
/// </summary>
public class WorkflowStateTransitionsTests
{
    [Fact]
    public void ForwardPipeline_IsAllowed()
    {
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.Triage, WorkflowState.Matching));
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.Matching, WorkflowState.Routing));
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.Routing, WorkflowState.Validating));
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.Validating, WorkflowState.PendingApproval));
    }

    [Fact]
    public void ApprovalDecisionsFromPendingApproval_AreAllowed()
    {
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.PendingApproval, WorkflowState.Approved));
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.PendingApproval, WorkflowState.Rejected));
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.PendingApproval, WorkflowState.RevisionRequested));
    }

    [Fact]
    public void RevisionLoop_BackToMatching_IsAllowed()
    {
        Assert.True(WorkflowStateTransitions.IsAllowed(WorkflowState.RevisionRequested, WorkflowState.Matching));
    }

    [Fact]
    public void AnyNonTerminalStage_CanFail()
    {
        foreach (var stage in new[]
                 {
                     WorkflowState.Triage, WorkflowState.Matching, WorkflowState.Routing,
                     WorkflowState.Validating, WorkflowState.PendingApproval, WorkflowState.RevisionRequested
                 })
        {
            Assert.True(WorkflowStateTransitions.IsAllowed(stage, WorkflowState.Failed));
        }
    }

    [Fact]
    public void SkippingStages_IsNotAllowed()
    {
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Triage, WorkflowState.Validating));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Matching, WorkflowState.PendingApproval));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Routing, WorkflowState.PendingApproval));
    }

    [Fact]
    public void GoingBackwards_IsNotAllowed()
    {
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.PendingApproval, WorkflowState.Validating));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Matching, WorkflowState.Triage));
    }

    [Fact]
    public void ApprovingWithoutValidation_IsNotAllowed()
    {
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Validating, WorkflowState.Approved));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Routing, WorkflowState.Approved));
    }

    [Fact]
    public void TerminalStates_AllowNothing()
    {
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Approved, WorkflowState.Failed));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Rejected, WorkflowState.PendingApproval));
        Assert.False(WorkflowStateTransitions.IsAllowed(WorkflowState.Failed, WorkflowState.Triage));
    }
}