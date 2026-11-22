using System;
using System.Collections.Generic;
using System.Linq;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Строка списка заказов.</summary>
public class OrderRow : ObservableObject
{
    private readonly StudioContext context;

    public OrderRow(Order order, StudioContext context)
    {
        Order = order;
        this.context = context;
    }

    public Order Order { get; }
    public string Number => Order.Number;
    public string Date => Fmt.DateTime(Order.Created);
    public string Customer => Order.Customer;
    public string City => Order.City;
    public string Status => context.Data.StatusName(Order.Status);
    public string StatusKind => OrdersViewModel.Kind(Order.Status);
    public string Total => Fmt.Money(Order.Total);
    public string Delivery => context.Data.Delivery.FirstOrDefault(d => d.Code == Order.Delivery)?.Name ?? Order.Delivery;
    public string Paid => Order.Paid ? "оплачен" : "не оплачен";
    public int ItemsCount => Order.ItemsCount;
    public string Phone => Order.Phone;
    public string Email => Order.User;
    public string Tracking => Order.Tracking;
    public string SubtotalText => Fmt.Money(Order.Subtotal);
    public string DiscountText => Order.Discount > 0 ? $"−{Fmt.Money(Order.Discount)}{(Order.Promo != null ? $" ({Order.Promo})" : "")}" : "—";
    public string DeliveryPriceText => Order.DeliveryPrice == 0 ? "бесплатно" : Fmt.Money(Order.DeliveryPrice);

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>Шаг шкалы статусов в карточке заказа.</summary>
public record StatusStep(string Title, string Time, string State);

/// <summary>Товар в карточке заказа.</summary>
public record OrderLine(Product? Product, string Name, string Details, string Total);

/// <summary>
/// ViewModel раздела «Заказы». Правила переходов — как в orderService.js сайта:
/// статусы идут по порядку, «Передан в доставку» для курьера — только с назначенным курьером,
/// при сборке товар списывается со склада, отменить можно до передачи в доставку.
/// Кто какой шаг выполняет, зависит от роли сотрудника.
/// </summary>
public class OrdersViewModel : PageViewModel
{
    public static readonly string[] Tabs = { "Все", "Новые", "Сборка", "Доставка", "Завершённые", "Отменённые" };

    /// <summary>Какие роли могут перевести заказ В статус.</summary>
    private static readonly Dictionary<string, string[]> WhoCan = new()
    {
        ["confirmed"] = new[] { "owner", "support" },
        ["assembling"] = new[] { "owner", "warehouse" },
        ["assembled"] = new[] { "owner", "warehouse" },
        ["handed_to_delivery"] = new[] { "owner", "warehouse" },
        ["in_transit"] = new[] { "owner", "courier" },
        ["arrived"] = new[] { "owner", "courier", "support" },
        ["delivered"] = new[] { "owner", "courier", "support" }
    };

    private readonly List<OrderRow> allRows;
    private IReadOnlyList<OrderRow> items = Array.Empty<OrderRow>();
    private OrderRow? selected;
    private int tab;
    private string search = string.Empty;
    private string? courierEmail;

    public OrdersViewModel(StudioContext context) : base(context)
    {
        allRows = context.State.Orders.OrderByDescending(o => o.Created).Select(o => new OrderRow(o, context)).ToList();
        Couriers = context.State.Users.Where(u => u.Role == "courier").Select(u => new Option(u.Email, u.FullName)).ToList();

        AdvanceCommand = new RelayCommand(() => Advance(), () => NextStatus != null && CanDo(NextStatus));
        CancelCommand = new RelayCommand(Cancel, () => Selected != null && CanCancel(Selected.Order) && Context.IsRole("owner", "support"));
        MarkPaidCommand = new RelayCommand(MarkPaid, () => Selected is { Order.Paid: false } s && s.Order.Status is not ("cancelled" or "returned") && Context.IsRole("owner", "support", "courier"));
        AssignCourierCommand = new RelayCommand(AssignCourier, () => Selected != null && CourierEmail != null && Context.IsRole("owner", "warehouse"));
        ApplyFilter();
        Selected = Items.FirstOrDefault();
    }

    public override string Title => "Заказы";
    public override string Subtitle => "Статусы как на странице отслеживания сайта · курьеры · оплата · списание со склада";

    public IReadOnlyList<string> TabNames => Tabs;
    public IReadOnlyList<Option> Couriers { get; }

