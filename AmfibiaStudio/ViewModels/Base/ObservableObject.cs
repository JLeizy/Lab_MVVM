using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AmfibiaStudio.ViewModels.Base;

/// <summary>
/// Основа MVVM: объект сообщает Виду об изменении своих свойств (INotifyPropertyChanged),
/// и привязки ({Binding}) обновляются сами — без кода в окне.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Меняет значение поля и, если оно действительно изменилось, сообщает об этом.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
