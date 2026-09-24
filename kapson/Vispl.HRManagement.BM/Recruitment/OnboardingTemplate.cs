namespace Vispl.HRManagement.BM.Recruitment;

/// <summary>
/// The default checklist seeded when an offer is accepted. Matches section 4 of
/// docs/hiring-pipeline.md. A per-tenant template would replace this later; the
/// shape stays the same.
/// </summary>
internal sealed record TaskTemplate(
    string Code,
    string Title,
    string Instructions,
    string TaskType,
    string OwnerKind,
    bool IsRequired);

internal static class OnboardingTemplate
{
    public const string Candidate = "Candidate";
    public const string Hr = "Hr";
    public const string Document = "Document";
    public const string Form = "Form";

    public static IReadOnlyList<TaskTemplate> Default { get; } =
    [
        new("PERSONAL_DETAILS", "Personal details",
            "Confirm your legal name, date of birth, gender and marital status.", Form, Candidate, true),
        new("ADDRESS_EMERGENCY", "Address and emergency contact",
            "Provide your current address and one emergency contact.", Form, Candidate, true),
        new("BANK_ACCOUNT", "Bank account for salary",
            "Provide the account that salary should be paid into.", Form, Candidate, true),
        new("PAN", "PAN card",
            "Upload a clear scan or photograph of your PAN card.", Document, Candidate, true),
        new("AADHAAR", "Aadhaar",
            "Upload a clear scan or photograph of your Aadhaar.", Document, Candidate, true),
        new("EDUCATION_CERTIFICATES", "Educational certificates",
            "Upload your highest qualification certificate.", Document, Candidate, true),
        new("EXPERIENCE_LETTERS", "Previous experience letters",
            "Upload relieving or experience letters from previous employers.", Document, Candidate, false),
        new("SIGNED_OFFER", "Signed offer letter",
            "Upload the offer letter with your signature.", Document, Candidate, true),
        new("BACKGROUND_VERIFICATION", "Background verification",
            "Record the outcome of the background check.", Form, Hr, true),
        new("ASSET_ALLOCATION", "Asset allocation",
            "Record the equipment issued to the new joiner.", Form, Hr, false)
    ];
}
