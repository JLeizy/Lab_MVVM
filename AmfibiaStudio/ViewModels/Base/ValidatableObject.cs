using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AmfibiaStudio.ViewModels.Base;

/// <summary>
/// ViewModel с проверкой полей (INotifyDataErrorInfo). Ошибка свойства автоматически
/// показывается во Виде под полем ввода — WPF сам подсвечивает поле.
/// </summary>
public abstract class ValidatableObject : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> errors = new();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName != null && errors.TryGetValue(propertyName, out List<string>? list) ? list : Enumerable.Empty<string>();

    /// <summary>Первая ошибка свойства (для подписи под полем).</summary>
    public string? FirstError(string propertyName) =>
        errors.TryGetValue(propertyName, out List<string>? list) ? list.FirstOrDefault() : null;

    public IReadOnlyDictionary<string, string> AllErrors => errors.ToDictionary(e => e.Key, e => e.Value[0]);

    /// <summary>Записывает ошибку свойства (null — ошибки нет).</summary>
    protected void SetError(string? error, [CallerMemberName] string propertyName = "")
    {
        bool had = errors.ContainsKey(propertyName);
        if (error == null)
        {
            if (!had) return;
            errors.Remove(propertyName);
        }
        else
        {
            errors[propertyName] = new List<string> { error };
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
        OnErrorsUpdated();
    }

    protected void ClearErrors()
    {
        foreach (string name in errors.Keys.ToList())
        {
            errors.Remove(name);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name));
        }

        OnPropertyChanged(nameof(HasErrors));
        OnErrorsUpdated();
    }

    /// <summary>Вызывается при любом изменении ошибок (например, чтобы обновить доступность кнопки «Сохранить»).</summary>
    protected virtual void OnErrorsUpdated()
    {
    }
}
