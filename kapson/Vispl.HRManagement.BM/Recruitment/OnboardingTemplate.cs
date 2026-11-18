namespace Vispl.HRManagement.BM.Recruitment;

/// <summary>
/// The default checklist seeded when an offer is accepted. Matches section 4 of
/// docs/hiring-pipeline.md and collects structured 8-section Employee Master data
/// (Personal/Family/Statutory, Education, Previous Employment, Address/Emergency,
/// Bank/PF/ESI, Documents, and HR Placement/Verification) for seamless provisioning.
/// </summary>
public sealed record TaskTemplate(
    string Code,
    string Title,
    string Instructions,
    string TaskType,
    string OwnerKind,
    bool IsRequired);

public static class OnboardingTemplate
{
    public const string Candidate = "Candidate";
    public const string Hr = "Hr";
    public const string Document = "Document";
    public const string Form = "Form";

    public static IReadOnlyList<TaskTemplate> Default { get; } =
    [
        new("PERSONAL_DETAILS", "Personal, family & statutory details",
            "Confirm your legal name, date of birth, gender, marital status, blood group, religion, nationality, family details, PAN and Aadhaar.",
            Form, Candidate, true),
        new("ADDRESS_EMERGENCY", "Address and emergency contact",
            "Provide your permanent and current residential address (city, state, country, pincode) and one emergency contact.",
            Form, Candidate, true),
        new("BANK_ACCOUNT", "Bank account & PF / ESI details",
            "Provide your salary bank account (holder name, bank, account number, IFSC, branch, account type) and existing PF / UAN / ESI details if applicable.",
            Form, Candidate, true),
        new("EDUCATION_HISTORY", "Education & qualifications",
            "Provide your highest academic qualification, specialisation, institute, board/university, passing year and CGPA or percentage.",
            Form, Candidate, false),
        new("PREVIOUS_EMPLOYMENT", "Previous company experience",
            "If you have prior work experience, provide your previous company, designation, tenure, last annual CTC and leaving reason.",
            Form, Candidate, false),
        new("PAN", "PAN card",
            "Upload a clear scan or photograph of your PAN card.",
            Document, Candidate, true),
        new("AADHAAR", "Aadhaar",
            "Upload a clear scan or photograph of your Aadhaar card.",
            Document, Candidate, true),
        new("EDUCATION_CERTIFICATES", "Educational certificates",
            "Upload your highest qualification certificate and mark sheet.",
            Document, Candidate, true),
        new("EXPERIENCE_LETTERS", "Previous experience letters",
            "Upload relieving or experience letters from previous employers.",
            Document, Candidate, false),
        new("SIGNED_OFFER", "Signed offer letter",
            "Upload the offer letter with your signature.",
            Document, Candidate, true),
        new("BACKGROUND_VERIFICATION", "Background verification & placement",
            "Record background verification outcome and confirm division, sub-department, designation and reporting manager.",
            Form, Hr, true),
        new("ASSET_ALLOCATION", "Asset allocation",
            "Record laptop, ID badge or store equipment issued to the new joiner.",
            Form, Hr, false)
    ];
}
