using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using AmfibiaStudio.Models;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.Services;

/// <summary>Всплывающее уведомление.</summary>
public class ToastViewModel
{
    public ToastViewModel(string message, bool isError)
    {
        Message = message;
        IsError = isError;
    }

    public string Message { get; }
    public bool IsError { get; }
}

/// <summary>Очередь уведомлений: последние 3 сообщения, каждое исчезает через 4 секунды.</summary>
public class Notifier : ObservableObject
{
    private readonly SynchronizationContext? ui = SynchronizationContext.Current;

    public ObservableCollection<ToastViewModel> Toasts { get; } = new();

    public string LastMessage { get; private set; } = string.Empty;

    public bool LastIsError { get; private set; }

    public void Show(string message, bool isError = false)
    {
        LastMessage = message;
        LastIsError = isError;
        var toast = new ToastViewModel(message, isError);
        Toasts.Add(toast);
        while (Toasts.Count > 3) Toasts.RemoveAt(0);
        OnPropertyChanged(nameof(LastMessage));

        if (ui != null)
        {
            var timer = new Timer(_ => ui.Post(__ => Toasts.Remove(toast), null), null, 4000, Timeout.Infinite);
            GC.KeepAlive(timer);
        }
    }
}

/// <summary>
/// Общий контекст панели: данные сайта, изменяемое состояние, сервисы, вошедший сотрудник,
/// журнал действий и права ролей. Все ViewModel получают его в конструкторе.
/// </summary>
public class StudioContext
{
    /// <summary>Какие разделы видит каждая роль (как проверки checkRole на сервере сайта).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Sections = new Dictionary<string, string[]>
    {
        ["owner"] = new[] { "dashboard", "products", "orders", "warehouse", "reviews", "promos", "rates", "users", "brands", "audit", "settings" },
        ["moderator"] = new[] { "dashboard", "products", "reviews", "users", "brands", "audit", "settings" },
        ["support"] = new[] { "dashboard", "orders", "reviews", "users", "brands", "settings" },
        ["warehouse"] = new[] { "dashboard", "orders", "warehouse", "brands", "settings" },
        ["courier"] = new[] { "orders", "brands", "settings" },
        ["seller"] = new[] { "products", "warehouse", "brands", "settings" }
    };

    public StudioContext(StudioData data, IStudioStore store, IDialogService dialogs, IThemeService theme, IRatesProvider rates)
    {
        Data = data;
        Store = store;
        Dialogs = dialogs;
        Theme = theme;
        Rates = rates;
        State = store.Load() ?? data.CreateInitialState();
        data.Link(State.Products);
    }

    public StudioData Data { get; private set; }
    public StudioState State { get; private set; }
    public IStudioStore Store { get; }
    public IDialogService Dialogs { get; }
    public IThemeService Theme { get; }
    public IRatesProvider Rates { get; }
    public Notifier Notifier { get; } = new();

    public User? CurrentUser { get; set; }

    public string UserName => CurrentUser?.FullName ?? "Система";

    public string Role => CurrentUser?.Role ?? string.Empty;

    /// <summary>Данные изменились — дашборд и счётчики в меню пересчитываются.</summary>
    public event EventHandler? Changed;

    public bool CanOpen(string section) => Sections.TryGetValue(Role, out string[]? list) && list.Contains(section);

    public bool IsRole(params string[] roles) => roles.Contains(Role);

    /// <summary>Записать действие в журнал, сохранить данные и сообщить об изменении.</summary>
    public void Log(string action, string details)
    {
        State.Audit.Insert(0, new AuditEntry { At = DateTime.Now, User = UserName, Action = action, Details = details });
        Save();
    }

    public void Save()
    {
        Store.Save(State);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Вернуть демо-данные сайта (удаляет сохранённые изменения).</summary>
    public void ResetDemo()
    {
        Store.Reset();
        Data = StudioData.LoadEmbedded();
        State = Data.CreateInitialState();
        Data.Link(State.Products);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Product? FindProduct(string id) => State.Products.FirstOrDefault(p => p.Id == id);

    public StockRow StockOf(string productId)
    {
        StockRow? row = State.Stock.FirstOrDefault(s => s.Product == productId);
        if (row == null)
        {
            row = new StockRow { Product = productId, Quantities = new List<int> { 0, 0, 0 } };
            State.Stock.Add(row);
        }

        return row;
    }
}
