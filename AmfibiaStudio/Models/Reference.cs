using System;
using System.Collections.Generic;

namespace AmfibiaStudio.Models;

/// <summary>Способ доставки.</summary>
public class DeliveryMethod
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DaysMin { get; set; }
    public int DaysMax { get; set; }

    /// <summary>true — пункт выдачи, false — курьер до двери.</summary>
    public bool Pickup { get; set; }
}

/// <summary>Способ оплаты.</summary>
public class PaymentMethod
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Online { get; set; }
}

/// <summary>Промокод со сроком действия.</summary>
public class PromoCode
{
    public string Code { get; set; } = string.Empty;
    public int Percent { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? UsageLimit { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Статус заказа: название и пояснение, как на странице отслеживания сайта.</summary>
public class OrderStatusInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Help { get; set; } = string.Empty;
}

/// <summary>Отзыв покупателя.</summary>
public class Review
{
    public string Product { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>approved — опубликован, pending — на модерации, rejected — отклонён.</summary>
    public string Status { get; set; } = "pending";
    public DateTime Created { get; set; }
}

/// <summary>Курсы валют к белорусскому рублю (как в таблице currency_rates сайта).</summary>
public class CurrencyRates
{
    public decimal BYN { get; set; } = 1;
    public decimal USD { get; set; }
    public decimal EUR { get; set; }
    public DateTime Updated { get; set; }
}

/// <summary>Склад (в магазине не используется, есть в данных сайта).</summary>
public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>Остатки товара по складам.</summary>
public class StockRow
{
    public string Product { get; set; } = string.Empty;
    public List<int> Quantities { get; set; } = new();
}
