namespace RadaTik.ViewModels.Sector;

public sealed class RadioEngineeringStudyViewModel
{
    public string Title { get; set; } = "دراسة هندسية للتحكم الراديوي";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public int TotalSectors { get; set; }
    public int ActiveSectors { get; set; }
    public int ReadySectors { get; set; }
    public int MissingIpSectors { get; set; }
    public int MissingServerSectors { get; set; }
    public int InactiveServersLinkedSectors { get; set; }

    public List<StudyStatItem> ServerProfiles { get; set; } = [];
    public List<StudyStatItem> SectorFamilies { get; set; } = [];
    public List<StudyScenario> Scenarios { get; set; } = [];
    public List<StudyPhase> Phases { get; set; } = [];
    public List<string> RiskControls { get; set; } = [];
    public List<string> PerformanceGuidelines { get; set; } = [];
}

public sealed class StudyStatItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? Note { get; set; }
}

public sealed class StudyScenario
{
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Preconditions { get; set; } = string.Empty;
    public string ExecutionFlow { get; set; } = string.Empty;
    public string SuccessKpi { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "متوسط";
}

public sealed class StudyPhase
{
    public string Name { get; set; } = string.Empty;
    public string TimeEstimate { get; set; } = string.Empty;
    public List<string> Tasks { get; set; } = [];
    public string Output { get; set; } = string.Empty;
}
