namespace Vispl.HRManagement.Mobile.Shared.Controls;

public partial class StatusPill : ContentView
{
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status), typeof(string), typeof(StatusPill), string.Empty);

    public StatusPill() => InitializeComponent();
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
}
