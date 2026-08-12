namespace RadaTik.ViewModels.CompanyAdmin
{
    public sealed class NetworkFeaturesIndexViewModel
    {
        public int SelectedNetworkId { get; set; }
        public string SelectedNetworkName { get; set; } = "";

        public int EffectiveCompanyNetworkId { get; set; }
        public string EffectiveCompanyNetworkName { get; set; } = "";

        /// <summary>
        /// True when DB table exists and we can persist enabled/disabled states.
        /// </summary>
        public bool CanManageFeatures { get; set; } = true;

        public List<NetworkFeatureItemViewModel> Features { get; set; } = [];
    }

    public sealed class NetworkFeatureItemViewModel
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        public bool IsEnabled { get; set; } = true;
        public bool DefaultEnabled { get; set; } = true;
    }
}

