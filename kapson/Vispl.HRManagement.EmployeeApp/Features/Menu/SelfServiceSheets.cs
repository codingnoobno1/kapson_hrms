using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp.Features.Menu;

/// <summary>
/// EMP-041 / 042 / 043. The Salary Details, Bank Details and Employee Onboarding sheet bodies
/// (employee-app-references 3.png and the menu rows of 13.png).
///
/// CR-9: these views are built from DTOs held in memory for as long as the sheet is open. Nothing
/// here writes salary or bank data to preferences, files or logs.
/// </summary>
public static class SelfServiceSheets
{
    // Indian digit grouping (e.g. ₹12,34,567) built explicitly. CultureInfo.GetCultureInfo("en-IN")
    // throws CultureNotFoundException on a runtime without ICU / predefined cultures, which was
    // crashing the Salary, Bank and Onboarding sheets the moment they were opened (the static
    // initialiser failed, surfacing as a TypeInitializationException). A NumberFormatInfo needs no
    // culture lookup and never throws.
    private static readonly NumberFormatInfo India = new()
    {
        NumberGroupSizes = [3, 2],
        NumberGroupSeparator = ",",
        NumberDecimalDigits = 0
    };

    public static View Salary(MySalaryDto salary)
    {
        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        if (!salary.IsConfigured)
        {
            body.Add(SheetContent.FieldCard(
                ("Salary structure", "HR has not set up a salary structure for your grade yet."),
                ("Grade", salary.GradeName ?? salary.GradeCode ?? "Not assigned")));
            body.Add(PaymentPlate(salary));
            return body;
        }

        // Green hero: the monthly take-home, with gross and the annual figure under it.
        body.Add(new Border
        {
            BackgroundColor = Tokens.MoneyPositive,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusTile },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            Padding = new Thickness(18, 16),
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = "Net monthly salary", FontSize = 12, TextColor = Colors.White },
                    new Label
                    {
                        Text = Rupees(salary.NetMonthly),
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    },
                    new Label
                    {
                        Text = $"Gross {Rupees(salary.GrossMonthly)} a month · {Rupees(salary.AnnualGross)} a year",
                        FontSize = 12,
                        TextColor = Colors.White
                    },
                    new Label
                    {
                        Text = $"Grade {salary.GradeName ?? salary.GradeCode}",
                        FontSize = 11,
                        TextColor = Colors.White,
                        Opacity = 0.9,
                        IsVisible = !string.IsNullOrWhiteSpace(salary.GradeName ?? salary.GradeCode)
                    }
                }
            }
        });

        body.Add(SheetContent.SectionHeader("Earnings"));
        var earnings = new List<(string, decimal, string?)> { ("Basic salary", salary.BasicMonthly, null) };
        earnings.AddRange(salary.Allowances.Select(line => (line.Name, line.Amount, line.Note)));
        body.Add(AmountPlate(earnings, ("Gross salary", salary.GrossMonthly), negative: false));

        if (salary.Deductions.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Deductions"));
            body.Add(AmountPlate(
                salary.Deductions.Select(line => (line.Name, line.Amount, line.Note)).ToList(),
                ("Total deductions", salary.TotalDeductionsMonthly),
                negative: true));
        }

        body.Add(SheetContent.SectionHeader("Payment details"));
        body.Add(PaymentPlate(salary));

        body.Add(Note("This is your grade's standard month. What is paid each month is prorated for attendance and approved by HR."));
        return body;
    }

    public static View Bank(MyBankAccountDto account, Func<Task>? requestChange = null)
    {
        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        if (!account.HasAccount)
        {
            body.Add(SheetContent.FieldCard(
                ("Salary account", "No salary bank account is on record. Your salary cannot be paid by bank transfer until HR adds one.")));
        }
        else
        {
            body.Add(SheetContent.FieldCard(
                ("Bank", account.BankName),
                ("Account holder", account.AccountHolderName),
                ("Account number", account.MaskedAccountNumber),
                ("IFSC", account.Ifsc),
                ("Account type", account.AccountType),
                ("Last updated", account.UpdatedUtc?.ToLocalTime().ToString("d MMM yyyy"))));

            body.Add(Note("For your security only the last four digits are shown."));
        }

        if (requestChange is not null)
        {
            var button = new Button
            {
                Text = account.HasAccount ? "Request change" : "Add bank account",
                BackgroundColor = Tokens.BrandAccent,
                TextColor = Tokens.OnAccent,
                CornerRadius = 10,
                FontSize = 13,
                HeightRequest = 42,
                Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 0)
            };

            button.Clicked += async (_, _) => await requestChange();
            body.Add(button);
        }

        return body;
    }

    public static View Onboarding(MyOnboardingDto plan)
    {
        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        if (!plan.HasOnboarding)
        {
            body.Add(SheetContent.FieldCard(
                ("Onboarding", "There is no onboarding checklist on your record. It is used for people who joined through recruitment.")));
            return body;
        }

        var percent = Math.Clamp(plan.CompletionPercent, 0, 100);

        body.Add(new Border
        {
            BackgroundColor = Tokens.PaymentTermsTint,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            Padding = new Thickness(16, 14),
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = plan.Stage ?? "In progress",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Tokens.TextPrimary
                    },
                    new Label
                    {
                        Text = string.Join(" · ", new[]
                        {
                            plan.JobTitle,
                            plan.JoiningOn is { } joining ? $"Joining {joining:d MMM yyyy}" : null
                        }.Where(part => !string.IsNullOrWhiteSpace(part))),
                        FontSize = 12,
                        TextColor = Tokens.TextSecondary
                    },
                    new ProgressBar
                    {
                        Progress = percent / 100d,
                        ProgressColor = Tokens.BrandAccent,
                        HeightRequest = 8,
                        Margin = new Thickness(0, 6, 0, 0)
                    },
                    new Label
                    {
                        Text = plan.CompletedUtc is { } completed
                            ? $"Completed on {completed.ToLocalTime():d MMM yyyy}"
                            : $"{percent}% complete · {plan.OutstandingRequiredCount} required task(s) outstanding",
                        FontSize = 12,
                        TextColor = Tokens.TextPrimary
                    }
                }
            }
        });

        if (plan.Tasks.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Tasks"));

            foreach (var task in plan.Tasks)
            {
                body.Add(TaskCard(task));
            }
        }

        return body;
    }

    // ---- Parts --------------------------------------------------------------

    private static View AmountPlate(IReadOnlyList<(string Name, decimal Amount, string? Note)> lines, (string Name, decimal Amount) total, bool negative)
    {
        var stack = new VerticalStackLayout { Spacing = 0 };

        foreach (var line in lines)
        {
            stack.Add(AmountRow(line.Name, line.Note, line.Amount, negative, bold: false));
            stack.Add(new BoxView { HeightRequest = 1, Color = Tokens.Border });
        }

        stack.Add(AmountRow(total.Name, null, total.Amount, negative, bold: true));

        return new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = stack
        };
    }

    private static View AmountRow(string name, string? note, decimal amount, bool negative, bool bold)
    {
        var title = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = name,
                    FontSize = 14,
                    FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = Tokens.TextPrimary
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(note))
        {
            title.Add(new Label { Text = note, FontSize = 11, TextColor = Tokens.TextSecondary });
        }

        var value = new Label
        {
            Text = (negative ? "− " : string.Empty) + Rupees(amount),
            FontSize = 14,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            TextColor = negative ? Tokens.StatusDanger : Tokens.TextPrimary,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.End
        };

        var grid = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 12,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };

        grid.Add(title, 0);
        grid.Add(value, 1);
        return grid;
    }

    /// <summary>The Payment Details plate: blush tint with a coral rule down the left (3.png).</summary>
    private static View PaymentPlate(MySalaryDto salary)
    {
        var rows = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                SheetContent.FieldRow("Payment frequency", salary.PaymentFrequency),
                SheetContent.FieldRow("Payment mode", salary.PaymentMode),
                SheetContent.FieldRow("Schedule", salary.PaymentSchedule)
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(4), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(new BoxView { Color = Tokens.BrandAccent }, 0);
        grid.Add(rows, 1);

        return new Border
        {
            BackgroundColor = Tokens.PaymentTermsTint,
            StrokeThickness = 0,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = grid
        };
    }

    private static View TaskCard(MyOnboardingTaskDto task)
    {
        var pill = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            BackgroundColor = Tokens.ForTone(task.Tone),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusPill },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = task.Status,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Tokens.OnTone(task.Tone)
            }
        };
        SemanticProperties.SetDescription(pill, task.Status);

        var head = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        head.Add(new Label
        {
            Text = task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary
        }, 0);
        head.Add(pill, 1);

        var stack = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(14, 12), Children = { head } };

        if (!string.IsNullOrWhiteSpace(task.Instructions))
        {
            stack.Add(new Label { Text = task.Instructions, FontSize = 12, TextColor = Tokens.TextSecondary });
        }

        stack.Add(new Label
        {
            Text = $"{(task.IsRequired ? "Required" : "Optional")} · Owner: {task.Owner}",
            FontSize = 11,
            TextColor = Tokens.TextTertiary
        });

        return new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = stack
        };
    }

    private static View Note(string text) => new Label
    {
        Text = text,
        FontSize = 11,
        TextColor = Tokens.TextSecondary,
        Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 0)
    };

    private static string Rupees(decimal amount) => string.Format(India, "₹{0:N0}", amount);
}
