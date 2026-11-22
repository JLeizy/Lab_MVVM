using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Строка таблицы промокодов.</summary>
public class PromoRow : ObservableObject
{
    private readonly StudioContext context;

    public PromoRow(PromoCode promo, StudioContext context)
    {
        Promo = promo;
        this.context = context;
    }

    public PromoCode Promo { get; }
    public string Code => Promo.Code;
    public string Percent => $"−{Promo.Percent}%";
    public string Period => $"{Promo.ValidFrom:dd.MM.yyyy} — {(Promo.ValidTo is DateTime to ? to.ToString("dd.MM.yyyy") : "бессрочно")}";
    public int Used => context.State.Orders.Count(o => o.Promo == Promo.Code && o.Status != "cancelled");
    public string Usage => Promo.UsageLimit is int limit ? $"{Used} из {limit}" : $"{Used} · без лимита";
    public string State => PromosViewModel.StateOf(Promo, DateTime.Today, Used);
    public bool IsActive => State == "действует";

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// ViewModel промокодов: таблица, редактирование с проверкой полей, создание, удаление,
/// включение и выключение. Правила — как в таблице promo_codes сайта.
/// </summary>
public class PromosViewModel : PageViewModel
{
    private static readonly Regex CodePattern = new("^[A-Z0-9]{4,16}$");

    private readonly List<PromoRow> rows;
    private PromoRow? selected;
    private bool isNew;
    private string code = string.Empty, percentText = string.Empty, fromText = string.Empty, toText = string.Empty, limitText = string.Empty;
    private bool active = true;

    public PromosViewModel(StudioContext context) : base(context)
    {
        rows = context.State.Promos.Select(p => new PromoRow(p, context)).ToList();
        NewCommand = new RelayCommand(StartNew, () => CanEdit);
        SaveCommand = new RelayCommand(Save, () => CanEdit && (Selected != null || isNew) && !HasErrors);
        DeleteCommand = new RelayCommand(Delete, () => CanEdit && Selected != null);
        Selected = rows.FirstOrDefault();
    }

    public override string Title => "Промокоды";
    public override string Subtitle => $"{Fmt.Plural(rows.Count(r => r.IsActive), "действует", "действуют", "действуют")} из {rows.Count} · скидка применяется к сумме товаров";

    public bool CanEdit => Context.IsRole("owner");
    public IReadOnlyList<PromoRow> Items => rows.ToList();

