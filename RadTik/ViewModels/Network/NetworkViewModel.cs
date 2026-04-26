using Microsoft.AspNetCore.Http;
using RadTik.Models;
using System.ComponentModel.DataAnnotations;

namespace RadTik.ViewModels.Network
{
    public class NetworkViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشبكة مطلوب")]
        [Display(Name = "اسم الشبكة")]
        [StringLength(100, ErrorMessage = "اسم الشبكة يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = null!;

        [Display(Name = "المحافظات")]
        [StringLength(500, ErrorMessage = "المحافظات يجب أن لا تتجاوز 500 حرف")]
        [DataType(DataType.MultilineText)]
        public string? Governorates { get; set; }

        [Display(Name = "شعار الشبكة")]
        [DataType(DataType.Upload)]
        public IFormFile? LogoFile { get; set; }

        public string? LogoPath { get; set; }

        [Required]
        [Display(Name = "حالة الشبكة")]
        public NetworkStatus Status { get; set; } = NetworkStatus.Active;

        [Display(Name = "ملاحظات")]
        [DataType(DataType.MultilineText)]
        [StringLength(1000, ErrorMessage = "الملاحظات يجب أن لا تتجاوز 1000 حرف")]
        public string? Notes { get; set; }
    }
}

