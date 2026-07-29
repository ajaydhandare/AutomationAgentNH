namespace NewHorizon.Automation.ErpClient;

/// <summary>
/// The ERP application endpoints the agent calls. Collected here so the surface the agent depends
/// on can be reviewed with the ERP team in one place, and corrected in one place when the real
/// paths are confirmed.
/// </summary>
/// <remarks>
/// OPEN QUESTION (design doc §18.3): whether these create endpoints accept an idempotency key.
/// Until that is confirmed the client sends one as a header AND performs query-before-create, so
/// duplicate-safety does not depend on the answer.
/// </remarks>
internal static class ErpEndpoints
{
    public const string DeAllocation = "/api/automation/deallocation";
    public const string Allocation = "/api/automation/allocation";
    public const string WorkOrder = "/api/automation/workorder";
    public const string PurchaseRequisition = "/api/automation/purchase-requisition";
    public const string LaborRequisition = "/api/automation/labor-requisition";
    public const string OafLink = "/api/automation/oaf-link";

    public const string ExistingDocument = "/api/automation/existing-document";

    /// <summary>
    /// Confirms a transition the ERP automates internally (SO → OAF, SJO → CBOM). The agent calls
    /// this instead of creating, so it never duplicates the ERP's own flag-driven automation.
    /// </summary>
    public const string VerifyAutomation = "/api/automation/verify-automation";

    public const string NetShortage = "/api/automation/net-shortage";
    public const string MilShortage = "/api/automation/mil-shortage";
    public const string AllocationStatus = "/api/automation/allocation-status";
    public const string PendingDocuments = "/api/automation/pending-documents";

    // ---- AutoShop cycle ----------------------------------------------------
    // Confirmed with the client 2026-07-27. Request/response shapes still to be supplied.

    /// <summary>Site ID collection. The ERP team is modifying this response.</summary>
    public const string SiteList = "/api/v1/admin/location/list";

    /// <summary>
    /// One site's SJOs (BOM already created). GET reads them, POST submits the sorted sequence —
    /// the same path serves both, which is the ERP's existing convention here.
    /// </summary>
    public static string SjoSequence(string siteId) =>
        $"/api/v1/planning/autoshopsjosequence/GetSJODetails/{Uri.EscapeDataString(siteId)}/S";

    /// <summary>The cycle's entry point: OAFs still awaiting an SJO. Endpoint to be confirmed.</summary>
    public const string OafAwaitingSjo = "/api/v1/planning/oaf/pending-sjo";

    /// <summary>AutoShop read/submit for one site. Endpoints still upcoming from the ERP team.</summary>
    public static string AutoShop(string siteId) =>
        $"/api/v1/planning/autoshop/{Uri.EscapeDataString(siteId)}";

    /// <summary>OAF → SJO creation. Endpoint to be confirmed.</summary>
    public const string CreateSjoFromOaf = "/api/v1/planning/sjo/create-from-oaf";
}
