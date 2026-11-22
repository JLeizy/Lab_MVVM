using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Карточка показателя.</summary>
public record KpiItem(string Title, string Value, string Hint);

/// <summary>Столбец диаграммы. Ratio (0…1) — высота относительно самого большого столбца.</summary>
public record BarItem(string Label, decimal Value, string ValueText, double Ratio);

/// <summary>Доля кольцевой диаграммы.</summary>
public record SegmentItem(string Label, int Count, double Fraction, string Color)
{
    public string PercentText => $"{Fraction * 100:0}%";
}

/// <summary>Самый продаваемый товар.</summary>
public record TopProductItem(Product Product, string Name, string Brand, string Sold, string Revenue);

/// <summary>
/// ViewModel дашборда. Всё считается из данных при смене периода —
/// Вид только рисует свойства (KPI, столбцы, доли) через привязки.
/// </summary>
public class DashboardViewModel : PageViewModel
{
    public static readonly string[] PeriodNames = { "7 дней", "30 дней", "90 дней", "Год" };
    private static readonly int[] PeriodDays = { 7, 30, 90, 365 };

    private static readonly Dictionary<string, string> StatusColors = new()
    {
        ["created"] = "#E5B567", ["confirmed"] = "#D9A441", ["assembling"] = "#7EA6E0", ["assembled"] = "#5C8DD6",
        ["handed_to_delivery"] = "#B58BE0", ["in_transit"] = "#9B6FD6", ["arrived"] = "#4FB3B0",
        ["delivered"] = "#7FC79B", ["cancelled"] = "#8A857D", ["returned"] = "#D9707F"
    };

    private readonly Func<DateTime> clock;
    private int periodIndex = 1;

    public DashboardViewModel(StudioContext context, Action<string>? navigate = null, Func<DateTime>? clock = null) : base(context)
    {
        this.clock = clock ?? (() => DemoNow(context));
        OpenCommand = new RelayCommand(p => { if (p is string key) navigate?.Invoke(key); });
        context.Changed += OnChanged;
        Recalculate();
    }

    public override string Title => "Дашборд";
    public override string Subtitle => $"Сводка магазина AMFIBIA · {Context.UserName}";

    public IReadOnlyList<string> Periods => PeriodNames;

    public int PeriodIndex
    {
        get => periodIndex;
        set
        {
            if (SetProperty(ref periodIndex, Math.Clamp(value, 0, PeriodDays.Length - 1))) Recalculate();
        }
    }

    public IReadOnlyList<KpiItem> Kpis { get; private set; } = Array.Empty<KpiItem>();
    public IReadOnlyList<BarItem> RevenueBars { get; private set; } = Array.Empty<BarItem>();
    public IReadOnlyList<SegmentItem> StatusSegments { get; private set; } = Array.Empty<SegmentItem>();
    public IReadOnlyList<BarItem> TopBrands { get; private set; } = Array.Empty<BarItem>();
    public IReadOnlyList<TopProductItem> TopProducts { get; private set; } = Array.Empty<TopProductItem>();
    public IReadOnlyList<AuditEntry> RecentActions { get; private set; } = Array.Empty<AuditEntry>();
    public string RevenueTitle { get; private set; } = string.Empty;
    public string OrdersTotalText { get; private set; } = string.Empty;

    public RelayCommand OpenCommand { get; }

    public decimal Revenue { get; private set; }
    public int OrdersCount { get; private set; }

    /// <summary>
    /// «Сейчас» для отчёта. Если последние заказы демо-данных давно в прошлом,
    /// отчёт строится на дату последнего заказа, чтобы графики не были пустыми.
    /// </summary>
    private static DateTime DemoNow(StudioContext context)
    {
        DateTime now = DateTime.Now;
        if (context.State.Orders.Count == 0) return now;
        DateTime last = context.State.Orders.Max(o => o.Created);
        return last > now || (now - last).TotalDays > 7 ? last : now;
    }

    private int FlowIndex(string status)
    {
        int index = Context.Data.Flow.IndexOf(status);
        return index < 0 ? 99 : index;
    }

    private static bool Counts(Order o) => o.Status is not ("cancelled" or "returned");

