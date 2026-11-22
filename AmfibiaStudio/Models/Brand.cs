using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AmfibiaStudio.Models;

/// <summary>Цвета и шрифт бренда — те же, что у заглушек на сайте.</summary>
public class BrandPalette
{
    public string Bg { get; set; } = "#111112";
    public string Bg2 { get; set; } = "#1D1D20";
    public string Fg { get; set; } = "#EDE6DA";
    public string Accent { get; set; } = "#D9707F";
    public string Font { get; set; } = "serif";
    public string Frame { get; set; } = "cross";
}

/// <summary>Коллекция бренда (сезон и год).</summary>
public class BrandCollection
{
    public string Name { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsNew { get; set; }
    public string Description { get; set; } = string.Empty;

    public string SeasonName => Season == "SS" ? "Весна–лето" : "Осень–зима";
}

/// <summary>Креативный директор бренда.</summary>
public class CreativeDirector
{
    public string Name { get; set; } = string.Empty;
    public int From { get; set; }
    public int? To { get; set; }
    public string Description { get; set; } = string.Empty;
    public string KeyWorks { get; set; } = string.Empty;

    public string Period => To == null ? $"с {From} года" : $"{From}–{To}";
}

/// <summary>Событие в истории бренда.</summary>
public class TimelineEvent
{
    public int Year { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>Модный дом.</summary>
public class Brand
{
    private static readonly Dictionary<string, string> Countries = new()
    {
        ["FR"] = "Франция", ["IT"] = "Италия", ["US"] = "США", ["GB"] = "Великобритания",
        ["ES"] = "Испания", ["PT"] = "Португалия", ["CH"] = "Швейцария", ["GE"] = "Грузия", ["JP"] = "Япония"
    };

    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public int Founded { get; set; }
    public string Founder { get; set; } = string.Empty;
    public string Accent { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BrandPalette Palette { get; set; } = new();
    public List<BrandCollection> Collections { get; set; } = new();
    public List<CreativeDirector> Directors { get; set; } = new();
    public List<TimelineEvent> Timeline { get; set; } = new();

    [JsonIgnore]
    public string CountryName => Countries.TryGetValue(Country, out string? name) ? name : Country;
}
