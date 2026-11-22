using System;
using AmfibiaStudio.Services;

namespace AmfibiaStudio.ViewModels.Base;

/// <summary>
/// Страница панели. Окно показывает её через DataTemplate:
/// WPF сам находит Вид (UserControl) по типу ViewModel — это и есть «навигация по данным» в MVVM.
/// </summary>
public abstract class PageViewModel : ValidatableObject, IDisposable
{
    protected PageViewModel(StudioContext context)
    {
        Context = context;
    }

    protected StudioContext Context { get; }

    public abstract string Title { get; }

    public abstract string Subtitle { get; }

    /// <summary>Страница закрыта — отписаться от событий контекста.</summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    protected void Notify(string message, bool isError = false) => Context.Notifier.Show(message, isError);
}
