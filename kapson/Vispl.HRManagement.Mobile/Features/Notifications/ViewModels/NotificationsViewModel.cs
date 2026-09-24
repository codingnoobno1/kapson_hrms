using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Models;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Notifications.ViewModels;

public sealed class NotificationsViewModel : ScreenViewModel
{
    private readonly IHrDataService data;
    private IReadOnlyList<NotificationSummary> allNotifications = [];
    private string selectedCategory = "General";

    public NotificationsViewModel(IHrDataService data)
    {
        this.data = data;
        SelectCategoryCommand = new Command<string>(category => SelectedCategory = category ?? "General");
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set { if (SetProperty(ref selectedCategory, value)) OnPropertyChanged(nameof(FilteredNotifications)); }
    }

    public IEnumerable<NotificationSummary> FilteredNotifications => SelectedCategory == "Interviews"
        ? allNotifications.Where(item => item.Category == "Interview")
        : allNotifications;

    public ICommand SelectCategoryCommand { get; }

    public override Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        allNotifications = await data.GetNotificationsAsync(cancellationToken);
        OnPropertyChanged(nameof(FilteredNotifications));
    });
}