    public PromoRow? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value) && value != null)
            {
                isNew = false;
                Fill(value.Promo);
            }

            RefreshCommands();
        }
    }

    public string EditorTitle => isNew ? "Новый промокод" : $"Промокод {Selected?.Code}";
    public string Code { get => code; set { if (SetProperty(ref code, value.Trim().ToUpperInvariant())) Validate(); } }
    public string PercentText { get => percentText; set { if (SetProperty(ref percentText, value)) Validate(); } }
    public string FromText { get => fromText; set { if (SetProperty(ref fromText, value)) Validate(); } }
    public string ToText { get => toText; set { if (SetProperty(ref toText, value)) Validate(); } }
    public string LimitText { get => limitText; set { if (SetProperty(ref limitText, value)) Validate(); } }
    public bool Active { get => active; set => SetProperty(ref active, value); }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public static string StateOf(PromoCode p, DateTime today, int used)
    {
        if (!p.Active) return "выключен";
        if (today < p.ValidFrom.Date) return "скоро";
        if (p.ValidTo is DateTime to && today > to.Date) return "истёк";
        if (p.UsageLimit is int limit && used >= limit) return "лимит исчерпан";
        return "действует";
    }

    private static bool TryDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private void Fill(PromoCode p)
    {
        code = p.Code;
        percentText = p.Percent.ToString();
        fromText = p.ValidFrom.ToString("dd.MM.yyyy");
        toText = p.ValidTo?.ToString("dd.MM.yyyy") ?? string.Empty;
        limitText = p.UsageLimit?.ToString() ?? string.Empty;
        active = p.Active;
        OnPropertyChanged(string.Empty);
        Validate();
    }

    private void StartNew()
    {
        selected = null;
        isNew = true;
        code = string.Empty;
        percentText = "10";
        fromText = DateTime.Today.ToString("dd.MM.yyyy");
        toText = DateTime.Today.AddMonths(1).ToString("dd.MM.yyyy");
        limitText = "100";
        active = true;
        OnPropertyChanged(string.Empty);
        Validate();
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        SaveCommand?.RaiseCanExecuteChanged();
        DeleteCommand?.RaiseCanExecuteChanged();
    }

    /// <summary>Правила проверки промокода.</summary>
    private void Validate()
    {
        bool duplicate = rows.Any(r => r.Code == code && (isNew || r != Selected));
        SetError(!CodePattern.IsMatch(code) ? "Код — 4–16 латинских букв и цифр, например SALE20." : duplicate ? "Такой промокод уже есть." : null, nameof(Code));
        SetError(!int.TryParse(percentText, out int pct) || pct < 1 || pct > 90 ? "Скидка — целое число от 1 до 90 %." : null, nameof(PercentText));
        bool fromOk = TryDate(fromText, out DateTime from);
        SetError(fromOk ? null : "Дата в формате ДД.ММ.ГГГГ.", nameof(FromText));
        string? toError = null;
        if (toText.Trim().Length > 0)
        {
            if (!TryDate(toText, out DateTime to)) toError = "Дата в формате ДД.ММ.ГГГГ или пусто (бессрочно).";
            else if (fromOk && to < from) toError = "Окончание раньше начала.";
        }

        SetError(toError, nameof(ToText));
        SetError(limitText.Trim().Length > 0 && (!int.TryParse(limitText, out int lim) || lim < 1) ? "Лимит — целое число от 1 или пусто." : null, nameof(LimitText));
    }

    protected override void OnErrorsUpdated() => SaveCommand?.RaiseCanExecuteChanged();

    private void Save()
    {
        Validate();
        if (HasErrors) return;
        PromoCode target = isNew ? new PromoCode() : Selected!.Promo;
        target.Code = code;
        target.Percent = int.Parse(percentText);
        target.ValidFrom = TryDate(fromText, out DateTime f) ? f : DateTime.Today;
        target.ValidTo = TryDate(toText, out DateTime t) ? t : null;
        target.UsageLimit = int.TryParse(limitText, out int l) ? l : null;
        target.Active = active;

        if (isNew)
        {
            Context.State.Promos.Add(target);
            var row = new PromoRow(target, Context);
            rows.Add(row);
            isNew = false;
            selected = row;
        }

        Context.Log("Промокод сохранён", $"{target.Code} · −{target.Percent}% · {(target.Active ? "включён" : "выключен")}");
        foreach (PromoRow r in rows) r.Refresh();
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(EditorTitle));
        RefreshCommands();
        Notify($"Промокод {target.Code} сохранён.");
    }

    private void Delete()
    {
        if (Selected == null) return;
        PromoRow row = Selected;
        if (!Context.Dialogs.Confirm("Удалить промокод", $"Удалить промокод {row.Code}? Уже оформленные заказы не изменятся.", "Удалить")) return;
        Context.State.Promos.Remove(row.Promo);
        rows.Remove(row);
        Context.Log("Промокод удалён", row.Code);
        Selected = rows.FirstOrDefault();
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Subtitle));
        Notify($"Промокод {row.Code} удалён.");
    }
}

/// <summary>
/// ViewModel курсов валют: ручной ввод с проверкой и загрузка официальных курсов НБРБ
/// (асинхронная команда — окно не зависает, кнопка недоступна, пока идёт загрузка).
/// </summary>
public class RatesViewModel : PageViewModel
{
    private string usdText, eurText;
    private string status = string.Empty;

    public RatesViewModel(StudioContext context) : base(context)
    {
        usdText = context.State.Rates.USD.ToString("0.0000", Fmt.Ru);
        eurText = context.State.Rates.EUR.ToString("0.0000", Fmt.Ru);
        SaveCommand = new RelayCommand(Save, () => CanEdit && !HasErrors);
        LoadCommand = new AsyncRelayCommand(LoadFromNbrbAsync, () => CanEdit);
    }

