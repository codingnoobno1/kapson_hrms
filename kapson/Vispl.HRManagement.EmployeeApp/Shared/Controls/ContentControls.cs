using QRCoder;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp.Shared.Controls;

/// <summary>
/// EW-030 and EW-031. The Home identity card: accent plate, avatar, name, role, and the QR.
///
/// The QR is drawn from <c>EmployeeIdentityCardDto.QrPayload</c>, which the server currently
/// sets to the plain employee number. That identifies a person and proves nothing, so this
/// renders it as a visual badge and no scanner should accept it — the signed
/// <c>GatePassCredentialDto.VerificationToken</c> is what a gate reads.
/// </summary>
public sealed class IdentityCard : ContentView
{
    private readonly Label nameLabel = new()
    {
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White
    };

    private readonly Label roleLabel = new() { FontSize = 13, TextColor = Colors.White };
    private readonly Label phoneLabel = new() { FontSize = 13, TextColor = Colors.White };

    private readonly Image qrImage = new() { WidthRequest = 50, HeightRequest = 50, Aspect = Aspect.AspectFit };
    private readonly Image photo = new() { WidthRequest = 60, HeightRequest = 60, Aspect = Aspect.AspectFill, IsVisible = false };
    private readonly Label initialsLabel = new()
    {
        FontSize = 20,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.BrandAccent,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly Label numberLabel = new()
    {
        FontSize = 8,
        TextColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center
    };

    private readonly Border qrPlate;

    public IdentityCard()
    {
        // 5.png: a round photo, falling back to initials on white when there is none.
        var avatar = new Border
        {
            WidthRequest = 60,
            HeightRequest = 60,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 30 },
            Content = new Grid { Children = { initialsLabel, photo } }
        };

        // A real QR of QrPayload, drawn black on a white plate as the image shows. It is a visual
        // identity badge only (employee-app.md §9.6): gates scan the signed gate pass token.
        qrPlate = new Border
        {
            WidthRequest = 58,
            HeightRequest = 58,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 4,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = qrImage
        };

        var details = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children = { nameLabel, roleLabel, phoneLabel }
        };

        var qrColumn = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children = { qrPlate, numberLabel }
        };

        var grid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        grid.Add(avatar, 0);
        grid.Add(details, 1);
        grid.Add(qrColumn, 2);

        Content = new Border
        {
            BackgroundColor = Tokens.BrandAccent,
            StrokeThickness = 0,
            Padding = 14,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = grid
        };
    }

    public static readonly BindableProperty IdentityProperty = BindableProperty.Create(
        nameof(Identity), typeof(EmployeeIdentityCardDto), typeof(IdentityCard),
        propertyChanged: OnIdentityChanged);

    public EmployeeIdentityCardDto? Identity
    {
        get => (EmployeeIdentityCardDto?)GetValue(IdentityProperty);
        set => SetValue(IdentityProperty, value);
    }

    private static void OnIdentityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var card = (IdentityCard)bindable;
        var identity = newValue as EmployeeIdentityCardDto;

        card.IsVisible = identity is not null;

        if (identity is null)
        {
            return;
        }

        card.nameLabel.Text = identity.DisplayName;

        // "Store Manager   📍 Operations", as the image sets the two side by side.
        card.roleLabel.Text = string.Join(
            "    ",
            new[]
            {
                identity.JobTitle,
                string.IsNullOrWhiteSpace(identity.DepartmentName) ? null : $"\U0001F4CD {identity.DepartmentName}"
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        card.phoneLabel.Text = identity.Phone ?? string.Empty;
        card.phoneLabel.IsVisible = !string.IsNullOrWhiteSpace(identity.Phone);
        card.numberLabel.Text = identity.EmployeeNumber;
        card.initialsLabel.Text = Initials(identity.DisplayName);

        card.photo.IsVisible = !string.IsNullOrWhiteSpace(identity.AvatarUrl);
        card.photo.Source = card.photo.IsVisible ? ImageSource.FromUri(new Uri(identity.AvatarUrl!)) : null;

        card.qrPlate.IsVisible = !string.IsNullOrWhiteSpace(identity.QrPayload);
        card.qrImage.Source = card.qrPlate.IsVisible ? RenderQr(identity.QrPayload) : null;

        // Announced as one label rather than four, so a screen reader does not read the card
        // as a list of disconnected fragments.
        SemanticProperties.SetDescription(
            card, $"{identity.DisplayName}, {identity.JobTitle}, {identity.DepartmentName}");
    }

    /// <summary>Encodes the payload as a PNG in memory. Nothing is written to disk.</summary>
    internal static ImageSource RenderQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);

        return ImageSource.FromStream(() => new MemoryStream(png));
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }
}

