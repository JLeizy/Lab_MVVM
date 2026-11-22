using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Вариант выбора в выпадающем списке.</summary>
public record Option(string Key, string Title);

/// <summary>Строка таблицы товаров (обёртка над моделью Product).</summary>
public class ProductRow : ObservableObject
{
    private readonly StudioContext context;

    public ProductRow(Product product, StudioContext context)
    {
        Product = product;
        this.context = context;
    }

    public Product Product { get; }
    public string Id => Product.Id;
    public string Name => Product.Name;
    public string Brand => Product.Brand;
    public string Category => Product.Category;
    public decimal Price => Product.Price;
    public string PriceText => Fmt.Money(Product.Price);
    public string OldPriceText => Product.OldPrice is decimal old && old > Product.Price ? Fmt.Money(old) : "";
    public int Stock => context.StockOf(Product.Id).Quantities.Sum();
    public bool IsLowStock => !Product.Preowned && Stock <= WarehouseViewModel.LowStock;

    public string Flags => string.Join(" · ", new[]
    {
        Product.Preowned ? "ресейл" : null, Product.IsNew ? "новинка" : null,
        Product.Iconic ? "культовая" : null, Product.DiscountPercent > 0 ? $"−{Product.DiscountPercent}%" : null
    }.Where(f => f != null));

    /// <summary>После сохранения — обновить все колонки строки.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>
/// ViewModel раздела «Товары»: таблица с поиском и фильтрами, карточка редактирования,
/// создание, копирование и удаление. Таблица (DataGrid) сама сортируется по клику на заголовок.
/// </summary>
public class ProductsViewModel : PageViewModel
{
    public const string All = "";

    private List<ProductRow> allRows;
    private IReadOnlyList<ProductRow> items = Array.Empty<ProductRow>();
    private ProductRow? selected;
    private ProductEditorViewModel? editor;
    private string search = string.Empty;
    private string brand = All;
    private string group = All;
    private bool onlyPreowned, onlySale, onlyLowStock;

    public ProductsViewModel(StudioContext context) : base(context)
    {
        allRows = context.State.Products.Select(p => new ProductRow(p, context)).ToList();
        Brands = new[] { new Option(All, "Все бренды") }
            .Concat(context.Data.Brands.OrderBy(b => b.Name).Select(b => new Option(b.Slug, b.Name))).ToList();
        Groups = new[] { new Option(All, "Все разделы") }
            .Concat(context.State.Products.GroupBy(p => (p.GroupSlug, p.Group)).Select(g => new Option(g.Key.GroupSlug, g.Key.Group))).ToList();

        NewCommand = new RelayCommand(CreateNew, () => CanEdit);
        DuplicateCommand = new RelayCommand(Duplicate, () => CanEdit && Selected != null);
        DeleteCommand = new RelayCommand(Delete, () => CanEdit && Selected != null);
        ResetFiltersCommand = new RelayCommand(() =>
        {
            search = string.Empty; brand = All; group = All; onlyPreowned = onlySale = onlyLowStock = false;
            OnPropertyChanged(string.Empty);
            ApplyFilter();
        });
        ApplyFilter();
    }

    public override string Title => "Товары";
    public override string Subtitle => $"{Fmt.Plural(allRows.Count, "товар", "товара", "товаров")} в каталоге · редактирование, цены, новинки";

    /// <summary>Редактировать могут владелец, модератор и продавец.</summary>
    public bool CanEdit => Context.IsRole("owner", "moderator", "seller");

