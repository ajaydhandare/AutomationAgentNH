using FluentAssertions;
using NewHorizon.Automation.Application.Erp;

namespace NewHorizon.Automation.IntegrationTests.Erp;

/// <summary>
/// Covers the rule the whole retry design rests on: transient conditions are retried, business
/// refusals are not.
/// </summary>
public sealed class ErpResilienceTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_server_error_is_retried_and_can_succeed()
    {
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("allocation", 503, 503, 200);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var result = await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        result.ErpDocumentRef.Should().Be("WO-OK");
        // Two failures plus the success: the caller never knew the ERP hiccuped.
        erp.RequestCountFor("allocation").Should().Be(3);
    }

    [Fact]
    public async Task A_business_refusal_is_never_retried()
    {
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("purchase-requisition", 400);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var call = async () => await harness.Client.CreatePurchaseRequisitionAsync(
            new PurchaseRequisitionRequest(Context(), SourceDocumentRef: null),
            CancellationToken.None);

        var exception = await call.Should().ThrowAsync<ErpBusinessException>();

        // The ERP's own wording reaches the planner; retrying would produce the same refusal and
        // only delay the human who has to fix it.
        exception.Which.LaymanMessage.Should().Be("Vendor missing for item X");
        erp.RequestCountFor("purchase-requisition").Should().Be(1);
    }

    [Fact]
    public async Task Retries_are_bounded_by_the_configured_maximum()
    {
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("allocation", 500, 500, 500, 500, 500, 500, 500, 500);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start), maxRetry: 2);

        var call = async () =>
            await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        (await call.Should().ThrowAsync<ErpTransientException>())
            .Which.LaymanMessage.Should().Contain("temporarily unavailable");

        // One initial attempt plus exactly MaxRetry retries — a stuck ERP must not be hammered.
        erp.RequestCountFor("allocation").Should().Be(3);
    }

    [Fact]
    public async Task A_rate_limit_response_is_treated_as_transient()
    {
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("allocation", 429, 200);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var result = await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        result.ErpDocumentRef.Should().Be("WO-OK");
        erp.RequestCountFor("allocation").Should().Be(2);
    }

    [Fact]
    public async Task An_unreachable_erp_surfaces_as_transient_not_business()
    {
        // Port 1 is closed; nothing is listening. This must read as "retry later", never as a
        // business refusal that would send a healthy document to human review.
        //
        // The failure lands on the sign-in call rather than the operation, so the assertion is on
        // the classification contract, not the concrete type: what the engine acts on is
        // IsTransient, and every ERP failure must answer it correctly regardless of where it arose.
        using var harness = new ErpClientHarness("http://127.0.0.1:1", new MutableClock(Start), maxRetry: 0);

        var call = async () =>
            await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        (await call.Should().ThrowAsync<ErpException>())
            .Which.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task A_business_refusal_declares_itself_not_transient()
    {
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("allocation", 400);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var call = async () =>
            await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        (await call.Should().ThrowAsync<ErpException>())
            .Which.IsTransient.Should().BeFalse();
    }

    [Fact]
    public async Task Retry_can_be_switched_off_entirely()
    {
        // MaxRetry = 0 is a legitimate configuration; it must disable retrying rather than crash
        // the pipeline at startup.
        await using var erp = await StubErpServer.StartAsync();
        erp.ScriptStatuses("allocation", 503, 200);

        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start), maxRetry: 0);

        var call = async () =>
            await harness.Client.AllocateAsync(new AllocationRequest(Context()), CancellationToken.None);

        await call.Should().ThrowAsync<ErpTransientException>();
        erp.RequestCountFor("allocation").Should().Be(1);
    }

    [Fact]
    public async Task Query_before_create_reports_what_the_erp_already_has()
    {
        await using var erp = await StubErpServer.StartAsync();
        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var existing = await harness.Client.FindExistingDocumentAsync(
            Context(),
            "workorder",
            CancellationToken.None);

        existing.Exists.Should().BeFalse();
        existing.ErpDocumentRef.Should().BeNull();
    }

    [Fact]
    public async Task Shortage_and_allocation_queries_deserialise()
    {
        await using var erp = await StubErpServer.StartAsync();
        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var shortage = await harness.Client.GetNetShortageAsync(Context(), CancellationToken.None);
        var allocation = await harness.Client.GetAllocationStatusAsync(Context(), CancellationToken.None);
        var mil = await harness.Client.GetMilShortageAsync(new MilShortageRequest(Context()), CancellationToken.None);

        shortage.NetShortage.Should().Be(5m);
        shortage.HasShortage.Should().BeTrue();
        allocation.ChildrenAllocated.Should().BeTrue();
        mil.HasShortage.Should().BeFalse();
    }

    [Fact]
    public async Task The_reconciliation_query_returns_documents_to_enqueue()
    {
        await using var erp = await StubErpServer.StartAsync();
        using var harness = new ErpClientHarness(erp.BaseUrl, new MutableClock(Start));

        var pending = await harness.Client.GetPendingDocumentsAsync(
            Start.AddHours(-1),
            CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].DocumentId.Should().Be("SO-STUB-1");
        pending[0].WorkflowType.Should().Be("SJO");
    }

    private static ErpOperationRequest Context() =>
        new("SalesOrder", "SO-1", "corr-1", Guid.Parse("11111111-1111-1111-1111-111111111111"));
}