/// <summary>
/// EW-050. One day of attendance: status glyph, date, check-in and out, and a status pill.
///
/// The times come from the day's punches. A day with none shows what the server called it —
/// Absent, Leave, Holiday — rather than an empty row that reads like missing data.
/// </summary>
public sealed class AttendanceDayRow : ContentView
{
    public AttendanceDayRow()
    {
        Content = new Grid();
    }

    public void Bind(AttendanceDayDto day)
    {
        var tone = Tokens.ForDayStatus(day.Status.Code);

        var glyph = new Border
        {
            WidthRequest = 18,
            HeightRequest = 18,
            BackgroundColor = tone,
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 9 }
        };

        var punches = day.Punches
            .OrderBy(punch => punch.OccurredUtc)
            .ToList();

        var inPunch = punches.FirstOrDefault(punch =>
            string.Equals(punch.Type, "In", StringComparison.OrdinalIgnoreCase));

        var outPunch = punches.LastOrDefault(punch =>
            string.Equals(punch.Type, "Out", StringComparison.OrdinalIgnoreCase));

        var detail = inPunch is null && outPunch is null
            ? day.Status.Name
            : $"{day.Status.Name} · In {Local(inPunch?.OccurredUtc)} · Out {Local(outPunch?.OccurredUtc)}";

        var text = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = day.Date.ToString("dd-MMM-yyyy"),
                    FontSize = Tokens.FontRowTitle,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Tokens.TextPrimary
                },
                new Label
                {
                    Text = detail,
                    FontSize = Tokens.FontRowSubtitle,
                    TextColor = Tokens.TextSecondary
                }
            }
        };

        var pill = new StatusPill { Status = day.Status, VerticalOptions = LayoutOptions.Center };

        var grid = new Grid
        {
            Padding = new Thickness(Tokens.SpaceGutter, 12),
            ColumnSpacing = 12,
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        grid.Add(glyph, 0);
        grid.Add(text, 1);
        grid.Add(pill, 2);

        Content = grid;

        SemanticProperties.SetDescription(this, $"{day.Date:d MMMM yyyy}. {detail}");
    }

    private static string Local(DateTimeOffset? utc) =>
        utc is null ? "—" : utc.Value.ToLocalTime().ToString("HH:mm");
}

/// <summary>EW-061. One leave policy's balance, as the Leave tab opens on.</summary>
public sealed class LeaveBalanceCard : ContentView
{
    public void Bind(LeaveBalanceDto balance)
    {
        var figures = new HorizontalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Figure("Available", balance.Available, Tokens.StatusSuccess),
                Figure("Used", balance.Used, Tokens.TextSecondary),
                Figure("Pending", balance.Pending, Tokens.StatusWarning)
            }
        };

        Content = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            BackgroundColor = Tokens.Surface,
            Padding = 16,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = balance.LeaveTypeName,
                        FontSize = Tokens.FontRowTitle,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Tokens.TextPrimary
                    },
                    figures
                }
            }
        };

        SemanticProperties.SetDescription(
            this,
            $"{balance.LeaveTypeName}. {balance.Available:0.#} {balance.Unit} available, " +
            $"{balance.Used:0.#} used, {balance.Pending:0.#} pending.");
    }

    private static View Figure(string label, decimal value, Color tone) => new VerticalStackLayout
    {
        Spacing = 0,
        Children =
        {
            new Label
            {
                Text = value.ToString("0.#"),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = tone
            },
            new Label { Text = label, FontSize = 11, TextColor = Tokens.TextSecondary }
        }
    };
}

/// <summary>EW-060. One row of leave history.</summary>
public sealed class LeaveRequestRow : ContentView
{
    public void Bind(LeaveRequestListItemDto request, string? typeName = null)
    {
        var range = request.From == request.To
            ? $"{request.From:d MMM yyyy}"
            : $"{request.From:d MMM} – {request.To:d MMM yyyy}";

        var text = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = typeName ?? request.LeaveTypeCode,
                    FontSize = Tokens.FontRowTitle,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Tokens.TextPrimary
                },
                new Label
                {
                    Text = $"{range} ({request.Days:0.#} {(request.Days == 1 ? "day" : "days")})",
                    FontSize = Tokens.FontRowSubtitle,
                    TextColor = Tokens.TextSecondary
                }
            }
        };

        var grid = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 12,
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };

        grid.Add(text, 0);
        grid.Add(new StatusPill { Status = request.Status, VerticalOptions = LayoutOptions.Center }, 1);

        Content = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            BackgroundColor = Tokens.Surface,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = grid
        };

        SemanticProperties.SetDescription(
            this, $"{typeName ?? request.LeaveTypeCode}, {range}, {request.Status.Name}");
    }
}