    public IReadOnlyList<Option> Brands { get; }
    public IReadOnlyList<Option> Groups { get; }

    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyFilter(); } }
    public string Brand { get => brand; set { if (SetProperty(ref brand, value ?? All)) ApplyFilter(); } }
    public string Group { get => group; set { if (SetProperty(ref group, value ?? All)) ApplyFilter(); } }
    public bool OnlyPreowned { get => onlyPreowned; set { if (SetProperty(ref onlyPreowned, value)) ApplyFilter(); } }
    public bool OnlySale { get => onlySale; set { if (SetProperty(ref onlySale, value)) ApplyFilter(); } }
    public bool OnlyLowStock { get => onlyLowStock; set { if (SetProperty(ref onlyLowStock, value)) ApplyFilter(); } }

    public IReadOnlyList<ProductRow> Items { get => items; private set => SetProperty(ref items, value); }

    public string CountText => $"Показано {Items.Count} из {allRows.Count}";

    public ProductRow? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value))
            {
                Editor = value == null ? null : new ProductEditorViewModel(Context, value.Product, isNew: false, OnSaved);
                DuplicateCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Карточка редактирования выбранного товара (своя ViewModel с проверкой полей).</summary>
    public ProductEditorViewModel? Editor { get => editor; private set => SetProperty(ref editor, value); }

    public RelayCommand NewCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }

    private void ApplyFilter()
    {
        IEnumerable<ProductRow> q = allRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Id.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.Brand.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Product.NameEn.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (brand != All) q = q.Where(r => r.Product.BrandSlug == brand);
        if (group != All) q = q.Where(r => r.Product.GroupSlug == group);
        if (onlyPreowned) q = q.Where(r => r.Product.Preowned);
        if (onlySale) q = q.Where(r => r.Product.DiscountPercent > 0);
        if (onlyLowStock) q = q.Where(r => r.IsLowStock);

        Items = q.ToList();
        OnPropertyChanged(nameof(CountText));
    }

    private void OnSaved(Product product, bool wasNew)
    {
        if (wasNew)
        {
            var row = new ProductRow(product, Context);
            allRows.Insert(0, row);
            ApplyFilter();
            Selected = Items.FirstOrDefault(r => r.Product == product);
            OnPropertyChanged(nameof(Subtitle));
        }
        else
        {
            allRows.FirstOrDefault(r => r.Product == product)?.Refresh();
        }
    }

    private void CreateNew()
    {
        Selected = null;
        Brand firstBrand = Context.Data.Brands.OrderBy(b => b.Name).First();
        Product template = Context.State.Products.First(p => p.BrandSlug == firstBrand.Slug);
        var draft = new Product
        {
            BrandSlug = template.BrandSlug, Brand = template.Brand, CategorySlug = template.CategorySlug, Category = template.Category,
            GroupSlug = template.GroupSlug, Group = template.Group, SizeType = template.SizeType, Sizes = template.Sizes.ToList(),
            ColorCode = "black", Color = "Чёрный", ColorHex = "#161616", InkHex = "#EDEAE4", Material = template.Material,
            Gender = template.Gender, GenderCode = template.GenderCode, Silhouette = template.Silhouette, IsNew = true
        };
        Context.Data.Link(new[] { draft });
        Editor = new ProductEditorViewModel(Context, draft, isNew: true, OnSaved);
    }

    private void Duplicate()
    {
        if (Selected == null) return;
        Product p = Selected.Product;
        var copy = new Product
        {
            BrandSlug = p.BrandSlug, Brand = p.Brand, Name = p.Name + " (копия)", NameEn = p.NameEn, CategorySlug = p.CategorySlug,
            Category = p.Category, GroupSlug = p.GroupSlug, Group = p.Group, SizeType = p.SizeType, Sizes = p.Sizes.ToList(),
            ColorCode = p.ColorCode, Color = p.Color, ColorHex = p.ColorHex, InkHex = p.InkHex, Material = p.Material, Gender = p.Gender,
            GenderCode = p.GenderCode, Price = p.Price, OldPrice = p.OldPrice, Preowned = false, IsNew = true, Description = p.Description,
            Composition = p.Composition, Care = p.Care, Silhouette = p.Silhouette, Model = p.Model
        };
        Context.Data.Link(new[] { copy });
        Selected = null;
        Editor = new ProductEditorViewModel(Context, copy, isNew: true, OnSaved);
    }

    private void Delete()
    {
        if (Selected == null) return;
        Product p = Selected.Product;
        if (Context.State.Orders.Any(o => o.Status is not ("delivered" or "cancelled" or "returned") && o.Items.Any(i => i.Product == p.Id)))
        {
            Notify("Товар есть в незавершённых заказах — удалить нельзя.", isError: true);
            return;
        }

        if (!Context.Dialogs.Confirm("Удалить товар", $"Удалить «{p.Name}» ({p.Id}) из каталога?", "Удалить"))
        {
            return;
        }

        Context.State.Products.Remove(p);
        Context.State.Stock.RemoveAll(s => s.Product == p.Id);
        allRows.RemoveAll(r => r.Product == p);
        Selected = null;
        Context.Log("Товар удалён", $"{p.Id} · {p.Name}");
        ApplyFilter();
        OnPropertyChanged(nameof(Subtitle));
        Notify($"Товар {p.Id} удалён.");
    }
}

