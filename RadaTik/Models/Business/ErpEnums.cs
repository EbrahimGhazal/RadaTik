namespace RadaTik.Models.Business;

public enum CompanyEmployeeTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum CompanyEmployeeTaskPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum EmployeeRewardPenaltyType
{
    Reward = 1,
    Penalty = 2
}

public enum EmployeeRewardPenaltyStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    AppliedToPayroll = 4
}

public enum ChartOfAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}
