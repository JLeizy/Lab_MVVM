using System;
using System.Collections.Generic;
using System.Linq;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Плитка бренда в энциклопедии.</summary>
public record BrandCard(Brand Brand, string Name, string Subtitle, string Bg, string Bg2, string Fg, string Accent, string FontKey);

/// <summary>Абзац статьи: заголовок, текст, пункт списка или цитата.</summary>
public record ArticleBlock(string Kind, string Text);

/// <summary>Раздел статьи об истории бренда.</summary>
public record ArticleSection(string Period, string Title, IReadOnlyList<ArticleBlock> Blocks, string? Quote, string? QuoteAuthor);

/// <summary>Показатель бренда (вещи, выручка, оценка…).</summary>
public record BrandStat(string Value, string Label);

/// <summary>
/// ViewModel энциклопедии модных домов: полные статьи об истории брендов с сайта
/// (db/content/histories), хронология, директора, коллекции и показатели магазина по бренду.
/// </summary>
public class BrandsViewModel : PageViewModel
{
    private BrandCard? selected;
    private string search = string.Empty;
    private IReadOnlyList<ArticleSection> allSections = Array.Empty<ArticleSection>();

    public BrandsViewModel(StudioContext context) : base(context)
    {
        Brands = context.Data.Brands.OrderBy(b => b.Name).Select(b => new BrandCard(b, b.Name, $"{b.CountryName} · {b.Founded}",
            b.Palette.Bg, b.Palette.Bg2, b.Palette.Fg, b.Palette.Accent, b.Palette.Font)).ToList();
        Selected = Brands.FirstOrDefault(b => b.Brand.Slug == "chrome-hearts") ?? Brands.FirstOrDefault();
    }

    public override string Title => "Энциклопедия модных домов";
    public override string Subtitle => $"{Fmt.Plural(Context.Data.History.Values.Sum(s => s.Count), "раздел", "раздела", "разделов")} истории из базы сайта AMFIBIA";

    public IReadOnlyList<BrandCard> Brands { get; }