/// <summary>
/// Карточка редактирования товара. Поля проверяются при вводе (INotifyDataErrorInfo),
/// «Сохранить» доступна, только если есть изменения и нет ошибок.
/// </summary>
public class ProductEditorViewModel : ValidatableObject
{
    private readonly StudioContext context;
    private readonly Product product;
    private readonly Action<Product, bool> saved;
    private string name = string.Empty, nameEn = string.Empty, priceText = string.Empty, oldPriceText = string.Empty, description = string.Empty;
    private string brandSlug, categorySlug, colorCode;
    private bool isNewArrival, iconic, preowned, isDirty;

    public ProductEditorViewModel(StudioContext context, Product product, bool isNew, Action<Product, bool> saved)
    {
        this.context = context;
        this.product = product;
        this.saved = saved;
        IsNew = isNew;
        brandSlug = product.BrandSlug;
        categorySlug = product.CategorySlug;
        colorCode = product.ColorCode;

        BrandOptions = context.Data.Brands.OrderBy(b => b.Name).Select(b => new Option(b.Slug, b.Name)).ToList();
        CategoryOptions = context.State.Products.GroupBy(p => (p.CategorySlug, p.Category)).OrderBy(g => g.Key.Category)
            .Select(g => new Option(g.Key.CategorySlug, g.Key.Category)).ToList();
        ColorOptions = context.State.Products.GroupBy(p => (p.ColorCode, p.Color)).OrderBy(g => g.Key.Color)
            .Select(g => new Option(g.Key.ColorCode, g.Key.Color)).ToList();

        SaveCommand = new RelayCommand(Save, () => CanEdit && IsDirty && !HasErrors);
        RevertCommand = new RelayCommand(Load, () => IsDirty);
        Load();
        if (isNew) Validate();
    }

    public Product Product => product;
    public bool IsNew { get; }
    public string Header => IsNew ? "Новый товар" : product.Id;
    public bool CanEdit => context.IsRole("owner", "moderator", "seller");
    public bool CanChangeBrand => IsNew;

    public IReadOnlyList<Option> BrandOptions { get; }
    public IReadOnlyList<Option> CategoryOptions { get; }
    public IReadOnlyList<Option> ColorOptions { get; }

    public string Name { get => name; set => Edit(ref name, value); }
    public string NameEn { get => nameEn; set => Edit(ref nameEn, value); }
    public string PriceText { get => priceText; set => Edit(ref priceText, value); }
    public string OldPriceText { get => oldPriceText; set => Edit(ref oldPriceText, value); }
    public string Description { get => description; set => Edit(ref description, value); }
    // Выпадающий список при смене DataContext может на миг передать null — такие значения пропускаются
    public string BrandSlug { get => brandSlug; set { if (value != null) Edit(ref brandSlug, value); } }
    public string CategorySlug { get => categorySlug; set { if (value != null) Edit(ref categorySlug, value); } }
    public string ColorCode { get => colorCode; set { if (value != null) Edit(ref colorCode, value); } }
    public bool IsNewArrival { get => isNewArrival; set => Edit(ref isNewArrival, value); }
    public bool Iconic { get => iconic; set => Edit(ref iconic, value); }
    public bool Preowned { get => preowned; set => Edit(ref preowned, value); }