    public override string Title => "Курсы валют";
    public override string Subtitle => $"Обновлено {Fmt.DateTime(Context.State.Rates.Updated)} · цены на сайте пересчитываются по этим курсам";

    public bool CanEdit => Context.IsRole("owner");

    public string UsdText { get => usdText; set { if (SetProperty(ref usdText, value)) Validate(); } }
    public string EurText { get => eurText; set { if (SetProperty(ref eurText, value)) Validate(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }

    public RelayCommand SaveCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }

    /// <summary>Примеры цен в долларах и евро — пересчитываются сразу при вводе курса.</summary>
    public IReadOnlyList<string> Preview
    {
        get
        {
            if (!TryRate(usdText, out decimal usd) || !TryRate(eurText, out decimal eur)) return new[] { "Введите курсы, чтобы увидеть пересчёт цен." };
            return Context.State.Products.Where(p => p.Iconic).OrderByDescending(p => p.Views).Take(4)
                .Select(p => $"{p.Brand} — {p.Name}: {Fmt.Money(p.Price)} = ${p.Price / usd:N2} = €{p.Price / eur:N2}").ToList();
        }
    }

    /// <summary>История изменений курсов из журнала.</summary>
    public IReadOnlyList<AuditEntry> History => Context.State.Audit.Where(a => a.Action.StartsWith("Курсы")).Take(8).ToList();

    public static bool TryRate(string text, out decimal value) =>
        decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && value is >= 0.5m and <= 20m;

    private void Validate()
    {
        SetError(TryRate(usdText, out _) ? null : "Курс — число от 0,5 до 20, например 3,2051.", nameof(UsdText));
        SetError(TryRate(eurText, out _) ? null : "Курс — число от 0,5 до 20, например 3,4722.", nameof(EurText));
        OnPropertyChanged(nameof(Preview));
    }

    protected override void OnErrorsUpdated() => SaveCommand?.RaiseCanExecuteChanged();

    private void Save()
    {
        if (!TryRate(usdText, out decimal usd) || !TryRate(eurText, out decimal eur)) return;
        Apply(usd, eur, DateTime.Now, "вручную");
    }

    private void Apply(decimal usd, decimal eur, DateTime date, string source)
    {
        CurrencyRates rates = Context.State.Rates;
        string details = $"USD {rates.USD:0.0000} → {usd:0.0000} · EUR {rates.EUR:0.0000} → {eur:0.0000} ({source})";
        rates.USD = usd;
        rates.EUR = eur;
        rates.Updated = date;
        Context.Log("Курсы валют", details);
        usdText = usd.ToString("0.0000", Fmt.Ru);
        eurText = eur.ToString("0.0000", Fmt.Ru);
        OnPropertyChanged(string.Empty);
        Notify($"Курсы обновлены: USD {usd:0.0000}, EUR {eur:0.0000}.");
    }

    public async Task LoadFromNbrbAsync()
    {
        Status = "Загрузка курсов с api.nbrb.by…";
        try
        {
            RatesResult result = await Context.Rates.LoadAsync();
            Apply(result.Usd, result.Eur, DateTime.Now, $"НБРБ на {result.Date:dd.MM.yyyy}");
            Status = $"Официальные курсы НБРБ на {result.Date:dd.MM.yyyy} загружены.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Status = "Не удалось загрузить курсы НБРБ: нет интернета или сайт недоступен. Курсы не изменены.";
            Notify(Status, isError: true);
        }
    }
}

/// <summary>Строка таблицы пользователей.</summary>
public class UserRow : ObservableObject
{
    public UserRow(User user, IReadOnlyList<Order> orders)
    {
        User = user;
        OrdersCount = orders.Count;
        Spent = orders.Where(o => o.Status is not ("cancelled" or "returned")).Sum(o => o.Total);
    }

