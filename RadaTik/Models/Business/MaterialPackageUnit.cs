using System.ComponentModel.DataAnnotations;

namespace RadaTik.Models.Business;

/// <summary>وحدة الشراء — تُحوَّل دائماً إلى «قطعة» في المستودع.</summary>
public enum MaterialPackageUnit
{
    [Display(Name = "قطعة")]
    Piece = 0,

    [Display(Name = "علبة")]
    Box = 1,

    [Display(Name = "كرتونة")]
    Carton = 2,

    [Display(Name = "ربطة")]
    Bundle = 3
}