    public void Recalculate()
    {
        DateTime now = clock();
        int days = PeriodDays[periodIndex];
        DateTime from = now.Date.AddDays(-days + 1);
        List<Order> period = Context.State.Orders.Where(o => o.Created >= from && o.Created <= now).ToList();
        List<Order> paid = period.Where(Counts).ToList();

        Revenue = paid.Sum(o => o.Total);
        OrdersCount = period.Count;
        int items = paid.Sum(o => o.Items.Sum(i => i.Quantity));
        int active = Context.State.Orders.Count(o => o.Status is not ("delivered" or "cancelled" or "returned"));
        int pending = Context.State.Reviews.Count(r => r.Status == "pending");
        int low = Context.State.Products.Count(p => !p.Preowned && Context.StockOf(p.Id).Quantities.Sum() <= WarehouseViewModel.LowStock);

        Kpis = new[]
        {
            new KpiItem("Выручка", Fmt.Money(Revenue), $"за {PeriodNames[periodIndex].ToLowerInvariant()}"),
            new KpiItem("Заказы", OrdersCount.ToString(), $"{Fmt.Plural(paid.Count, "оплачен", "оплачено", "оплачено")} без отмен"),
            new KpiItem("Средний чек", paid.Count == 0 ? "—" : Fmt.Money(Math.Round(Revenue / paid.Count)), $"{Fmt.Plural(items, "вещь", "вещи", "вещей")} продано"),
            new KpiItem("В работе", active.ToString(), "заказов ещё не доставлено"),
            new KpiItem("Отзывы", pending.ToString(), "ждут модерации"),
            new KpiItem("Мало на складе", low.ToString(), $"товаров ≤ {WarehouseViewModel.LowStock} шт.")
        };

        RevenueBars = BuildRevenueBars(paid, from, now, days);
        RevenueTitle = days <= 30 ? "Выручка по дням" : days <= 90 ? "Выручка по неделям" : "Выручка по месяцам";

        StatusSegments = period.GroupBy(o => o.Status)
            .OrderBy(g => FlowIndex(g.Key))
            .Select(g => new SegmentItem(Context.Data.StatusName(g.Key), g.Count(), period.Count == 0 ? 0 : g.Count() / (double)period.Count,
                                         StatusColors.GetValueOrDefault(g.Key, "#8A857D")))
            .ToList();
        OrdersTotalText = Fmt.Plural(period.Count, "заказ", "заказа", "заказов");

        var byBrand = paid.SelectMany(o => o.Items).GroupBy(i => i.Brand)
            .Select(g => (Brand: g.Key, Sum: g.Sum(i => i.Total))).OrderByDescending(x => x.Sum).Take(6).ToList();
        decimal maxBrand = byBrand.Count == 0 ? 1 : byBrand.Max(x => x.Sum);
        TopBrands = byBrand.Select(x => new BarItem(x.Brand, x.Sum, Fmt.Money(x.Sum), (double)(x.Sum / maxBrand))).ToList();

        TopProducts = paid.SelectMany(o => o.Items).GroupBy(i => i.Product)
            .Select(g => (Id: g.Key, Qty: g.Sum(i => i.Quantity), Sum: g.Sum(i => i.Total)))
            .OrderByDescending(x => x.Qty).ThenByDescending(x => x.Sum).Take(5)
            .Select(x => (Product: Context.FindProduct(x.Id), x.Qty, x.Sum))
            .Where(x => x.Product != null)
            .Select(x => new TopProductItem(x.Product!, x.Product!.Name, x.Product.Brand, Fmt.Plural(x.Qty, "шт.", "шт.", "шт."), Fmt.Money(x.Sum)))
            .ToList();

        RecentActions = Context.State.Audit.Take(6).ToList();

        foreach (string name in new[] { nameof(Kpis), nameof(RevenueBars), nameof(RevenueTitle), nameof(StatusSegments), nameof(OrdersTotalText),
                                        nameof(TopBrands), nameof(TopProducts), nameof(RecentActions), nameof(Revenue), nameof(OrdersCount) })
        {
            OnPropertyChanged(name);
        }
    }

    private static IReadOnlyList<BarItem> BuildRevenueBars(List<Order> paid, DateTime from, DateTime now, int days)
    {
        var buckets = new List<(string Label, decimal Sum)>();
        CultureInfo ru = Fmt.Ru;
        if (days <= 30)
        {
            for (DateTime d = from; d <= now.Date; d = d.AddDays(1))
            {
                bool label = days <= 7 || (now.Date - d).Days % 5 == 0;
                buckets.Add((label ? d.ToString(days <= 7 ? "ddd" : "dd.MM", ru) : "", paid.Where(o => o.Created.Date == d).Sum(o => o.Total)));
            }
        }
        else if (days <= 90)
        {
            for (DateTime d = from; d <= now.Date; d = d.AddDays(7))
            {
                DateTime end = d.AddDays(7);
                buckets.Add((d.ToString("dd.MM", ru), paid.Where(o => o.Created >= d && o.Created < end).Sum(o => o.Total)));
            }
        }
        else
        {
            var month = new DateTime(from.Year, from.Month, 1);
            for (DateTime d = month; d <= now; d = d.AddMonths(1))
            {
                buckets.Add((d.ToString("MMM", ru).TrimEnd('.'), paid.Where(o => o.Created.Year == d.Year && o.Created.Month == d.Month).Sum(o => o.Total)));
            }
        }

        decimal max = buckets.Count == 0 ? 0 : buckets.Max(b => b.Sum);
        return buckets.Select(b => new BarItem(b.Label, b.Sum, Fmt.Money(b.Sum), max == 0 ? 0 : (double)(b.Sum / max))).ToList();
    }

    private void OnChanged(object? sender, EventArgs e) => Recalculate();

    public override void Dispose()
    {
        Context.Changed -= OnChanged;
        base.Dispose();
    }
}