    public bool IsDirty
    {
        get => isDirty;
        private set
        {
            if (SetProperty(ref isDirty, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                RevertCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }

    public static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text.Replace(" ", "").Replace(" ", "").Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private void Edit<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (SetProperty(ref field, value, property))
        {
            IsDirty = true;
            Validate();
        }
    }

    /// <summary>Правила проверки полей товара.</summary>
    private void Validate()
    {
        string n = name.Trim();
        SetError(n.Length < 3 ? "Название — от 3 символов." : n.Length > 80 ? "Название — не длиннее 80 символов." : null, nameof(Name));

        bool priceOk = TryMoney(priceText, out decimal price);
        SetError(!priceOk ? "Цена — число, например 3550." : price < 50 || price > 500_000 ? "Цена от 50 до 500 000 BYN." : null, nameof(PriceText));

        string? oldError = null;
        if (oldPriceText.Trim().Length > 0)
        {
            if (!TryMoney(oldPriceText, out decimal old)) oldError = "Старая цена — число или пусто.";
            else if (priceOk && old <= price) oldError = "Старая цена должна быть больше новой (это скидка).";
        }

        SetError(oldError, nameof(OldPriceText));
        SetError(description.Trim().Length < 20 ? "Описание — от 20 символов." : null, nameof(Description));
        SetError(preowned && isNewArrival ? "Вещь с ресейла не может быть новинкой." : null, nameof(Preowned));
    }

    protected override void OnErrorsUpdated() => SaveCommand?.RaiseCanExecuteChanged();

    private void Load()
    {
        name = product.Name;
        nameEn = product.NameEn;
        priceText = product.Price == 0 ? string.Empty : product.Price.ToString("0.##", CultureInfo.InvariantCulture);
        oldPriceText = product.OldPrice?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        description = product.Description;
        brandSlug = product.BrandSlug;
        categorySlug = product.CategorySlug;
        colorCode = product.ColorCode;
        isNewArrival = product.IsNew;
        iconic = product.Iconic;
        preowned = product.Preowned;
        ClearErrors();
        IsDirty = false;
        OnPropertyChanged(string.Empty);
    }

    private void Save()
    {
        Validate();
        if (HasErrors) return;

        TryMoney(priceText, out decimal price);
        decimal? old = TryMoney(oldPriceText, out decimal o) ? o : null;
        string changes = IsNew ? "создан" : product.Price != price ? $"цена {Fmt.Money(product.Price)} → {Fmt.Money(price)}" : "данные изменены";

        Product sameCategory = context.State.Products.FirstOrDefault(p => p.CategorySlug == categorySlug) ?? product;
        Product sameColor = context.State.Products.FirstOrDefault(p => p.ColorCode == colorCode) ?? product;
        Brand brand = context.Data.Brands.First(b => b.Slug == brandSlug);

        product.Name = name.Trim();
        product.NameEn = nameEn.Trim();
        product.Price = price;
        product.OldPrice = old;
        product.Description = description.Trim();
        product.IsNew = isNewArrival;
        product.Iconic = iconic;
        product.Preowned = preowned;
        product.BrandSlug = brand.Slug;
        product.Brand = brand.Name;
        product.CategorySlug = sameCategory.CategorySlug;
        product.Category = sameCategory.Category;
        product.GroupSlug = sameCategory.GroupSlug;
        product.Group = sameCategory.Group;
        product.SizeType = sameCategory.SizeType;
        product.Silhouette = sameCategory.Silhouette;
        if (product.Sizes.Count == 0 || IsNew) product.Sizes = sameCategory.Sizes.ToList();
        product.ColorCode = sameColor.ColorCode;
        product.Color = sameColor.Color;
        product.ColorHex = sameColor.ColorHex;
        product.InkHex = sameColor.InkHex;

        if (IsNew)
        {
            string code = sameCategory.Id.Split('-')[1];
            int next = context.State.Products.Where(p => p.Id.StartsWith(brand.Code + "-")).Select(p => int.TryParse(p.Id.Split('-')[^1], out int n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
            product.Id = $"{brand.Code}-{code}-{next:D4}";
            product.Model = $"{brand.Code}-M9{next:D2}";
            context.State.Products.Insert(0, product);
            context.StockOf(product.Id);
        }

        context.Data.Link(new[] { product });
        context.Log(IsNew ? "Товар создан" : "Товар изменён", $"{product.Id} · {product.Name} · {changes}");
        bool wasNew = IsNew;
        IsDirty = false;
        context.Notifier.Show(wasNew ? $"Товар {product.Id} создан." : $"Изменения {product.Id} сохранены.");
        saved(product, wasNew);
    }
}
