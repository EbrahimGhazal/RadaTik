namespace RadaTik.Models.Business;

/// <summary>نوع الدوام.</summary>
public enum PayrollEmploymentType
{
    FullTime = 1,
    PartTime = 2
}

/// <summary>حركة مالية على راتب الموظف خلال الشهر.</summary>
public enum PayrollTransactionType
{
    /// <summary>سحب جزء من الراتب خلال الشهر.</summary>
    MidMonthWithdrawal = 1,

    /// <summary>سلفة على الراتب (تُخصم عند التسوية).</summary>
    Advance = 2,

    /// <summary>مكافأة إضافية.</summary>
    Bonus = 3,

    /// <summary>حسم إضافي.</summary>
    Deduction = 4
}

/// <summary>طريقة زيادة الراتب.</summary>
public enum PayrollSalaryAdjustmentType
{
    FixedAmount = 1,
    Percentage = 2
}
