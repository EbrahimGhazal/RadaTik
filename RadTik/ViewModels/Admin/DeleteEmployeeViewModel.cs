using System.ComponentModel.DataAnnotations;

namespace RadTik.ViewModels.Admin
{
    public class DeleteEmployeeViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }

        public string? ReturnUrl { get; set; }
    }
}

