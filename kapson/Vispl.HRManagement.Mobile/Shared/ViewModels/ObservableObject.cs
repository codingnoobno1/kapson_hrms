using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Vispl.HRManagement.Mobile.Shared.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public abstract class ScreenViewModel : ObservableObject
{
    private bool isBusy;
    private string? errorMessage;

    public bool IsBusy
    {
        get => isBusy;
        protected set => SetProperty(ref isBusy, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        protected set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Loads the screen. Data now comes over HTTP, so it cannot happen in a constructor the way
    /// the demo service allowed — pages call this from OnAppearing.
    /// </summary>
    public virtual Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Runs a load, turning a client failure into a message rather than an unhandled exception.
    /// Every screen goes through here so none can be written with only a happy path.
    /// </summary>
    protected async Task RunAsync(Func<Task> work)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await work();
        }
        catch (Vispl.HRManagement.Mobile.Client.Http.MobileFeatureNotReadyException notReady)
        {
            ErrorMessage = $"{notReady.Module} is not available yet.";
        }
        catch (Vispl.HRManagement.Mobile.Client.Http.MobileAccessDeniedException denied)
        {
            ErrorMessage = denied.Message;
        }
        catch (Vispl.HRManagement.Mobile.Client.Http.MobileOfflineException)
        {
            ErrorMessage = "No connection to the Kapsons server.";
        }
        catch (Vispl.HRManagement.Mobile.Client.Http.MobileApiException api)
        {
            ErrorMessage = api.Message;
        }
        catch (Exception exception)
        {
            ErrorMessage = "Something went wrong loading this screen.";
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
