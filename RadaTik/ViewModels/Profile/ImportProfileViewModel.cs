using System.Collections.Generic;

namespace RadaTik.ViewModels.Profile
{
    // نموذج لاستيراد بروفايل من MikroTik
    public class ImportProfileViewModel
    {
        public int MikroTikServerId { get; set; }
        public List<string> SelectedProfileIds { get; set; } = new();
        public bool ImportAsInactive { get; set; }
        public bool SetDefaultPrice { get; set; } = true;
        public decimal DefaultPrice { get; set; } = 100;
    }
}

