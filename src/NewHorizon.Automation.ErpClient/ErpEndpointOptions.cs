namespace NewHorizon.Automation.ErpClient;

/// <summary>
/// The ERP application paths the agent calls, relative to <c>AutomationAgent:ErpApi:BaseUrl</c>.
/// </summary>
/// <remarks>
/// <para>
/// Collected in one place so the surface the agent depends on can be reviewed with the ERP team at
/// a glance — and bound from <c>AutomationAgent:ErpEndpoints</c> so a path can be corrected on the
/// server without a rebuild. That matters more than usual here: only the three AutoShop paths the
/// client confirmed on 2026-07-27 are known to be right. The rest are placeholders.
/// </para>
/// <para>
/// One base URL serves all of them. There is no separate host for AutoShop.
/// </para>
/// </remarks>
public sealed class ErpEndpointOptions
{
    /// <summary>Placeholder replaced with the Site ID in the per-site templates.</summary>
    public const string SiteIdToken = "{siteId}";

    public string DeAllocation { get; init; } = "/api/automation/deallocation";

    public string Allocation { get; init; } = "/api/automation/allocation";

    public string WorkOrder { get; init; } = "/api/automation/workorder";

    public string PurchaseRequisition { get; init; } = "/api/automation/purchase-requisition";

    public string LaborRequisition { get; init; } = "/api/automation/labor-requisition";

    public string OafLink { get; init; } = "/api/automation/oaf-link";

    /// <summary>Query-before-create, so a resumed job adopts what an earlier attempt made.</summary>
    public string ExistingDocument { get; init; } = "/api/automation/existing-document";

    /// <summary>
    /// Confirms a transition the ERP automates internally (SO → OAF, SJO → CBOM). The agent asks
    /// rather than acts, so it can never duplicate the ERP's own flag-driven automation.
    /// </summary>
    public string VerifyAutomation { get; init; } = "/api/automation/verify-automation";

    public string NetShortage { get; init; } = "/api/automation/net-shortage";

    public string MilShortage { get; init; } = "/api/automation/mil-shortage";

    public string AllocationStatus { get; init; } = "/api/automation/allocation-status";

    public string PendingDocuments { get; init; } = "/api/automation/pending-documents";

    // ---- AutoShop cycle ----------------------------------------------------

    /// <summary>Site ID collection. Confirmed 2026-07-27; the ERP team is modifying its response.</summary>
    public string SiteList { get; init; } = "/api/v1/admin/location/list";

    /// <summary>
    /// One site's SJOs (BOM already created). The same path serves GET and POST — the ERP's
    /// existing convention here. Confirmed 2026-07-27.
    /// </summary>
    public string SjoSequenceTemplate { get; init; } =
        "/api/v1/planning/autoshopsjosequence/GetSJODetails/" + SiteIdToken + "/S";

    /// <summary>AutoShop read/submit for one site. Still upcoming from the ERP team.</summary>
    public string AutoShopTemplate { get; init; } = "/api/v1/planning/autoshop/" + SiteIdToken;

    /// <summary>The cycle's entry point: OAFs still awaiting an SJO. Path to be confirmed.</summary>
    public string OafAwaitingSjo { get; init; } = "/api/v1/planning/oaf/pending-sjo";

    /// <summary>OAF → SJO creation. Path to be confirmed.</summary>
    public string CreateSjoFromOaf { get; init; } = "/api/v1/planning/sjo/create-from-oaf";

    public string SjoSequence(string siteId) => Resolve(SjoSequenceTemplate, siteId);

    public string AutoShop(string siteId) => Resolve(AutoShopTemplate, siteId);

    private static string Resolve(string template, string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);

        return template.Replace(SiteIdToken, Uri.EscapeDataString(siteId), StringComparison.Ordinal);
    }
}
