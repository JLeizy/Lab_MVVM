using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>
/// Корневая ViewModel окна: показывает либо вход, либо панель управления.
/// Окно привязано к CurrentView, а нужный Вид выбирается по DataTemplate.
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly StudioContext context;
    private object currentView = null!;

    public MainViewModel(StudioContext context)
    {
        this.context = context;
        context.Theme.Apply(context.State.Theme);
        ShowLogin();
    }

    public object CurrentView
    {
        get => currentView;
        private set => SetProperty(ref currentView, value);
    }

    public Notifier Notifier => context.Notifier;

    private void ShowLogin()
    {
        var login = new LoginViewModel(context);
        login.LoggedIn += (_, _) => ShowShell();
        CurrentView = login;
    }

    private void ShowShell()
    {
        var shell = new ShellViewModel(context);
        shell.LoggedOut += (_, _) =>
        {
            shell.Dispose();
            context.CurrentUser = null;
            ShowLogin();
        };
        CurrentView = shell;
    }
}
