using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Domain;

/// <summary>The aggregate root of the Tickets context. The lifecycle is the transition table below - declared once,
/// read by every method and by AllowedActions(). Every method appends exactly one TicketActivity and raises one
/// domain event. There is no public status setter.</summary>
/// <remarks>ITransferableOrganisationScoped: the write guard judges a modified ticket by its original scope, so
/// TransferTo can move it out of the sender's scope. Only TransferTo changes DepartmentId/BranchId.</remarks>
public sealed class Ticket : AuditableEntity<TicketId>, ISoftDeletable, IHasRowVersion, ITransferableOrganisationScoped
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private sealed record StatusTransition(IReadOnlyList<TicketStatus> From, TicketStatus To);

    private static readonly IReadOnlyList<TicketStatus> WorkingStatuses =
        [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

    // THE transition table (Story 15 product rules). Actions not listed here are the "working" actions: allowed in
    // WorkingStatuses, status unchanged.
    private static readonly Dictionary<TicketAction, StatusTransition> StatusTransitions =
        new()
        {
            [TicketAction.Open] = new([TicketStatus.New, TicketStatus.PendingCustomer, TicketStatus.OnHold], TicketStatus.Open),
            [TicketAction.AwaitCustomer] = new([TicketStatus.Open, TicketStatus.OnHold], TicketStatus.PendingCustomer),
            [TicketAction.PutOnHold] = new([TicketStatus.Open, TicketStatus.PendingCustomer], TicketStatus.OnHold),
            [TicketAction.Resolve] = new(WorkingStatuses, TicketStatus.Resolved),
            [TicketAction.Close] = new([TicketStatus.Resolved], TicketStatus.Closed),
            [TicketAction.Reopen] = new([TicketStatus.Resolved, TicketStatus.Closed], TicketStatus.Open),
            [TicketAction.Cancel] = new(WorkingStatuses, TicketStatus.Cancelled),
        };

    private readonly List<TicketActivity> _activities = [];

    public TicketNumber Number { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid? ContactId { get; private set; }

    public TicketChannel Channel { get; private set; }

    public CategoryId? CategoryId { get; private set; }

    public TicketPriority Priority { get; private set; } = TicketPriority.Normal;

    public TicketStatus Status { get; private set; } = TicketStatus.New;

    public Guid? AssignedAgentId { get; private set; }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    /// <summary>Set by Story 18 (first agent reply). Never set in Story 15.</summary>
    public DateTimeOffset? FirstRespondedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>Bumped by every activity append, so the root row (and its rowversion check) is always in the UPDATE -
    /// Customer.Revision's reasoning. Do not remove.</summary>
    public int Revision { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<TicketActivity> Activities => _activities.AsReadOnly();

    private Ticket()
    {
        Number = null!;
        Subject = string.Empty;
        Description = string.Empty;
    }

    private Ticket(
        TicketId id, TicketNumber number, string subject, string description, Guid customerId, Guid? contactId,
        TicketChannel channel, TicketPriority priority, TenantId tenantId, BranchId branchId, DepartmentId departmentId) : base(id)
    {
        Number = number;
        Subject = subject;
        Description = description;
        CustomerId = customerId;
        ContactId = contactId;
        Channel = channel;
        Priority = priority;
        TenantId = tenantId;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary><paramref name="category"/> is read, not mutated (the Department.Create(Branch) precedent).</summary>
    public static Ticket Create(
        TicketNumber number, string subject, string description, Guid customerId, Guid? contactId, TicketChannel channel,
        Category? category, TicketPriority priority, TenantId tenantId, BranchId branchId, DepartmentId departmentId,
        Guid actorId, DateTimeOffset nowUtc, TicketId? id = null)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(departmentId);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(TicketDomainErrors.ScopeRequired);
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException(TicketDomainErrors.CustomerRequired);
        }

        EnsureActor(actorId);

        var ticket = new Ticket(
            id ?? TicketId.New(), number, NormaliseSubject(subject), NormaliseDescription(description), customerId,
            contactId is { } c && c != Guid.Empty ? c : null, EnsureDefined(channel), EnsureDefined(priority),
            tenantId, branchId, departmentId);

        if (category is not null)
        {
            ticket.EnsureCategoryFits(category);
            ticket.CategoryId = category.Id;
        }

        ticket.Record(TicketActivityKind.Created, actorId, null, TicketStatus.New.ToString(), nowUtc);
        ticket.Raise(new TicketCreated(ticket.Id, nowUtc));
        return ticket;
    }

    public bool IsAllowedInCurrentStatus(TicketAction action) =>
        StatusTransitions.TryGetValue(action, out StatusTransition? transition)
            ? transition.From.Contains(Status)
            : WorkingStatuses.Contains(Status);

    public bool CanPerform(TicketAction action) => action switch
    {
        TicketAction.Claim => AssignedAgentId is null && IsAllowedInCurrentStatus(action),
        TicketAction.Unassign => AssignedAgentId is not null && IsAllowedInCurrentStatus(action),
        _ => IsAllowedInCurrentStatus(action),
    };

    /// <summary>Derived from the same table - what Story 20's UI offers (before permission filtering).</summary>
    public IReadOnlyList<TicketAction> AllowedActions() => [.. Enum.GetValues<TicketAction>().Where(CanPerform)];

    public void Open(Guid actorId, DateTimeOffset nowUtc) =>
        ChangeStatus(TicketAction.Open, TicketActivityKind.Opened, actorId, nowUtc);

    public void AwaitCustomer(Guid actorId, DateTimeOffset nowUtc) =>
        ChangeStatus(TicketAction.AwaitCustomer, TicketActivityKind.AwaitingCustomer, actorId, nowUtc);

    public void PutOnHold(Guid actorId, DateTimeOffset nowUtc) =>
        ChangeStatus(TicketAction.PutOnHold, TicketActivityKind.PutOnHold, actorId, nowUtc);

    public void Resolve(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Resolve, TicketActivityKind.Resolved, actorId, nowUtc);
        ResolvedAtUtc = nowUtc;
    }

    public void Close(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Close, TicketActivityKind.Closed, actorId, nowUtc);
        ClosedAtUtc = nowUtc;
    }

    public void Reopen(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Reopen, TicketActivityKind.Reopened, actorId, nowUtc);
        ResolvedAtUtc = null;
        ClosedAtUtc = null;
    }

    public void Cancel(Guid actorId, DateTimeOffset nowUtc) =>
        ChangeStatus(TicketAction.Cancel, TicketActivityKind.Cancelled, actorId, nowUtc);

    public void AssignTo(Guid agentId, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Assign);

        if (agentId == Guid.Empty)
        {
            throw new DomainException(TicketDomainErrors.AssigneeRequired);
        }

        if (AssignedAgentId == agentId)
        {
            throw new DomainException(TicketDomainErrors.AssigneeUnchanged);
        }

        Guid? previous = AssignedAgentId;
        AssignedAgentId = agentId;
        Record(TicketActivityKind.Assigned, actorId, previous?.ToString(), agentId.ToString(), nowUtc);
        Raise(new TicketAssignmentChanged(Id, previous, agentId, nowUtc));
    }

    /// <summary>The caller takes an unassigned ticket. Two concurrent claims are settled by the rowversion; the
    /// handler turns both refusals into 409 Tickets.AlreadyClaimed.</summary>
    public void Claim(Guid agentId, DateTimeOffset nowUtc)
    {
        EnsureActor(agentId);
        EnsureAllowed(TicketAction.Claim);

        if (AssignedAgentId is not null)
        {
            throw new DomainException(TicketDomainErrors.AlreadyAssigned);
        }

        AssignedAgentId = agentId;
        Record(TicketActivityKind.Claimed, agentId, null, agentId.ToString(), nowUtc);
        Raise(new TicketAssignmentChanged(Id, null, agentId, nowUtc));
    }

    public void Unassign(Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Unassign);

        if (AssignedAgentId is not { } previous)
        {
            throw new DomainException(TicketDomainErrors.NotAssigned);
        }

        AssignedAgentId = null;
        Record(TicketActivityKind.Unassigned, actorId, previous.ToString(), null, nowUtc);
        Raise(new TicketAssignmentChanged(Id, previous, null, nowUtc));
    }

    /// <summary>The only method that changes DepartmentId/BranchId. The receiving department triages it: assignee and
    /// category are cleared; the activity history stays.</summary>
    public void TransferTo(DepartmentId departmentId, BranchId branchId, Guid actorId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(departmentId);
        ArgumentNullException.ThrowIfNull(branchId);
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Transfer);

        if (departmentId.Equals(DepartmentId))
        {
            throw new DomainException(TicketDomainErrors.TransferToSameDepartment);
        }

        DepartmentId previous = DepartmentId;
        DepartmentId = departmentId;
        BranchId = branchId;
        AssignedAgentId = null;
        CategoryId = null;
        Record(TicketActivityKind.Transferred, actorId, previous.Value.ToString(), departmentId.Value.ToString(), nowUtc);
        Raise(new TicketTransferred(Id, previous, departmentId, nowUtc));
    }

    public void ChangePriority(TicketPriority priority, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.ChangePriority);
        TicketPriority next = EnsureDefined(priority);

        if (next == Priority)
        {
            throw new DomainException(TicketDomainErrors.PriorityUnchanged);
        }

        TicketPriority previous = Priority;
        Priority = next;
        Record(TicketActivityKind.PriorityChanged, actorId, previous.ToString(), next.ToString(), nowUtc);
        Raise(new TicketPriorityChanged(Id, previous, next, nowUtc));
    }

    public void ChangeCategory(Category category, Guid actorId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(category);
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.ChangeCategory);

        if (category.Id == CategoryId)
        {
            throw new DomainException(TicketDomainErrors.CategoryUnchanged);
        }

        EnsureCategoryFits(category);

        CategoryId? previous = CategoryId;
        CategoryId = category.Id;
        Record(TicketActivityKind.CategoryChanged, actorId, previous?.Value.ToString(), category.Id.Value.ToString(), nowUtc);
        Raise(new TicketCategoryChanged(Id, previous, category.Id, nowUtc));
    }

    private void ChangeStatus(TicketAction action, TicketActivityKind kind, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(action);

        TicketStatus previous = Status;
        Status = StatusTransitions[action].To;
        Record(kind, actorId, previous.ToString(), Status.ToString(), nowUtc);
        Raise(new TicketStatusChanged(Id, previous, Status, nowUtc));
    }

    private void EnsureAllowed(TicketAction action)
    {
        if (!IsAllowedInCurrentStatus(action))
        {
            throw new DomainException(TicketDomainErrors.TransitionNotAllowed(action, Status));
        }
    }

    private void EnsureCategoryFits(Category category)
    {
        if (!category.IsActive)
        {
            throw new DomainException(TicketDomainErrors.CategoryInactive);
        }

        if (!category.TenantId.Equals(TenantId) || !category.DepartmentId.Equals(DepartmentId))
        {
            throw new DomainException(TicketDomainErrors.CategoryDepartmentMismatch);
        }
    }

    private void Record(TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset nowUtc)
    {
        _activities.Add(new TicketActivity(Guid.NewGuid(), kind, actorId, from, to, nowUtc));
        Revision++;
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new DomainException(TicketDomainErrors.ActorRequired);
        }
    }

    private static string NormaliseSubject(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        string trimmed = subject.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(TicketDomainErrors.SubjectRequired);
        }

        return trimmed.Length > SubjectMaxLength ? throw new DomainException(TicketDomainErrors.SubjectTooLong) : trimmed;
    }

    private static string NormaliseDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        string trimmed = description.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(TicketDomainErrors.DescriptionRequired);
        }

        return trimmed.Length > DescriptionMaxLength ? throw new DomainException(TicketDomainErrors.DescriptionTooLong) : trimmed;
    }

    private static TicketChannel EnsureDefined(TicketChannel channel) =>
        Enum.IsDefined(channel) ? channel : throw new DomainException(TicketDomainErrors.ChannelUnsupported);

    private static TicketPriority EnsureDefined(TicketPriority priority) =>
        Enum.IsDefined(priority) ? priority : throw new DomainException(TicketDomainErrors.PriorityUnsupported);
}
