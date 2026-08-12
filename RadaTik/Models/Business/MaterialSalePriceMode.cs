using System.ComponentModel.DataAnnotations;

namespace RadaTik.Models.Business;

public enum MaterialSalePriceMode
{
    [Display(Name = "سعر الجملة")]
    Wholesale = 1,

    [Display(Name = "سعر المفرق")]
    Retail = 2,

    [Display(Name = "سعر مخصص")]
    Custom = 3
}
