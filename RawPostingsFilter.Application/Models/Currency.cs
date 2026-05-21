using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Валюта оплаты, указанной в вакансии или заказе.")]
public enum Currency
{
    [Description("Российский рубль.")]
    Rub,

    [Description("Доллар США.")]
    Usd,

    [Description("Евро.")]
    Eur
}
