using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NetworkStatus
    {
        [Display(Name = "نشط")]
        Active,
        [Display(Name = "معطل")]
        Inactive,
        [Display(Name = "قيد الإنشاء")]
        UnderConstruction
    }

    public class Network
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // لدعم (شركة/شبكات فرعية): الشبكة يمكن أن تكون رئيسية (Company/Main) أو فرعية تابعة لها
        [Display(Name = "معرف الشبكة الرئيسية")]
        public int? ParentNetworkId { get; set; }

        [ForeignKey(nameof(ParentNetworkId))]
        [Display(Name = "الشبكة الرئيسية")]
        public virtual Network? ParentNetwork { get; set; }

        [Required(ErrorMessage = "اسم الشبكة مطلوب")]
        [Display(Name = "اسم الشبكة")]
        [StringLength(100, ErrorMessage = "اسم الشبكة يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = null!;

        [Display(Name = "المحافظات")]
        [StringLength(500, ErrorMessage = "المحافظات يجب أن لا تتجاوز 500 حرف")]
        [DataType(DataType.MultilineText)]
        public string? Governorates { get; set; }

        [Display(Name = "شعار الشبكة")]
        [StringLength(500, ErrorMessage = "مسار الشعار يجب أن لا يتجاوز 500 حرف")]
        public string? LogoPath { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreationDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "حالة الشبكة")]
        public NetworkStatus Status { get; set; } = NetworkStatus.Active;

        [Display(Name = "ملاحظات")]
        [DataType(DataType.MultilineText)]
        [StringLength(1000, ErrorMessage = "الملاحظات يجب أن لا تتجاوز 1000 حرف")]
        public string? Notes { get; set; }

        [Display(Name = "رصيد الشبكة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        [Display(Name = "معرف مدير الشركة")]
        [StringLength(450)]
        public string? ManagerUserId { get; set; }

        [ForeignKey(nameof(ManagerUserId))]
        [Display(Name = "مدير الشركة")]
        public virtual ApplicationUser? ManagerUser { get; set; }

        // Navigation Properties
        [Display(Name = "الشبكات الفرعية")]
        public virtual ICollection<Network> ChildNetworks { get; set; } = [];

        public virtual ICollection<ApplicationUser> Users { get; set; } = [];
        public virtual ICollection<MikroTikServer> MikroTikServers { get; set; } = [];
        public virtual ICollection<Sector> Sectors { get; set; } = [];
        public virtual ICollection<Receiver> Receivers { get; set; } = [];
        public virtual ICollection<Client> Clients { get; set; } = [];
        public virtual ICollection<Profile> Profiles { get; set; } = [];

        [NotMapped]
        [Display(Name = "شبكة رئيسية")]
        public bool IsMainNetwork => ParentNetworkId == null;
    }
}
