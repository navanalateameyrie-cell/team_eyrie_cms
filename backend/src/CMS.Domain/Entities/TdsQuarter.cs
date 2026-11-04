namespace CMS.Domain.Entities;

/// <summary>Indian FY quarter: Q1 Apr-Jun · Q2 Jul-Sep · Q3 Oct-Dec · Q4 Jan-Mar.</summary>
public enum TdsQuarter
{
    Q1 = 1,
    Q2 = 2,
    Q3 = 3,
    Q4 = 4,
}

public static class TdsQuarterExtensions
{
    public static TdsQuarter QuarterFor(DateOnly d) => d.Month switch
    {
        >= 4 and <= 6  => TdsQuarter.Q1,
        >= 7 and <= 9  => TdsQuarter.Q2,
        >= 10 and <= 12 => TdsQuarter.Q3,
        _               => TdsQuarter.Q4,
    };

    /// <summary>Indian FY label (<c>YYYY-YY</c>) that contains the given date (Apr-start).</summary>
    public static string FinancialYearFor(DateOnly d)
    {
        var startYear = d.Month >= 4 ? d.Year : d.Year - 1;
        var endYear = (startYear + 1) % 100;
        return $"{startYear:0000}-{endYear:00}";
    }
}
