using System;
using System.Collections.Generic;
using System.Linq;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Строка склада: остатки товара на трёх складах сайта.</summary>
public class StockRowViewModel : ObservableObject
{
    public StockRowViewModel(Product product, StockRow stock)
    {
        Product = product;
        Stock = stock;
    }

    public Product Product { get; }
    public StockRow Stock { get; }
    public string Id => Product.Id;
    public string Name => Product.Name;
    public string Brand => Product.Brand;
    public int Minsk => Stock.Quantities[0];
    public int Moscow => Stock.Quantities[1];
    public int Almaty => Stock.Quantities[2];
    public int Total => Stock.Quantities.Sum();
    public bool IsLow => !Product.Preowned && Total <= WarehouseViewModel.LowStock;
    public string State => Total == 0 ? "нет в наличии" : IsLow ? "заканчивается" : Product.Preowned ? "ресейл, 1 шт." : "в наличии";

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// ViewModel склада: остатки по складам Минск / Москва / Алматы, приход товара и перемещение
/// между складами. Количество проверяется при вводе.
/// </summary>
public class WarehouseViewModel : PageViewModel
{
    public const int LowStock = 2;

    private readonly List<StockRowViewModel> allRows;
    private IReadOnlyList<StockRowViewModel> items = Array.Empty<StockRowViewModel>();
    private StockRowViewModel? selected;
    private string search = string.Empty;
    private bool onlyLow;
    private int fromIndex, toIndex = 1, receiveIndex;
    private string quantityText = "1", receiveText = "5";

    public WarehouseViewModel(StudioContext context) : base(context)
    {
        Warehouses = context.Data.Warehouses.Select(w => w.City).ToList();
        allRows = context.State.Products.Select(p => new StockRowViewModel(p, context.StockOf(p.Id))).ToList();
        TransferCommand = new RelayCommand(Transfer, () => Selected != null && !HasErrors);
        ReceiveCommand = new RelayCommand(Receive, () => Selected != null && !HasErrors);
        ApplyFilter();
        Selected = Items.FirstOrDefault(r => r.IsLow) ?? Items.FirstOrDefault();
        Validate();
    }

    public override string Title => "Склад";
    public override string Subtitle => string.Join(" · ", Context.Data.Warehouses.Select(w => $"{w.City}: {Fmt.Number(allRows.Sum(r => r.Stock.Quantities[w.Id - 1]))} шт."));

    public IReadOnlyList<string> Warehouses { get; }

    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyFilter(); } }
    public bool OnlyLow { get => onlyLow; set { if (SetProperty(ref onlyLow, value)) ApplyFilter(); } }
    public IReadOnlyList<StockRowViewModel> Items { get => items; private set => SetProperty(ref items, value); }
    public string CountText => $"{Fmt.Plural(Items.Count, "товар", "товара", "товаров")} · заканчиваются: {allRows.Count(r => r.IsLow)}";

    public StockRowViewModel? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value))
            {
                Validate();
                TransferCommand.RaiseCanExecuteChanged();
                ReceiveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int FromIndex { get => fromIndex; set { if (SetProperty(ref fromIndex, value)) Validate(); } }
    public int ToIndex { get => toIndex; set { if (SetProperty(ref toIndex, value)) Validate(); } }
    public int ReceiveIndex { get => receiveIndex; set => SetProperty(ref receiveIndex, value); }
    public string QuantityText { get => quantityText; set { if (SetProperty(ref quantityText, value)) Validate(); } }
    public string ReceiveText { get => receiveText; set { if (SetProperty(ref receiveText, value)) Validate(); } }

    public RelayCommand TransferCommand { get; }
    public RelayCommand ReceiveCommand { get; }

    private void ApplyFilter()
    {
        IEnumerable<StockRowViewModel> q = allRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Id.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.Brand.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (onlyLow) q = q.Where(r => r.IsLow);
        Items = q.OrderBy(r => r.Total).ThenBy(r => r.Name).ToList();
        OnPropertyChanged(nameof(CountText));
    }

    private void Validate()
    {
        int available = Selected?.Stock.Quantities[Math.Clamp(fromIndex, 0, 2)] ?? 0;
        string? transferError = !int.TryParse(quantityText, out int qty) || qty <= 0 ? "Количество — целое число больше нуля."
            : fromIndex == toIndex ? "Склады отправки и получения совпадают."
            : qty > available ? $"На складе {Warehouses[Math.Clamp(fromIndex, 0, 2)]} только {available} шт."
            : null;
        SetError(transferError, nameof(QuantityText));
        SetError(!int.TryParse(receiveText, out int r) || r <= 0 || r > 500 ? "Приход — от 1 до 500 шт." : null, nameof(ReceiveText));
        TransferCommand?.RaiseCanExecuteChanged();
        ReceiveCommand?.RaiseCanExecuteChanged();
    }

    private void Transfer()
    {
        if (Selected == null || !int.TryParse(quantityText, out int qty)) return;
        Validate();
        if (FirstError(nameof(QuantityText)) is string error)
        {
            Notify(error, isError: true);
            return;
        }

        Selected.Stock.Quantities[fromIndex] -= qty;
        Selected.Stock.Quantities[toIndex] += qty;
        Context.Log("Перемещение", $"{Selected.Id}: {qty} шт. {Warehouses[fromIndex]} → {Warehouses[toIndex]}");
        Done($"Перемещено {qty} шт.: {Warehouses[fromIndex]} → {Warehouses[toIndex]}.");
    }

    private void Receive()
    {
        if (Selected == null || !int.TryParse(receiveText, out int qty) || FirstError(nameof(ReceiveText)) != null) return;
        if (Selected.Product.Preowned)
        {
            Notify("Вещь с ресейла существует в одном экземпляре — приход невозможен.", isError: true);
            return;
        }

        Selected.Stock.Quantities[receiveIndex] += qty;
        Context.Log("Приход товара", $"{Selected.Id}: +{qty} шт. на склад {Warehouses[receiveIndex]}");
        Done($"Принято {qty} шт. на склад {Warehouses[receiveIndex]}.");
    }

    private void Done(string message)
    {
        Selected?.Refresh();
        Validate();
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(CountText));
        Notify(message);
    }
}

