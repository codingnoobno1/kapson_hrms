using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Features.Manage;

/// <summary>
/// The Manage tab visible to HR users (RECRUITMENT.VIEW). Three sub-tabs: Jobs, Applicants,
/// Shortlisted — matching the locked reference mockup exactly.
/// </summary>
public sealed class ManagePage : ContentPage
{
    private static readonly Color HrBlue = Color.FromArgb("#1565C0");
    private static readonly Color HrBlueTint = Color.FromArgb("#E3F2FD");
    private static readonly Color BadgeDraft = Color.FromArgb("#F6A549");
    private static readonly Color BadgeActive = Color.FromArgb("#1565C0");
    private static readonly Color BadgeInactive = Color.FromArgb("#9E9E9E");
    private static readonly Color BadgeExpired = Color.FromArgb("#F44336");

    private readonly RecruitmentApiClient recruitment;
    private readonly MobileSession session;
    private readonly QuickActionsHost quickActions;
    private readonly AppSheet sheet = new();
    private readonly BrandAppBar appBar = new();

    // Sub-tab state
    private int activeSubTab;
    private readonly Border[] subTabBorders = new Border[3];
    private readonly Label[] subTabLabels = new Label[3];
    private readonly VerticalStackLayout content = new() { Spacing = 0 };

    // Filter state
    private string? activeJobStageFilter;

