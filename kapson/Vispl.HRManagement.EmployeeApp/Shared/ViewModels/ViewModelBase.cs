using System.ComponentModel;
using System.Runtime.CompilerServices;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Shared.ViewModels;

/// <summary>
/// The six states every screen must be able to draw.
///
/// <c>NotReady</c> and <c>Denied</c> are the two that a generic "loading / loaded / error"
/// model collapses, and they are exactly the two this product needs kept apart: roughly a
/// quarter of the app answers 501 during Phase 1-3, and that is a designed state, not a fault.
/// </summary>
public enum ScreenState
{
    Loading,
    Ready,
    Empty,
    Error,
    NotReady,
    Denied,
    Offline
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Turns a failed call into a state the screen renders, rather than into an exception a user
/// sees. Every ViewModel loads through <see cref="RunAsync"/> so no screen can be written with
/// only a happy path.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private ScreenState state = ScreenState.Loading;
    private string? message;
    private string? notReadyModule;
    private IReadOnlyList<string> notReadyComponents = [];
    private string? correlationId;
    private bool isRefreshing;

    public ScreenState State
    {
        get => state;
        private set
        {
            if (Set(ref state, value))
            {
                Raise(nameof(IsLoading));
                Raise(nameof(IsReady));
                Raise(nameof(IsEmpty));
                Raise(nameof(HasProblem));
                Raise(nameof(IsNotReady));
            }
        }
    }

    public bool IsLoading => State == ScreenState.Loading;
    public bool IsReady => State == ScreenState.Ready;
    public bool IsEmpty => State == ScreenState.Empty;
    public bool IsNotReady => State == ScreenState.NotReady;

    public bool HasProblem =>
        State is ScreenState.Error or ScreenState.NotReady or ScreenState.Denied or ScreenState.Offline;

    /// <summary>What to show the person. Never a stack trace, never an exception type.</summary>
    public string? Message
    {
        get => message;
        private set => Set(ref message, value);
    }

    /// <summary>The module a 501 named, so the panel can say which feature is missing.</summary>
    public string? NotReadyModule
    {
        get => notReadyModule;
        private set => Set(ref notReadyModule, value);
    }

    public IReadOnlyList<string> NotReadyComponents
    {
        get => notReadyComponents;
        private set => Set(ref notReadyComponents, value);
    }

    /// <summary>Shown on long-press, so a support ticket can name the exact request.</summary>
    public string? CorrelationId
    {
        get => correlationId;
        private set => Set(ref correlationId, value);
    }

    public bool IsRefreshing
    {
        get => isRefreshing;
        set => Set(ref isRefreshing, value);
    }

    /// <summary>Loads the screen. Implementations set <see cref="Ready"/> or <see cref="Empty"/>.</summary>
    public abstract Task LoadAsync(CancellationToken ct = default);

    public async Task RefreshAsync()
    {
        IsRefreshing = true;

        try
        {
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    protected void Ready() => Transition(ScreenState.Ready, null);

    protected void Empty(string text) => Transition(ScreenState.Empty, text);

    protected void Loading() => Transition(ScreenState.Loading, null);

    /// <summary>
    /// Runs a load and maps every typed client failure onto the state that belongs to it.
    ///
    /// Returns false when the work did not complete, so a caller can stop rather than carry on
    /// binding to values it never received.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> work, bool showLoading = true)
    {
        if (showLoading && !IsRefreshing)
        {
            Loading();
        }

        try
        {
            await work();
            return true;
        }
        catch (MobileFeatureNotReadyException notReady)
        {
            NotReadyModule = notReady.Module;
            NotReadyComponents = notReady.RequiredComponents;
            CorrelationId = notReady.CorrelationId;
            Transition(ScreenState.NotReady, $"{notReady.Module} is not available yet.");
            return false;
        }
        catch (MobileAccessDeniedException denied)
        {
            CorrelationId = denied.CorrelationId;
            Transition(ScreenState.Denied, denied.Message);
            return false;
        }
        catch (MobileNotFoundException notFound)
        {
            Transition(ScreenState.Empty, notFound.Message);
            return false;
        }
        catch (MobileOfflineException offline)
        {
            Transition(ScreenState.Offline, offline.Message);
            return false;
        }
        catch (MobileSignedOutException signedOut)
        {
            Transition(ScreenState.Denied, signedOut.Message);
            return false;
        }
        catch (MobileApiException api)
        {
            CorrelationId = api.CorrelationId;
            Transition(ScreenState.Error, api.Message);
            return false;
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-load. Not a failure worth drawing.
            return false;
        }
        catch (Exception exception)
        {
            Transition(ScreenState.Error, "Something went wrong loading this screen.");
            System.Diagnostics.Debug.WriteLine(exception);
            return false;
        }
    }

    private void Transition(ScreenState next, string? text)
    {
        Message = text;
        State = next;
    }
}