/// <summary>Строка модерации отзыва.</summary>
public class ReviewRow : ObservableObject
{
    private readonly StudioContext context;

    public ReviewRow(Review review, StudioContext context)
    {
        Review = review;
        this.context = context;
        Product = context.FindProduct(review.Product);
    }

    public Review Review { get; }
    public Product? Product { get; }
    public string ProductName => Product?.Name ?? Review.Product;
    public string Brand => Product?.Brand ?? "";
    public string Author => Review.Author;
    public string Stars => new string('★', Review.Rating) + new string('☆', 5 - Review.Rating);
    public string Text => Review.Text;
    public string Date => Fmt.Date(Review.Created);
    public string Status => Review.Status switch { "pending" => "на модерации", "approved" => "опубликован", _ => "отклонён" };
    public string StatusKind => Review.Status;
    public bool IsPending => Review.Status == "pending";

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>ViewModel модерации отзывов: опубликовать или отклонить, распределение оценок.</summary>
public class ReviewsViewModel : PageViewModel
{
    public static readonly string[] Tabs = { "На модерации", "Опубликованные", "Отклонённые", "Все" };

    private readonly List<ReviewRow> allRows;
    private IReadOnlyList<ReviewRow> items = Array.Empty<ReviewRow>();
    private int tab;

    public ReviewsViewModel(StudioContext context) : base(context)
    {
        allRows = context.State.Reviews.OrderByDescending(r => r.Created).Select(r => new ReviewRow(r, context)).ToList();
        ApproveCommand = new RelayCommand(p => SetStatus(p as ReviewRow, "approved"), p => p is ReviewRow { IsPending: true } && CanModerate);
        RejectCommand = new RelayCommand(p => SetStatus(p as ReviewRow, "rejected"), p => p is ReviewRow { IsPending: true } && CanModerate);
        ApplyFilter();
    }

    public override string Title => "Отзывы";
    public override string Subtitle => $"Средняя оценка {AverageText} · отзыв появляется на сайте после проверки модератором";

    public bool CanModerate => Context.IsRole("owner", "moderator", "support");
    public IReadOnlyList<string> TabNames => Tabs;
    public int Tab { get => tab; set { if (SetProperty(ref tab, value)) ApplyFilter(); } }
    public IReadOnlyList<ReviewRow> Items { get => items; private set => SetProperty(ref items, value); }
    public string CountText => Fmt.Plural(Items.Count, "отзыв", "отзыва", "отзывов");
    public int PendingCount => allRows.Count(r => r.IsPending);

    public string AverageText
    {
        get
        {
            var approved = allRows.Where(r => r.Review.Status == "approved").ToList();
            return approved.Count == 0 ? "—" : approved.Average(r => r.Review.Rating).ToString("0.00", Fmt.Ru);
        }
    }

    /// <summary>Сколько опубликованных отзывов с каждой оценкой (5…1).</summary>
    public IReadOnlyList<BarItem> Distribution
    {
        get
        {
            var approved = allRows.Where(r => r.Review.Status == "approved").ToList();
            int max = Math.Max(1, Enumerable.Range(1, 5).Max(s => approved.Count(r => r.Review.Rating == s)));
            return Enumerable.Range(1, 5).Reverse().Select(s =>
            {
                int n = approved.Count(r => r.Review.Rating == s);
                return new BarItem($"{s} ★", n, n.ToString(), n / (double)max);
            }).ToList();
        }
    }

    public RelayCommand ApproveCommand { get; }
    public RelayCommand RejectCommand { get; }

    private void ApplyFilter()
    {
        Items = tab switch
        {
            0 => allRows.Where(r => r.IsPending).ToList(),
            1 => allRows.Where(r => r.Review.Status == "approved").ToList(),
            2 => allRows.Where(r => r.Review.Status == "rejected").ToList(),
            _ => allRows
        };
        OnPropertyChanged(nameof(CountText));
    }

    public void SetStatus(ReviewRow? row, string status)
    {
        if (row == null || !row.IsPending || !CanModerate) return;
        row.Review.Status = status;
        Context.Log(status == "approved" ? "Отзыв опубликован" : "Отзыв отклонён", $"{row.Author} · {row.ProductName} · {row.Stars}");
        row.Refresh();
        Notify(status == "approved" ? $"Отзыв {row.Author} опубликован." : $"Отзыв {row.Author} отклонён.");
        ApplyFilter();
        foreach (string name in new[] { nameof(PendingCount), nameof(Distribution), nameof(AverageText), nameof(Subtitle) }) OnPropertyChanged(name);
    }
}