    public ManagePage(
        RecruitmentApiClient recruitment,
        MobileSession session,
        QuickActionsHost quickActions)
    {
        this.recruitment = recruitment;
        this.session = session;
        this.quickActions = quickActions;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Manage";

        var tabBar = new EmployeeTabBar(EmployeeTab.Manage, session);
        tabBar.QuickActionsRequested += (_, _) => quickActions.Present(sheet);

        var subTabRow = BuildSubTabRow();
        var scrollView = new ScrollView { Content = content };

        sheet.Content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ],
            Children =
            {
                Row(appBar, 0),
                Row(subTabRow, 1),
                Row(scrollView, 2),
                Row(tabBar, 3)
            }
        };

        Content = sheet;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadAsync();
    }

    private View BuildSubTabRow()
    {
        var grid = new Grid
        {
            ColumnSpacing = 0,
            BackgroundColor = Tokens.Surface,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ]
        };

        string[] labels = ["Jobs", "Applicants", "Shortlisted"];

        for (var i = 0; i < 3; i++)
        {
            var idx = i;
            var label = new Label
            {
                Text = labels[i],
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12)
            };

            var indicator = new BoxView
            {
                HeightRequest = 3,
                VerticalOptions = LayoutOptions.End
            };

            var tab = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                Content = new Grid
                {
                    RowDefinitions =
                    [
                        new RowDefinition(GridLength.Star),
                        new RowDefinition(3)
                    ],
                    Children = { Row(label, 0), Row(indicator, 1) }
                }
            };

            subTabBorders[i] = tab;
            subTabLabels[i] = label;

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await SwitchSubTabAsync(idx);
            tab.GestureRecognizers.Add(tap);

            grid.Add(tab, i);
        }

        ApplySubTabStyles();

        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Tokens.Surface,
            Padding = new Thickness(Tokens.SpaceGutter, 0),
            Content = grid,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.06f, Radius = 4, Offset = new Point(0, 2) }
        };
    }

    private void ApplySubTabStyles()
    {
        for (var i = 0; i < subTabBorders.Length; i++)
        {
            var isActive = i == activeSubTab;
            subTabLabels[i].TextColor = isActive ? HrBlue : Tokens.TextSecondary;

            // The indicator is the second child of the inner Grid
            if (subTabBorders[i].Content is Grid g && g.Children.Count >= 2 && g.Children[1] is BoxView box)
            {
                box.Color = isActive ? HrBlue : Colors.Transparent;
            }
        }
    }

    private async Task SwitchSubTabAsync(int idx)
    {
        activeSubTab = idx;
        ApplySubTabStyles();
        activeJobStageFilter = null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        content.Children.Clear();

        var loading = new ActivityIndicator
        {
            IsRunning = true,
            Color = HrBlue,
            Margin = new Thickness(Tokens.SpaceGutter, 40),
            HorizontalOptions = LayoutOptions.Center
        };
        content.Children.Add(loading);

        try
        {
            switch (activeSubTab)
            {
                case 0: await LoadJobsAsync(); break;
                case 1: await LoadApplicantsAsync(stage: null); break;
                case 2: await LoadApplicantsAsync(stage: "Shortlisted"); break;
            }
        }
        catch (Exception ex)
        {
            content.Children.Clear();
            content.Children.Add(new Label
            {
                Text = ex.Message,
                FontSize = 13,
                TextColor = Tokens.TextSecondary,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(24, 48)
            });
        }
    }

    // ---- Jobs tab ----------------------------------------------------------

    private async Task LoadJobsAsync()
    {
        var page = await recruitment.GetJobsAsync(status: activeJobStageFilter);
        content.Children.Clear();

        if (page.Items.Count == 0)
        {
            content.Children.Add(EmptyState("No job openings found.", "Create a posting from the HR portal."));
            return;
        }

        // Status filter row
        content.Children.Add(BuildJobFilterRow());

        foreach (var job in page.Items)
        {
            content.Children.Add(JobCard(job));
        }

        content.Children.Add(new BoxView { HeightRequest = 24 });
    }

    private View BuildJobFilterRow()
    {
        var filters = new[] { ("All", (string?)null), ("Draft", "Draft"), ("Active", "Active"), ("Inactive", "Inactive"), ("Expired", "Expired") };

        var stack = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(Tokens.SpaceGutter, 12)
        };

        foreach (var (label, value) in filters)
        {
            var chipLabel = label;
            var chipValue = value;
            var isActive = activeJobStageFilter == chipValue;

            var chip = new Border
            {
                BackgroundColor = isActive ? HrBlue : Tokens.SurfaceMuted,
                StrokeThickness = 0,
                Padding = new Thickness(14, 7),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Content = new Label
                {
                    Text = chipLabel,
                    FontSize = 12,
                    FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isActive ? Colors.White : Tokens.TextSecondary
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                activeJobStageFilter = chipValue;
                await LoadJobsAsync();
            };
            chip.GestureRecognizers.Add(tap);
            stack.Children.Add(chip);
        }

        return new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
    }

    private View JobCard(JobListItemDto job)
    {
        var statusColor = job.Status switch
        {
            "Active" => BadgeActive,
            "Inactive" => BadgeInactive,
            "Expired" => BadgeExpired,
            _ => BadgeDraft
        };

        var statusBadge = new Border
        {
            BackgroundColor = statusColor,
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = job.Status, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
        };

        var titleRow = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 8
        };
        titleRow.Add(new Label { Text = job.Title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, VerticalOptions = LayoutOptions.Center }, 0);
        titleRow.Add(statusBadge, 1);

        var expiryText = job.ExpiresOn.HasValue
            ? $"Expiry Date: {job.ExpiresOn.Value:dd-MM-yyyy}"
            : string.Empty;

        var meta1 = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Margin = new Thickness(0, 2)
        };
        meta1.Add(new Label { Text = $"{job.Department} • {job.Location}", FontSize = 12, TextColor = Tokens.TextSecondary }, 0);
        meta1.Add(new Label { Text = expiryText, FontSize = 11, TextColor = Tokens.TextTertiary, HorizontalTextAlignment = TextAlignment.End }, 1);

        var typeIcons = $"🧳 {job.EmploymentType}  📅 {job.Experience}";
        var salaryLine = string.IsNullOrWhiteSpace(job.SalaryRange) ? typeIcons : $"{typeIcons}  {job.SalaryRange}";

        var meta2 = new Label { Text = salaryLine, FontSize = 12, TextColor = Tokens.TextSecondary, Margin = new Thickness(0, 2) };

        var lastUpdated = new Label
        {
            Text = job.LastStatusUpdatedOn.HasValue
                ? $"Last Status Updated: {job.LastStatusUpdatedOn.Value:dd-MM-yyyy}"
                : string.Empty,
            FontSize = 11,
            TextColor = Tokens.TextTertiary,
            HorizontalTextAlignment = TextAlignment.End,
            Margin = new Thickness(0, 4)
        };

        var actionRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 8,
            Margin = new Thickness(0, 6, 0, 0)
        };

        actionRow.Add(IconButton("👁", () => ShowJobDetailAsync(job.Id)), 0);
        actionRow.Add(IconButton("✏️", () => ShowJobDetailAsync(job.Id)), 1);
        actionRow.Add(lastUpdated, 3);

        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(14, 12),
            Children = { titleRow, meta1, meta2, actionRow }
        };

        return new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Margin = new Thickness(Tokens.SpaceGutter, 6),
            Content = stack
        };
    }

    private static View IconButton(string emoji, Func<Task> onTap)
    {
        var border = new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            WidthRequest = 36,
            HeightRequest = 36,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new Label
            {
                Text = emoji,
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private async Task ShowJobDetailAsync(long jobId)
    {
        sheet.Present("Job Details", new ActivityIndicator { IsRunning = true, Color = HrBlue, Margin = 32 });

        try
        {
            var job = await recruitment.GetJobAsync(jobId);
            sheet.Present("Job Details", BuildJobDetailContent(job));
        }
        catch (Exception ex)
        {
            sheet.Present("Job Details", SheetContent.FieldCard(("Error", ex.Message)));
        }
    }

    private static View BuildJobDetailContent(JobDetailDto job)
    {
        var statusColor = job.Status switch
        {
            "Active" => BadgeActive,
            "Inactive" => BadgeInactive,
            "Expired" => BadgeExpired,
            _ => BadgeDraft
        };

        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        // Header card
        var headerBadge = new Border
        {
            BackgroundColor = statusColor,
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = job.Status, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Padding = new Thickness(14, 12)
        };
        headerGrid.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = job.Title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                new Label { Text = job.Department, FontSize = 13, TextColor = Tokens.TextSecondary }
            }
        }, 0);
        headerGrid.Add(headerBadge, 1);

        body.Add(new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 12),
            Content = headerGrid
        });

        // Job Summary
        body.Add(SheetContent.SectionHeader("Job Summary"));
        body.Add(SheetContent.FieldCard(
            ("Location", job.Location),
            ("Job Type", job.EmploymentType),
            ("Experience", job.Experience),
            ("Salary", job.SalaryRange ?? "Not specified")));

        // Job Description
        if (!string.IsNullOrWhiteSpace(job.Summary))
        {
            body.Add(SheetContent.SectionHeader("Job Description"));
            body.Add(new Label
            {
                Text = job.Summary,
                FontSize = 13,
                TextColor = Tokens.TextSecondary,
                Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 12)
            });
        }

        // Responsibilities
        if (job.Responsibilities.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Key Responsibilities"));
            body.Add(BulletList(job.Responsibilities));
        }

        // Requirements
        if (job.Requirements.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Required Qualifications"));
            body.Add(BulletList(job.Requirements));
        }

        // Posted info
        body.Add(new Label
        {
            Text = $"📅 Posted: {(job.PublishedUtc?.ToLocalTime() ?? job.UpdatedUtc.ToLocalTime()):yyyy-MM-dd}",
            FontSize = 12,
            TextColor = Tokens.TextTertiary,
            Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 0)
        });

        return body;
    }

    private static View BulletList(IReadOnlyList<string> items)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 12)
        };

        foreach (var item in items)
        {
            stack.Children.Add(new Label
            {
                Text = $"− {item}",
                FontSize = 13,
                TextColor = Tokens.TextSecondary
            });
        }

        return stack;
    }

    // ---- Applicants tab ----------------------------------------------------

    private async Task LoadApplicantsAsync(string? stage)
    {
        var page = await recruitment.GetApplicationsAsync(stage: stage);
        content.Children.Clear();

        if (page.Items.Count == 0)
        {
            content.Children.Add(EmptyState("No applicants found.", stage is null ? "Applications will appear here." : $"No {stage.ToLowerInvariant()} applications yet."));
            return;
        }

        if (stage is null)
        {
            content.Children.Add(BuildApplicantFilterRow());
        }

        foreach (var application in page.Items)
        {
            content.Children.Add(ApplicantCard(application));
        }

        content.Children.Add(new BoxView { HeightRequest = 24 });
    }

    private View BuildApplicantFilterRow()
    {
        var filterChips = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(Tokens.SpaceGutter, 12) };

        var stages = new[] { "All", "Shortlisted", "Rejected", "Pending" };
        foreach (var stage in stages)
        {
            var isAll = stage == "All";
            var chip = new Border
            {
                BackgroundColor = isAll ? HrBlue : Tokens.SurfaceMuted,
                StrokeThickness = 0,
                Padding = new Thickness(14, 7),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Content = new Label
                {
                    Text = stage,
                    FontSize = 12,
                    FontAttributes = isAll ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isAll ? Colors.White : Tokens.TextSecondary
                }
            };

            filterChips.Children.Add(chip);
        }

        return new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = filterChips, HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
    }

    private View ApplicantCard(ApplicationListItemDto application)
    {
        var tone = application.Stage.Tone;
        var dotColor = tone == "success" ? Tokens.StatusSuccess
            : tone == "danger" ? Tokens.StatusDanger
            : Tokens.StatusWarning;

        // Left accent dot
        var dot = new Border
        {
            WidthRequest = 10,
            HeightRequest = 10,
            BackgroundColor = dotColor,
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 5, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 5 }
        };

        // Stage badge
        var stageBadge = new Border
        {
            BackgroundColor = Tokens.ForTone(tone),
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new Label
            {
                Text = application.Stage.Name,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Tokens.OnTone(tone)
            }
        };

        var nameRow = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 8
        };
        nameRow.Add(new Label { Text = application.Candidate.DisplayName, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary }, 0);
        nameRow.Add(stageBadge, 1);

        var infoStack = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                nameRow,
                new Label { Text = application.JobTitle, FontSize = 12, TextColor = Tokens.TextSecondary },
                new Label { Text = $"📅 Applied: {application.AppliedUtc.ToLocalTime():dd-MM-yyyy}", FontSize = 11, TextColor = Tokens.TextTertiary }
            }
        };

        // Action buttons
        var canShortlist = application.Actions.Any(a => a.Action == "shortlist" && a.IsAllowed);
        var canReject = application.Actions.Any(a => a.Action == "reject" && a.IsAllowed);

        var buttonRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };

        var viewBtn = new Button
        {
            Text = "👁 View Details",
            FontSize = 12,
            BackgroundColor = HrBlue,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        viewBtn.Clicked += async (_, _) => await ShowApplicationDetailAsync(application);
        buttonRow.Children.Add(viewBtn);

        if (canShortlist)
        {
            var shortlistBtn = new Button
            {
                Text = "✓ Shortlisted",
                FontSize = 12,
                BackgroundColor = Tokens.DecisionApprove,
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 36,
                Padding = new Thickness(12, 0)
            };
            shortlistBtn.Clicked += async (_, _) => await DecideApplicationAsync(application.Id, "shortlist", "Shortlisted");
            buttonRow.Children.Add(shortlistBtn);
        }

        if (canReject)
        {
            var rejectBtn = new Button
            {
                Text = "✗ Reject",
                FontSize = 12,
                BackgroundColor = Tokens.DecisionDisapprove,
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 36,
                Padding = new Thickness(12, 0)
            };
            rejectBtn.Clicked += async (_, _) => await DecideApplicationAsync(application.Id, "reject", "Rejected");
            buttonRow.Children.Add(rejectBtn);
        }

        var bodyGrid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 10,
            Padding = new Thickness(14, 12)
        };
        bodyGrid.Add(dot, 0);

        var rightStack = new VerticalStackLayout { Spacing = 0, Children = { infoStack, buttonRow } };
        bodyGrid.Add(rightStack, 1);

        return new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Margin = new Thickness(Tokens.SpaceGutter, 6),
            Content = bodyGrid
        };
    }

    private async Task ShowApplicationDetailAsync(ApplicationListItemDto summary)
    {
        sheet.Present("Full Application Details", new ActivityIndicator { IsRunning = true, Color = HrBlue, Margin = 32 });

        try
        {
            var detail = await recruitment.GetApplicationAsync(long.Parse(summary.Id));
            var candidateId = summary.Candidate.Id;
            CandidateProfileDto? profile = null;

            try { profile = await recruitment.GetCandidateAsync(candidateId); }
            catch { /* PII endpoint might not be accessible */ }

            sheet.Present("Full Application Details", BuildApplicationDetailContent(summary, detail, profile));
        }
        catch (Exception ex)
        {
            sheet.Present("Full Application Details", SheetContent.FieldCard(("Error", ex.Message)));
        }
    }

    private static View BuildApplicationDetailContent(
        ApplicationListItemDto summary,
        ApplicationDetailDto detail,
        CandidateProfileDto? profile)
    {
        var tone = summary.Stage.Tone;
        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        // Header card
        var stageBadge = new Border
        {
            BackgroundColor = Tokens.ForTone(tone),
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = summary.Stage.Name, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Tokens.OnTone(tone) }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Padding = new Thickness(14, 12)
        };
        headerGrid.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = summary.Candidate.DisplayName, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                new Label { Text = summary.JobTitle, FontSize = 13, TextColor = Tokens.TextSecondary },
                new Label { Text = $"📅 Applied: {summary.AppliedUtc.ToLocalTime():yyyy-MM-dd}", FontSize = 12, TextColor = Tokens.TextTertiary }
            }
        }, 0);
        headerGrid.Add(stageBadge, 1);

        body.Add(new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 12),
            Content = headerGrid
        });

        // Personal Information (from profile if available)
        body.Add(SheetContent.SectionHeader("📋 Personal Information"));
        body.Add(SheetContent.FieldCard(
            ("Email", profile?.Email ?? "—"),
            ("Phone", profile?.Phone ?? "—")));

        // Professional Details
        body.Add(SheetContent.SectionHeader("🧳 Professional Details"));
        body.Add(SheetContent.FieldCard(
            ("Experience", profile?.TotalExperienceYears is { } exp ? $"{exp} years" : "—"),
            ("Skills", profile?.Skills.Count > 0 ? string.Join(", ", profile.Skills) : "—"),
            ("Source", detail.Source)));

        // Documents
        if (detail.Documents.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("📁 Documents"));

            foreach (var doc in detail.Documents)
            {
                body.Add(DocumentRow(doc.FileName, doc.ContentType ?? "file"));
            }
        }

        // Activity
        if (detail.Activity.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Activity"));

            foreach (var entry in detail.Activity.Take(5))
            {
                body.Add(new Label
                {
                    Text = $"• {entry.OccurredUtc.ToLocalTime():d MMM yyyy HH:mm}  {entry.Summary}",
                    FontSize = 12,
                    TextColor = Tokens.TextSecondary,
                    Margin = new Thickness(Tokens.SpaceGutter, 2)
                });
            }
        }

        return body;
    }

    private static View DocumentRow(string name, string contentType)
    {
        var icon = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "📄" : "📎";

        var row = new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Margin = new Thickness(Tokens.SpaceGutter, 4),
            Padding = new Thickness(14, 10),
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
                ColumnSpacing = 8,
                Children =
                {
                    Span(new Label { Text = $"{icon} {name}", FontSize = 13, TextColor = Tokens.TextPrimary, VerticalOptions = LayoutOptions.Center }, 0),
                    Span(DocActionButton("👁"), 1),
                    Span(DocActionButton("⬇"), 2)
                }
            }
        };

        return row;
    }

    private static View Span(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    private static View DocActionButton(string emoji)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#1565C0"),
            StrokeThickness = 0,
            WidthRequest = 32,
            HeightRequest = 32,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = new Label { Text = emoji, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
        };
    }

    private async Task DecideApplicationAsync(string applicationId, string decision, string label)
    {
        try
        {
            await recruitment.DecideApplicationAsync(
                long.Parse(applicationId),
                new Contracts.Mobile.Recruitment.ApplicationDecisionRequest
                {
                    Decision = decision,
                    Reason = label,
                    NotifyCandidate = true,
                    RowVersion = string.Empty
                });

            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ---- Helpers -----------------------------------------------------------

    private static View EmptyState(string title, string subtitle)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(Tokens.SpaceGutter, 48),
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "📂", FontSize = 40, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = subtitle, FontSize = 13, TextColor = Tokens.TextSecondary, HorizontalTextAlignment = TextAlignment.Center }
            }
        };
    }

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
