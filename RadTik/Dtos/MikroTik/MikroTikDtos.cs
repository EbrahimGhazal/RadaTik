using System.Collections.Generic;

namespace RadTik.Dtos.MikroTik
{
    // نموذج لبروفايل MikroTik
    public class MikroTikProfileInfo
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? LocalAddress { get; set; }
        public string? RemoteAddress { get; set; }
        public string? RateLimit { get; set; }
        public bool OnlyOne { get; set; }
        public string? Service { get; set; }
        public bool IsDisabled { get; set; }
        public bool ExistsInDatabase { get; set; }
        public int? DatabaseProfileId { get; set; }
        public string? DatabaseProfileName { get; set; }
    }

    // نموذج لنتائج المزامنة
    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> AddedProfiles { get; set; } = new();
        public List<string> UpdatedProfiles { get; set; } = new();
        public List<string> FailedProfiles { get; set; } = new();
    }
}

