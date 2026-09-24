namespace Vispl.HRManagement.MobileApp.Services.Ui;

/// <summary>
/// Content transcribed from docs/screen-references/. Used only when sample mode
/// is switched on, so the design can be reviewed before the API exists. The
/// shell shows a banner whenever any of this is on screen.
/// </summary>
internal static class SampleContent
{
    private static readonly DateOnly InterviewDate = new(2024, 12, 29);
    private static readonly TimeOnly InterviewTime = new(10, 30);

    // Evaluated on access: Interviews is declared below and would still be null
    // during static field initialisation.
    public static DashboardModel Dashboard => new(
        [
            // REF-01 labels the first and third tile "Total Jobs". Transcribed
            // as shown; confirm whether the third should read Total Applicants.
            new KpiModel("Total Jobs", "3", "clipboard", "blue"),
            new KpiModel("Active Jobs", "7", "file-check", "cyan"),
            new KpiModel("Total Jobs", "10", "layers", "amber")
        ],
        [Interviews[0], Interviews[1]],
        [
            new AnalyticsRowModel("Store Manager", 1, 5),
            new AnalyticsRowModel("Financial Analyst", 3, 5),
            new AnalyticsRowModel("Sales Executive", 2, 5),
            new AnalyticsRowModel("HR Coordinator", 4, 5)
        ]);

    public static IReadOnlyList<InterviewModel> Interviews { get; } =
    [
        new("iv-1", "Rajesh Kumar", "Store Manager", InterviewDate, InterviewTime, "Round 1 - Screening", "Past Interview"),
        new("iv-2", "Amit Patel", "Sales Executive", InterviewDate, InterviewTime, "Round 2 - Technical", "Past Interview"),
        new("iv-3", "Udeep Singh", "Sales Executive", InterviewDate, InterviewTime, "Round 2 - Technical", "Past Interview"),
        new("iv-4", "Neha Singh", "HR Coordinator - HR Department", InterviewDate, InterviewTime, "Round 1 - Screening", "Past Interview"),
        new("iv-5", "Priya Gupta", "Store Engineer - Technology", InterviewDate, InterviewTime, "Round 2 - Technical", "Past Interview")
    ];

    public static IReadOnlyList<JobCardModel> Jobs { get; } =
    [
        new(1, "Storage Manager", "Retail", "Chandigarh, Mohali", new DateOnly(2026, 2, 15),
            "Full-time", "5-8 years", "₹8,00,000 - ₹11,00,000 p.a.", JobState.Draft, new DateOnly(2026, 2, 5)),
        new(2, "Financial Analyst", "Finance & Operations", "Mohali", new DateOnly(2026, 2, 15),
            "Full-time", "3-6 years", "₹6,00,000 - ₹8,50,000 p.a.", JobState.Active, new DateOnly(2026, 2, 5)),
        new(3, "Human Resources Executive", "HR & Administration", "Mohali, Chandigarh", new DateOnly(2026, 2, 15),
            "Full-time", "2-5 years", "₹4,50,000 - ₹6,50,000 p.a.", JobState.Inactive, new DateOnly(2026, 2, 5)),
        new(4, "E-Commerce Manager", "E-Commerce", "Mohali", new DateOnly(2026, 2, 15),
            "Full-time", "4-7 years", "₹7,00,000 - ₹10,00,000 p.a.", JobState.Expired, new DateOnly(2026, 2, 5))
    ];

    public static IReadOnlyList<JobDetailModel> JobDetails { get; } =
    [
        new(3, "Human Resources Executive", "HR & Administration", new DateOnly(2024, 12, 12), JobState.Inactive,
            "Chandigarh", "Full-time", "3-6 years", "₹6,50,000 - ₹9,50,000 p.a.",
            "Develop and maintain web applications for our e-commerce and retail platforms. Work with modern technologies and contribute to innovative solutions.",
            [new BulletGroup("Web application development",
                ["Database design", "API integration", "Code testing", "Documentation"])],
            [new BulletGroup("Bachelor's in IT/Computer Science",
                ["3+ years development", "Java/Python proficiency", "Database knowledge"])])
    ];

    public static IReadOnlyList<ApplicantModel> Applicants { get; } =
    [
        new("ap-1", "Rajesh Kumar", "Store Manager - Operations", "rajesh@email.com", "9876543210", 4.5m, ApplicantState.Shortlisted),
        new("ap-2", "Priya Gupta", "Store Engineer - Technology", "priya@email.com", "9876543210", 4.0m, ApplicantState.PendingReview),
        new("ap-3", "Neha Singh", "HR Coordinator - HR", "neha@email.com", "9876543210", 2.2m, ApplicantState.Rejected),
        new("ap-4", "Amit Patel", "Sales Executive - Retail", "amit@email.com", "9876543210", 3.8m, ApplicantState.Shortlisted)
    ];

    public static IReadOnlyList<ApplicationDetailModel> Applications { get; } =
    [
        new("ap-1", "Rajesh Kumar", "Store Manager", new DateOnly(2024, 12, 19), "Past Interview",
            [
                ("Email", "rajesh@email.com"),
                ("Phone", "9876543211"),
                ("Gender", "Male"),
                ("Marital Status", "Single"),
                ("Father/Husband Name", "Rajender Singh")
            ],
            [
                ("Experience", "3-5 years"),
                ("Education", "Bachelor of Business Administration"),
                ("Languages Known", "English, Hindi")
            ],
            new AddressModel("789 Finance Square, Corporate Hub", "Mohali", "Punjab", "160055"))
    ];

    public static IReadOnlyList<NotificationModel> Notifications { get; } =
    [
        new("nt-1", "Interview scheduled", new TimeOnly(15, 25), new DateOnly(2025, 9, 8),
            "Rajesh Kumar has been scheduled for Round 1 - Screening on 29 December at 10:30."),
        new("nt-2", "New application received", new TimeOnly(15, 25), new DateOnly(2025, 9, 8),
            "Priya Gupta applied for Store Engineer - Technology. The application is awaiting review."),
        new("nt-3", "Job opening expiring", new TimeOnly(15, 25), new DateOnly(2025, 9, 8),
            "E-Commerce Manager closes on 15 February 2026. Extend the posting or let it expire."),
        new("nt-4", "Offer accepted", new TimeOnly(15, 25), new DateOnly(2025, 9, 8),
            "Amit Patel accepted the Sales Executive offer. Onboarding can now be started.")
    ];
}
