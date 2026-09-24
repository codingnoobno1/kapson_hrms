namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class BrandHeader : ContentView
{
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(BrandHeader), "Recruitment workspace");

    public static readonly BindableProperty NotificationCountProperty = BindableProperty.Create(
        nameof(NotificationCount), typeof(string), typeof(BrandHeader), "5");

    public BrandHeader() => InitializeComponent();

    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string NotificationCount { get => (string)GetValue(NotificationCountProperty); set => SetValue(NotificationCountProperty, value); }
}
