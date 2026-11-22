using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace AmfibiaStudio.Models;

/// <summary>Строка заказа.</summary>
public class OrderItem
{
    public string Product { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    [JsonIgnore]
    public decimal Total => Price * Quantity;
}

/// <summary>Запись истории статусов заказа.</summary>
public class StatusChange
{
    public string Status { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

/// <summary>Заказ (номер и трек-номер в формате сайта).</summary>
public class Order
{
    public string Number { get; set; } = string.Empty;
    public string Tracking { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string Status { get; set; } = "created";
    public string Delivery { get; set; } = string.Empty;
    public string Payment { get; set; } = string.Empty;
    public bool Paid { get; set; }
    public string? Promo { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DeliveryPrice { get; set; }
    public decimal Total { get; set; }
    public string? Courier { get; set; }
    public List<StatusChange> History { get; set; } = new();

    [JsonIgnore]
    public int ItemsCount => Items.Sum(i => i.Quantity);

    /// <summary>Когда заказ получил указанный статус (null — ещё не получал).</summary>
    public DateTime? StatusTime(string status) =>
        History.LastOrDefault(h => h.Status == status)?.At;
}
