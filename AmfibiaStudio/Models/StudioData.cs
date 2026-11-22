using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace AmfibiaStudio.Models;

/// <summary>Раздел статьи об истории модного дома (db/content/histories сайта).</summary>
public class HistorySection
{
    public string Period { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Quote { get; set; }
    public string? QuoteAuthor { get; set; }
}

/// <summary>Запись журнала действий сотрудников (как audit_log на сайте).</summary>
public class AuditEntry
{
    public DateTime At { get; set; }
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Изменяемые данные панели: товары, заказы, склад, промокоды, курсы, пользователи, отзывы, журнал.
/// Сохраняются в %AppData%\AMFIBIA\Lab_MVVM\studio.json.
/// </summary>
public class StudioState
{
    public List<Product> Products { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public List<StockRow> Stock { get; set; } = new();
    public List<PromoCode> Promos { get; set; } = new();
    public CurrencyRates Rates { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<AuditEntry> Audit { get; set; } = new();
    public string Theme { get; set; } = "amfibia";
}

/// <summary>Данные сайта AMFIBIA, встроенные в программу (Data/amfibia.json и Data/amfibia-history.json).</summary>
public class StudioData
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public List<Brand> Brands { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<List<SilhouetteShape>> Silhouettes { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<DeliveryMethod> Delivery { get; set; } = new();
    public List<PaymentMethod> Payments { get; set; } = new();
    public List<PromoCode> Promos { get; set; } = new();
    public List<OrderStatusInfo> Statuses { get; set; } = new();
    public List<string> Flow { get; set; } = new();
    public List<Warehouse> Warehouses { get; set; } = new();
    public CurrencyRates Rates { get; set; } = new();
    public List<StockRow> Stock { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public decimal FreeDeliveryFrom { get; set; } = 3000;

    /// <summary>Статьи об истории брендов: slug → разделы.</summary>
    public Dictionary<string, List<HistorySection>> History { get; set; } = new();

    public static StudioData LoadEmbedded()
    {
        Assembly assembly = typeof(StudioData).Assembly;
        using Stream main = assembly.GetManifestResourceStream("amfibia.json")
            ?? throw new InvalidOperationException("В программе нет файла данных amfibia.json.");
        StudioData data = JsonSerializer.Deserialize<StudioData>(main, Options) ?? throw new InvalidOperationException("Файл данных пуст.");

        using Stream? history = assembly.GetManifestResourceStream("amfibia-history.json");
        if (history != null)
        {
            data.History = JsonSerializer.Deserialize<Dictionary<string, List<HistorySection>>>(history, Options) ?? new();
        }

        return data;
    }

    /// <summary>Начальное состояние панели — демо-данные сайта.</summary>
    public StudioState CreateInitialState() => new()
    {
        Products = Products,
        Orders = Orders,
        Stock = Stock,
        Promos = Promos,
        Rates = Rates,
        Users = Users,
        Reviews = Reviews,
        Audit = CreateDemoAudit()
    };

    /// <summary>
    /// Журнал для демо-данных: кто из сотрудников провёл заказы по статусам и проверил отзывы
    /// (по ролям, как на сайте: подтверждает поддержка, собирает склад, доставляет курьер).
    /// </summary>
    private List<AuditEntry> CreateDemoAudit()
    {
        string NameOf(string role, int index)
        {
            List<User> staff = Users.Where(u => u.Role == role && !u.Blocked).ToList();
            return staff.Count == 0 ? "Система" : staff[index % staff.Count].FullName;
        }

        var audit = new List<AuditEntry>
        {
            new() { At = Rates.Updated, User = NameOf("owner", 0), Action = "Курсы валют", Details = $"USD {Rates.USD:0.0000} · EUR {Rates.EUR:0.0000} (НБРБ)" }
        };

        int i = 0;
        foreach (Order order in Orders)
        {
            foreach (StatusChange change in order.History.Where(h => h.Status != "created"))
            {
                string role = change.Status switch
                {
                    "assembling" or "assembled" or "handed_to_delivery" => "warehouse",
                    "in_transit" or "arrived" or "delivered" => "courier",
                    _ => "support"
                };
                string action = change.Status == "cancelled" ? "Заказ отменён" : "Статус заказа";
                audit.Add(new AuditEntry { At = change.At, User = NameOf(role, i++), Action = action, Details = $"{order.Number} → {StatusName(change.Status)}" });
            }
        }

        foreach (Review review in Reviews.Where(r => r.Status != "pending"))
        {
            audit.Add(new AuditEntry
            {
                At = review.Created.AddHours(5), User = NameOf("moderator", 0),
                Action = review.Status == "approved" ? "Отзыв опубликован" : "Отзыв отклонён",
                Details = $"{review.Author} · {Products.FirstOrDefault(p => p.Id == review.Product)?.Name ?? review.Product} · {new string('★', review.Rating)}"
            });
        }

        return audit.OrderByDescending(a => a.At).ToList();
    }

    /// <summary>Связывает товары с брендами и силуэтами (для рисунков товаров).</summary>
    public void Link(IEnumerable<Product> products)
    {
        Dictionary<string, Brand> brands = Brands.ToDictionary(b => b.Slug);
        foreach (Product p in products)
        {
            p.BrandInfo = brands.GetValueOrDefault(p.BrandSlug);
            if (p.Silhouette >= 0 && p.Silhouette < Silhouettes.Count)
            {
                p.Shapes = Silhouettes[p.Silhouette];
            }
        }
    }

    public string StatusName(string code) => Statuses.FirstOrDefault(s => s.Code == code)?.Name ?? code;

    public string StatusHelp(string code) => Statuses.FirstOrDefault(s => s.Code == code)?.Help ?? string.Empty;
}
