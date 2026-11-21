using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AmfibiaStudio.Services;

/// <summary>Окно подтверждения («Удалить товар?»). ViewModel вызывает интерфейс — в тестах подставляется заглушка.</summary>
public interface IDialogService
{
    bool Confirm(string title, string message, string confirmText);
}

/// <summary>Смена темы оформления (тёмная AMFIBIA, светлая, графит).</summary>
public interface IThemeService
{
    void Apply(string theme);
}

/// <summary>Курсы валют к белорусскому рублю.</summary>
public record RatesResult(decimal Usd, decimal Eur, DateTime Date);

/// <summary>Источник курсов (НБРБ).</summary>
public interface IRatesProvider
{
    Task<RatesResult> LoadAsync();
}

/// <summary>
/// Официальные курсы Национального банка Республики Беларусь:
/// https://api.nbrb.by/exrates/rates?periodicity=0
/// </summary>
public class NbrbRatesProvider : IRatesProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private class NbrbRate
    {
        [JsonPropertyName("Cur_Abbreviation")] public string Abbreviation { get; set; } = string.Empty;
        [JsonPropertyName("Cur_Scale")] public int Scale { get; set; }
        [JsonPropertyName("Cur_OfficialRate")] public decimal? OfficialRate { get; set; }
        [JsonPropertyName("Date")] public DateTime Date { get; set; }
    }

    public async Task<RatesResult> LoadAsync()
    {
        string json = await Http.GetStringAsync("https://api.nbrb.by/exrates/rates?periodicity=0");
        List<NbrbRate> rates = JsonSerializer.Deserialize<List<NbrbRate>>(json) ?? new List<NbrbRate>();

        decimal Rate(string code)
        {
            NbrbRate r = rates.FirstOrDefault(x => x.Abbreviation == code)
                         ?? throw new InvalidOperationException($"в ответе НБРБ нет курса {code}");
            return Math.Round((r.OfficialRate ?? 0) / Math.Max(1, r.Scale), 4);
        }

        return new RatesResult(Rate("USD"), Rate("EUR"), rates.FirstOrDefault()?.Date ?? DateTime.Today);
    }
}

/// <summary>Форматирование чисел и дат по-русски.</summary>
public static class Fmt
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Money(decimal value) =>
        value.ToString(value == Math.Truncate(value) ? "N0" : "N2", Ru) + " BYN";

    public static string Number(decimal value) => value.ToString("N0", Ru);

    public static string Date(DateTime value) => value.ToString("d MMMM yyyy", Ru);

    public static string DateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Ru);

    public static string Plural(int n, string one, string few, string many)
    {
        int mod100 = Math.Abs(n) % 100, mod10 = mod100 % 10;
        string word = mod100 is >= 11 and <= 14 ? many : mod10 == 1 ? one : mod10 is >= 2 and <= 4 ? few : many;
        return $"{n} {word}";
    }
}
