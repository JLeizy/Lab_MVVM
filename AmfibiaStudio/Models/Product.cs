using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AmfibiaStudio.Models;

/// <summary>Одна фигура силуэта вещи (путь SVG в системе координат 200×200).</summary>
public class SilhouetteShape
{
    public string D { get; set; } = string.Empty;
    public bool Fill { get; set; }
    public double Width { get; set; } = 1;
    public bool Dash { get; set; }
    public bool EvenOdd { get; set; }
}

/// <summary>Товар каталога AMFIBIA (настоящая вещь из каталога сайта).</summary>
public class Product
{
    /// <summary>Артикул, как на сайте: BAL-SNK-0001.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Код модели: у разных цветов одной модели он общий.</summary>
    public string Model { get; set; } = string.Empty;

    public string BrandSlug { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string GroupSlug { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string SizeType { get; set; } = string.Empty;
    public string ColorCode { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string InkHex { get; set; } = "#1B1B1B";
    public string Material { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string GenderCode { get; set; } = string.Empty;

    /// <summary>Цена в белорусских рублях.</summary>
    public decimal Price { get; set; }

    /// <summary>Старая цена (если товар со скидкой).</summary>
    public decimal? OldPrice { get; set; }

    /// <summary>Вещь с ресейла: подлинная, с экспертизой, в единственном экземпляре.</summary>
    public bool Preowned { get; set; }

    public bool IsNew { get; set; }

    /// <summary>Культовая модель дома.</summary>
    public bool Iconic { get; set; }

    /// <summary>Просмотры на сайте — для сортировки «Популярные».</summary>
    public int Views { get; set; }

    public string Description { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string Care { get; set; } = string.Empty;
    public int Silhouette { get; set; }
    public List<string> Sizes { get; set; } = new();

    [JsonIgnore]
    public Brand? BrandInfo { get; set; }

    [JsonIgnore]
    public IReadOnlyList<SilhouetteShape> Shapes { get; set; } = Array.Empty<SilhouetteShape>();

    /// <summary>Скидка в процентах (0 — без скидки).</summary>
    [JsonIgnore]
    public int DiscountPercent => OldPrice is decimal old && old > Price
        ? (int)Math.Round((1 - Price / old) * 100)
        : 0;

    [JsonIgnore]
    public string Condition => Preowned ? "Ресейл, один экземпляр" : "Новая вещь";
}
