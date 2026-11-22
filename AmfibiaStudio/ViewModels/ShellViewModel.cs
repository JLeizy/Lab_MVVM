using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Пункт меню слева.</summary>
public class NavItem : ObservableObject
{
    private int badge;

    public NavItem(string key, string title, string glyph)
    {
        Key = key;
        Title = title;
        Glyph = glyph;
    }

    public string Key { get; }
    public string Title { get; }
    public string Glyph { get; }

    /// <summary>Счётчик «требует внимания» (новые заказы, отзывы на модерации…).</summary>
    public int Badge
    {
        get => badge;
        set
        {
            if (SetProperty(ref badge, value)) OnPropertyChanged(nameof(HasBadge));
        }
    }

    public bool HasBadge => Badge > 0;
}

/// <summary>
/// ViewModel главного окна панели: меню по роли сотрудника, текущая страница, уведомления, выход.
/// Окно показывает CurrentPage через DataTemplate — ViewModel не знает, как выглядит страница.
/// </summary>
public class ShellViewModel : ObservableObject, IDisposable
{
    private static readonly Dictionary<string, (string Title, string Glyph)> Menu = new()
    {
        ["dashboard"] = ("Дашборд", "◉"),
        ["products"] = ("Товары", "▦"),
        ["orders"] = ("Заказы", "☰"),
        ["warehouse"] = ("Склад", "▤"),
        ["reviews"] = ("Отзывы", "★"),
        ["promos"] = ("Промокоды", "%"),
        ["rates"] = ("Курсы валют", "¤"),
        ["users"] = ("Пользователи", "♟"),
        ["brands"] = ("Энциклопедия", "❦"),
        ["audit"] = ("Журнал", "≡"),
        ["settings"] = ("Настройки", "⚙")
    };

    private readonly StudioContext context;
    private NavItem? selectedItem;
    private PageViewModel? currentPage;

    public ShellViewModel(StudioContext context)
    {
        this.context = context;
        NavItems = new ObservableCollection<NavItem>(
            StudioContext.Sections[context.Role].Select(key => new NavItem(key, Menu[key].Title, Menu[key].Glyph)));
        LogoutCommand = new RelayCommand(() =>
        {
            context.Log("Выход из панели", context.UserName);
            LoggedOut?.Invoke(this, EventArgs.Empty);
        });
        NavigateCommand = new RelayCommand(p => { if (p is string key) Navigate(key); });

        context.Changed += OnContextChanged;
        UpdateBadges();
        SelectedItem = NavItems.First();
    }

    public event EventHandler? LoggedOut;

    public ObservableCollection<NavItem> NavItems { get; }

    /// <summary>Выбор пункта меню сразу открывает страницу.</summary>
    public NavItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (SetProperty(ref selectedItem, value) && value != null && currentPage?.GetType() != PageType(value.Key))
            {
                Open(value.Key);
            }
        }
    }

    public PageViewModel? CurrentPage
    {
        get => currentPage;
        private set => SetProperty(ref currentPage, value);
    }

    public string UserName => context.UserName;
    public string UserRole => context.CurrentUser?.RoleName ?? string.Empty;
    public string Initials => context.CurrentUser?.Initials ?? "";
    public Notifier Notifier => context.Notifier;

    public RelayCommand LogoutCommand { get; }

    /// <summary>Переход по ключу раздела (например, с дашборда «Все заказы»).</summary>
    public RelayCommand NavigateCommand { get; }

    public void Navigate(string key)
    {
        NavItem? item = NavItems.FirstOrDefault(n => n.Key == key);
        if (item != null) SelectedItem = item;
    }

    private static Type PageType(string key) => key switch
    {
        "dashboard" => typeof(DashboardViewModel),
        "products" => typeof(ProductsViewModel),
        "orders" => typeof(OrdersViewModel),
        "warehouse" => typeof(WarehouseViewModel),
        "reviews" => typeof(ReviewsViewModel),
        "promos" => typeof(PromosViewModel),
        "rates" => typeof(RatesViewModel),
        "users" => typeof(UsersViewModel),
        "brands" => typeof(BrandsViewModel),
        "audit" => typeof(AuditViewModel),
        _ => typeof(SettingsViewModel)
    };

    private void Open(string key)
    {
        currentPage?.Dispose();
        CurrentPage = key switch
        {
            "dashboard" => new DashboardViewModel(context, Navigate),
            "products" => new ProductsViewModel(context),
            "orders" => new OrdersViewModel(context),
            "warehouse" => new WarehouseViewModel(context),
            "reviews" => new ReviewsViewModel(context),
            "promos" => new PromosViewModel(context),
            "rates" => new RatesViewModel(context),
            "users" => new UsersViewModel(context),
            "brands" => new BrandsViewModel(context),
            "audit" => new AuditViewModel(context),
            _ => new SettingsViewModel(context, () => LoggedOut?.Invoke(this, EventArgs.Empty))
        };
    }

    private void OnContextChanged(object? sender, EventArgs e) => UpdateBadges();

    private void UpdateBadges()
    {
        foreach (NavItem item in NavItems)
        {
            item.Badge = item.Key switch
            {
                "orders" => context.State.Orders.Count(o => o.Status is "created" or "confirmed"),
                "reviews" => context.State.Reviews.Count(r => r.Status == "pending"),
                "warehouse" => context.State.Products.Count(p => !p.Preowned && context.StockOf(p.Id).Quantities.Sum() <= WarehouseViewModel.LowStock),
                _ => 0
            };
        }
    }

    public void Dispose()
    {
        context.Changed -= OnContextChanged;
        currentPage?.Dispose();
        GC.SuppressFinalize(this);
    }
}