    public int Tab { get => tab; set { if (SetProperty(ref tab, value)) ApplyFilter(); } }
    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyFilter(); } }

    public IReadOnlyList<OrderRow> Items { get => items; private set => SetProperty(ref items, value); }
    public string CountText => Fmt.Plural(Items.Count, "заказ", "заказа", "заказов");

    public OrderRow? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value))
            {
                courierEmail = value?.Order.Courier;
                RefreshDetails();
            }
        }
    }

    public string? CourierEmail
    {
        get => courierEmail;
        set { if (SetProperty(ref courierEmail, value)) AssignCourierCommand.RaiseCanExecuteChanged(); }
    }

    // ----- карточка выбранного заказа -----

    public IReadOnlyList<StatusStep> Steps => Selected == null ? Array.Empty<StatusStep>() : BuildSteps(Selected.Order);
    public IReadOnlyList<OrderLine> Lines => Selected?.Order.Items.Select(i => new OrderLine(Context.FindProduct(i.Product), $"{i.Brand} — {i.Name}",
        $"Размер {i.Size} · {i.Quantity} шт. × {Fmt.Money(i.Price)}", Fmt.Money(i.Total))).ToList() ?? new List<OrderLine>();
    public string StatusHelp => Selected == null ? "" : Context.Data.StatusHelp(Selected.Order.Status);
    public string PaymentText => Selected == null ? "" : $"{Context.Data.Payments.FirstOrDefault(p => p.Code == Selected.Order.Payment)?.Name} · {Selected.Paid}";
    public string AddressText => Selected == null ? "" : $"{Selected.Order.City}, {Selected.Order.Address}";
    public string CourierText => Selected?.Order.Courier is string c ? Couriers.FirstOrDefault(x => x.Key == c)?.Title ?? c : "не назначен";
    public bool NeedsCourier => Selected != null && Selected.Order.Delivery == "courier" && Selected.Order.Status is "confirmed" or "assembling" or "assembled";
    public string? NextStatus => Selected == null ? null : Next(Selected.Order);
    public string AdvanceText => NextStatus == null ? "Заказ завершён" : $"Перевести в «{Context.Data.StatusName(NextStatus)}»";
    public string RuleHint { get; private set; } = string.Empty;

    public RelayCommand AdvanceCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand MarkPaidCommand { get; }
    public RelayCommand AssignCourierCommand { get; }

    public static string Kind(string status) => status switch
    {
        "created" or "confirmed" => "new",
        "assembling" or "assembled" => "work",
        "handed_to_delivery" or "in_transit" or "arrived" => "delivery",
        "delivered" => "done",
        _ => "cancel"
    };

    private bool IsPickup(Order o) => Context.Data.Delivery.FirstOrDefault(d => d.Code == o.Delivery)?.Pickup ?? true;

    /// <summary>Следующий статус по порядку сайта (для курьера «В пункте выдачи» пропускается).</summary>
    public string? Next(Order o)
    {
        int i = Context.Data.Flow.IndexOf(o.Status);
        if (i < 0 || i == Context.Data.Flow.Count - 1) return null;
        string next = Context.Data.Flow[i + 1];
        return next == "arrived" && !IsPickup(o) ? "delivered" : next;
    }

    public static bool CanCancel(Order o) => o.Status is "created" or "confirmed" or "assembling" or "assembled";

    private bool CanDo(string status) => WhoCan.TryGetValue(status, out string[]? roles) && Context.IsRole(roles);

    private void ApplyFilter()
    {
        IEnumerable<OrderRow> q = allRows;
        q = tab switch
        {
            1 => q.Where(r => r.StatusKind == "new"),
            2 => q.Where(r => r.StatusKind == "work"),
            3 => q.Where(r => r.StatusKind == "delivery"),
            4 => q.Where(r => r.StatusKind == "done"),
            5 => q.Where(r => r.StatusKind == "cancel"),
            _ => q
        };

        // Курьер видит только курьерскую доставку
        if (Context.Role == "courier") q = q.Where(r => r.Order.Delivery == "courier");

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(r => r.Number.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Customer.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.Order.Phone.Contains(s) || r.Order.Tracking.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        Items = q.ToList();
        OnPropertyChanged(nameof(CountText));
        if (Selected != null && !Items.Contains(Selected)) Selected = Items.FirstOrDefault();
    }

    private IReadOnlyList<StatusStep> BuildSteps(Order o)
    {
        string Time(string s) => o.StatusTime(s) is DateTime at ? Fmt.DateTime(at) : "";
        if (o.Status == "cancelled")
        {
            return o.History.Select(h => new StatusStep(Context.Data.StatusName(h.Status), Fmt.DateTime(h.At), h.Status == "cancelled" ? "cancelled" : "done")).ToList();
        }

        var steps = Context.Data.Flow.Where(s => IsPickup(o) || s != "arrived")
            .Select(s => new StatusStep(Context.Data.StatusName(s), Time(s),
                s == o.Status ? "current" : o.StatusTime(s) != null || o.Status == "returned" ? "done" : "todo")).ToList();
        if (o.Status == "returned") steps.Add(new StatusStep(Context.Data.StatusName("returned"), Time("returned"), "cancelled"));
        return steps;
    }

    private void RefreshDetails()
    {
        RuleHint = Selected == null ? "" : Hint(Selected.Order);
        foreach (string name in new[] { nameof(Steps), nameof(Lines), nameof(StatusHelp), nameof(PaymentText), nameof(AddressText), nameof(CourierText),
                                        nameof(NeedsCourier), nameof(NextStatus), nameof(AdvanceText), nameof(RuleHint), nameof(CourierEmail) })
        {
            OnPropertyChanged(name);
        }

        AdvanceCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        MarkPaidCommand.RaiseCanExecuteChanged();
        AssignCourierCommand.RaiseCanExecuteChanged();
    }

    private string Hint(Order o)
    {
        string? next = Next(o);
        if (next == null) return "Заказ завершён — дальнейших шагов нет.";
        if (!CanDo(next))
        {
            string roles = string.Join(", ", WhoCan[next].Select(r => Context.State.Users.FirstOrDefault(u => u.Role == r)?.RoleName ?? r).Distinct());
            return $"Следующий шаг выполняет: {roles}.";
        }

        return "";
    }

    /// <summary>Перевести заказ в следующий статус с проверкой правил.</summary>
    public bool Advance()
    {
        if (Selected == null) return false;
        Order o = Selected.Order;
        string? next = Next(o);
        if (next == null || !CanDo(next)) return false;

        if (next == "confirmed" && !o.Paid && o.Payment != "cash")
        {
            Notify("Заказ ещё не оплачен: подтвердить можно после оплаты (или при оплате наличными).", isError: true);
            return false;
        }

        if (next == "handed_to_delivery" && o.Delivery == "courier" && o.Courier == null)
        {
            Notify("Сначала назначьте курьера.", isError: true);
            return false;
        }

        if (next == "assembled" && !TakeFromStock(o))
        {
            return false;
        }

        o.Status = next;
        o.History.Add(new StatusChange { Status = next, At = DateTime.Now });
        if (next == "delivered" && o.Payment == "cash") o.Paid = true;
        Context.Log("Статус заказа", $"{o.Number} → {Context.Data.StatusName(next)}");
        Selected.Refresh();
        Notify($"Заказ {o.Number}: {Context.Data.StatusName(next)}.");
        ApplyFilter();
        RefreshDetails();
        return true;
    }

    /// <summary>Списание со склада при сборке: сначала Минск, потом Москва и Алматы.</summary>
    private bool TakeFromStock(Order o)
    {
        foreach (OrderItem item in o.Items)
        {
            if (Context.StockOf(item.Product).Quantities.Sum() < item.Quantity)
            {
                Notify($"Недостаточно на складе: {item.Name} (нужно {item.Quantity}). Пополните остатки в разделе «Склад».", isError: true);
                return false;
            }
        }

        foreach (OrderItem item in o.Items)
        {
            List<int> q = Context.StockOf(item.Product).Quantities;
            int need = item.Quantity;
            for (int w = 0; w < q.Count && need > 0; w++)
            {
                int take = Math.Min(q[w], need);
                q[w] -= take;
                need -= take;
            }
        }

        return true;
    }

    private void Cancel()
    {
        if (Selected == null) return;
        Order o = Selected.Order;
        if (!Context.Dialogs.Confirm("Отмена заказа", $"Отменить заказ {o.Number} ({o.Customer}, {Fmt.Money(o.Total)})? Оплата вернётся покупателю.", "Отменить заказ"))
        {
            return;
        }

        // Уже собранный заказ: возвращаем вещи на склад (в Минск)
        if (o.StatusTime("assembled") != null)
        {
            foreach (OrderItem item in o.Items) Context.StockOf(item.Product).Quantities[0] += item.Quantity;
        }

        o.Status = "cancelled";
        o.History.Add(new StatusChange { Status = "cancelled", At = DateTime.Now });
        Context.Log("Заказ отменён", $"{o.Number} · {o.Customer}");
        Selected.Refresh();
        Notify($"Заказ {o.Number} отменён.");
        ApplyFilter();
        RefreshDetails();
    }

    private void MarkPaid()
    {
        if (Selected == null) return;
        Selected.Order.Paid = true;
        Context.Log("Оплата получена", $"{Selected.Order.Number} · {Fmt.Money(Selected.Order.Total)}");
        Selected.Refresh();
        Notify($"Заказ {Selected.Order.Number} отмечен оплаченным.");
        RefreshDetails();
    }

    private void AssignCourier()
    {
        if (Selected == null || CourierEmail == null) return;
        Selected.Order.Courier = CourierEmail;
        Context.Log("Курьер назначен", $"{Selected.Order.Number} → {CourierText}");
        Notify($"Курьер назначен: {Couriers.First(c => c.Key == CourierEmail).Title}.");
        RefreshDetails();
    }
}