    public BrandCard? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value) && value != null)
            {
                allSections = Context.Data.History.GetValueOrDefault(value.Brand.Slug, new List<HistorySection>())
                    .Select(s => new ArticleSection(s.Period, s.Title, Parse(s.Content), s.Quote, s.QuoteAuthor)).ToList();
                search = string.Empty;
                OnPropertyChanged(string.Empty);
            }
        }
    }

    public string Search
    {
        get => search;
        set
        {
            if (SetProperty(ref search, value))
            {
                OnPropertyChanged(nameof(Sections));
                OnPropertyChanged(nameof(FoundText));
            }
        }
    }

    /// <summary>Разделы статьи; при поиске — только те, где встречается слово.</summary>
    public IReadOnlyList<ArticleSection> Sections => string.IsNullOrWhiteSpace(search)
        ? allSections
        : allSections.Where(s => s.Title.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                                 || s.Blocks.Any(b => b.Text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

    public string FoundText => string.IsNullOrWhiteSpace(search)
        ? Fmt.Plural(allSections.Count, "раздел", "раздела", "разделов")
        : $"Найдено: {Sections.Count} из {allSections.Count}";

    public string Description => Selected?.Brand.Description ?? "";
    public IReadOnlyList<TimelineEvent> Timeline => Selected?.Brand.Timeline ?? new List<TimelineEvent>();
    public IReadOnlyList<CreativeDirector> Directors => Selected?.Brand.Directors ?? new List<CreativeDirector>();
    public IReadOnlyList<BrandCollection> Collections => Selected?.Brand.Collections.OrderByDescending(c => c.Year).ToList() ?? new List<BrandCollection>();

    /// <summary>Показатели магазина по бренду — связь энциклопедии с данными продаж.</summary>
    public IReadOnlyList<BrandStat> Stats
    {
        get
        {
            if (Selected == null) return Array.Empty<BrandStat>();
            string slug = Selected.Brand.Slug;
            var products = Context.State.Products.Where(p => p.BrandSlug == slug).ToList();
            var ids = products.Select(p => p.Id).ToHashSet();
            decimal revenue = Context.State.Orders.Where(o => o.Status is not ("cancelled" or "returned"))
                .SelectMany(o => o.Items).Where(i => ids.Contains(i.Product)).Sum(i => i.Total);
            var ratings = Context.State.Reviews.Where(r => r.Status == "approved" && ids.Contains(r.Product)).Select(r => r.Rating).ToList();
            return new[]
            {
                new BrandStat(products.Count.ToString(), "вещей в каталоге"),
                new BrandStat(Fmt.Money(revenue), "выручка магазина"),
                new BrandStat(ratings.Count == 0 ? "—" : ratings.Average().ToString("0.0", Fmt.Ru), "средняя оценка"),
                new BrandStat(Selected.Brand.Founded.ToString(), "год основания")
            };
        }
    }

    /// <summary>Разбор текста раздела (разметка сайта: «### заголовок», «- пункт», «> цитата», **жирный**).</summary>
    public static IReadOnlyList<ArticleBlock> Parse(string content)
    {
        var blocks = new List<ArticleBlock>();
        foreach (string raw in content.Replace("\r", "").Split("\n\n"))
        {
            string text = raw.Trim().Replace("**", "");
            if (text.Length == 0) continue;
            if (text.StartsWith("#")) blocks.Add(new ArticleBlock("h", text.TrimStart('#', ' ')));
            else if (text.StartsWith("> ")) blocks.Add(new ArticleBlock("quote", text[2..]));
            else if (text.StartsWith("- ") || text.StartsWith("* "))
            {
                foreach (string line in text.Split('\n')) blocks.Add(new ArticleBlock("li", line.TrimStart('-', '*', ' ')));
            }
            else blocks.Add(new ArticleBlock("p", text.Replace('\n', ' ')));
        }

        return blocks;
    }
}

/// <summary>ViewModel журнала действий сотрудников (как audit_log на сайте).</summary>
public class AuditViewModel : PageViewModel
{
    private string search = string.Empty;
    private string user = string.Empty;

    public AuditViewModel(StudioContext context) : base(context)
    {
        Users = new[] { new Option("", "Все сотрудники") }
            .Concat(context.State.Audit.Select(a => a.User).Distinct().OrderBy(u => u).Select(u => new Option(u, u))).ToList();
    }

    public override string Title => "Журнал действий";
    public override string Subtitle => $"{Fmt.Plural(Context.State.Audit.Count, "запись", "записи", "записей")} · кто, что и когда изменил";

    public IReadOnlyList<Option> Users { get; }
    public string Search { get => search; set { if (SetProperty(ref search, value)) OnPropertyChanged(nameof(Items)); } }
    public string User { get => user; set { if (SetProperty(ref user, value ?? "")) OnPropertyChanged(nameof(Items)); } }

    public IReadOnlyList<AuditEntry> Items => Context.State.Audit
        .Where(a => user.Length == 0 || a.User == user)
        .Where(a => string.IsNullOrWhiteSpace(search) || a.Action.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                    || a.Details.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
        .ToList();
}

/// <summary>Тема оформления.</summary>
public record ThemeOption(string Key, string Title, string Description, string Bg, string Fg, string Accent);

/// <summary>ViewModel настроек: тема (меняется сразу, без перезапуска), сброс демо-данных, сведения.</summary>
public class SettingsViewModel : PageViewModel
{
    private readonly Action logout;
    private ThemeOption selectedTheme;

    public SettingsViewModel(StudioContext context, Action logout) : base(context)
    {
        this.logout = logout;
        Themes = new[]
        {
            new ThemeOption("amfibia", "AMFIBIA", "Готическая тёмная тема сайта", "#0B0B0C", "#EDE6DA", "#D9707F"),
            new ThemeOption("light", "Светлая", "Светлая тема сайта: бумага и чернила", "#F7F5F2", "#0B0B0C", "#B23A48"),
            new ThemeOption("graphite", "Графит", "Тёмно-серая тема с холодным акцентом", "#151517", "#FFFFFF", "#7EA6E0")
        };
        selectedTheme = Themes.FirstOrDefault(t => t.Key == context.State.Theme) ?? Themes[0];
        ResetCommand = new RelayCommand(Reset, () => Context.IsRole("owner"));
    }

    public override string Title => "Настройки";
    public override string Subtitle => "Тема оформления и демо-данные";

    public IReadOnlyList<ThemeOption> Themes { get; }

    public ThemeOption SelectedTheme
    {
        get => selectedTheme;
        set
        {
            if (value != null && SetProperty(ref selectedTheme, value))
            {
                Context.Theme.Apply(value.Key);
                Context.State.Theme = value.Key;
                Context.Save();
                Notify($"Тема «{value.Title}» применена.");
            }
        }
    }

    public string DataInfo => $"Данные сайта: {Context.State.Products.Count} товаров, {Context.Data.Brands.Count} брендов, " +
                              $"{Context.State.Orders.Count} заказов, {Context.State.Users.Count} пользователей. " +
                              "Изменения сохраняются в %AppData%\\AMFIBIA\\Lab_MVVM\\studio.json.";

    public RelayCommand ResetCommand { get; }

    private void Reset()
    {
        if (!Context.Dialogs.Confirm("Сброс данных", "Вернуть демо-данные сайта? Все изменения товаров, заказов, склада и промокодов будут удалены.", "Сбросить"))
        {
            return;
        }

        Context.ResetDemo();
        Context.Theme.Apply(Context.State.Theme);
        Notify("Демо-данные восстановлены. Войдите снова.");
        logout();
    }
}