    public User User { get; }
    public string Name => User.FullName;
    public string Email => User.Email;
    public string Role => User.RoleName;
    public string City => User.City;
    public string Phone => User.Phone;
    public int OrdersCount { get; }
    public decimal Spent { get; }
    public string SpentText => Spent == 0 ? "—" : Fmt.Money(Spent);
    public bool Blocked => User.Blocked;
    public string State => User.Blocked ? "заблокирован" : "активен";

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>ViewModel пользователей: поиск, фильтр по ролям, блокировка с подтверждением.</summary>
public class UsersViewModel : PageViewModel
{
    private readonly List<UserRow> rows;
    private IReadOnlyList<UserRow> items = Array.Empty<UserRow>();
    private UserRow? selected;
    private string search = string.Empty;
    private string role = string.Empty;

    public UsersViewModel(StudioContext context) : base(context)
    {
        rows = context.State.Users.Select(u => new UserRow(u, context.State.Orders.Where(o => string.Equals(o.User, u.Email, StringComparison.OrdinalIgnoreCase)).ToList())).ToList();
        Roles = new[] { new Option("", "Все роли") }.Concat(rows.GroupBy(r => (r.User.Role, r.Role)).Select(g => new Option(g.Key.Item1, g.Key.Item2))).ToList();
        ToggleBlockCommand = new RelayCommand(ToggleBlock, () => Selected != null && CanBlock(Selected));
        ApplyFilter();
    }

    public override string Title => "Пользователи";
    public override string Subtitle => $"{Fmt.Plural(rows.Count(r => r.User.Role == "buyer"), "покупатель", "покупателя", "покупателей")} · {Fmt.Plural(rows.Count(r => r.User.Role != "buyer"), "сотрудник", "сотрудника", "сотрудников")}";

    public IReadOnlyList<Option> Roles { get; }
    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyFilter(); } }
    public string Role { get => role; set { if (SetProperty(ref role, value ?? "")) ApplyFilter(); } }
    public IReadOnlyList<UserRow> Items { get => items; private set => SetProperty(ref items, value); }

    public UserRow? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value))
            {
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(BlockText));
                ToggleBlockCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BlockText => Selected?.Blocked == true ? "Разблокировать" : "Заблокировать";

    public IReadOnlyList<string> SelectedOrders => Selected == null ? Array.Empty<string>()
        : Context.State.Orders.Where(o => string.Equals(o.User, Selected.Email, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.Created).Take(6)
            .Select(o => $"{o.Number} · {Fmt.Date(o.Created)} · {Context.Data.StatusName(o.Status)} · {Fmt.Money(o.Total)}").ToList();

    public RelayCommand ToggleBlockCommand { get; }

    /// <summary>Блокировать могут владелец, модератор и поддержка; себя и владельца — нельзя.</summary>
    public bool CanBlock(UserRow row) =>
        Context.IsRole("owner", "moderator", "support") && row.User.Role != "owner" && row.Email != Context.CurrentUser?.Email;

    private void ApplyFilter()
    {
        IEnumerable<UserRow> q = rows;
        if (role.Length > 0) q = q.Where(r => r.User.Role == role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Email.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.City.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Phone.Contains(s));
        }

        Items = q.OrderByDescending(r => r.Spent).ToList();
    }

    private void ToggleBlock()
    {
        if (Selected == null || !CanBlock(Selected)) return;
        UserRow row = Selected;
        bool block = !row.Blocked;
        if (!Context.Dialogs.Confirm(block ? "Блокировка" : "Разблокировка",
                block ? $"Заблокировать {row.Name}? Пользователь не сможет войти на сайт." : $"Разблокировать {row.Name}?",
                block ? "Заблокировать" : "Разблокировать"))
        {
            return;
        }

        row.User.Blocked = block;
        Context.Log(block ? "Пользователь заблокирован" : "Пользователь разблокирован", $"{row.Name} ({row.Email})");
        row.Refresh();
        OnPropertyChanged(nameof(BlockText));
        Notify(block ? $"{row.Name} заблокирован." : $"{row.Name} разблокирован.");
    }
}
