using System.Globalization;

namespace CMS.Infrastructure.Reconciliation;

public record ParsedBankLine(DateOnly ValueDate, string Description, string? Reference, decimal AmountInr);

/// <summary>
/// Lenient CSV parser for bank statements. Accepts a header row and infers columns by name — the
/// supported shape is <c>ValueDate / Description / Reference / Debit / Credit</c> (any case, spaces
/// tolerated). Alternative column names: <c>Date</c>, <c>Narration</c>, <c>Ref</c>, <c>Withdrawal</c>,
/// <c>Deposit</c>. Amounts may contain commas and currency symbols.
/// </summary>
public class BankStatementParser
{
    public List<ParsedBankLine> Parse(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText)) throw new FormatException("Empty CSV");

        var raw = csvText.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
        if (raw.Count < 2) throw new FormatException("CSV must have a header row and at least one data row.");

        var header = SplitCsvRow(raw[0]).Select(h => h.Trim()).ToList();
        int FindCol(params string[] names)
        {
            foreach (var n in names)
            {
                var idx = header.FindIndex(h => h.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }
            return -1;
        }

        var cDate   = FindCol("ValueDate", "Value Date", "Date", "Txn Date", "Transaction Date");
        var cDesc   = FindCol("Description", "Narration", "Particulars");
        var cRef    = FindCol("Reference", "Ref", "Ref No", "Cheque Number", "ChqNo");
        var cDebit  = FindCol("Debit", "Withdrawal", "Dr", "Withdrawal Amt");
        var cCredit = FindCol("Credit", "Deposit", "Cr", "Deposit Amt");
        var cAmount = FindCol("Amount");  // signed amount column — alternative to Debit/Credit

        if (cDate < 0 || cDesc < 0 || (cCredit < 0 && cAmount < 0 && cDebit < 0))
            throw new FormatException("CSV header must contain Date, Description, and either Credit/Debit or a signed Amount column.");

        var rows = new List<ParsedBankLine>();
        for (int i = 1; i < raw.Count; i++)
        {
            var cells = SplitCsvRow(raw[i]);
            if (cells.Count == 0) continue;
            string Cell(int idx) => idx >= 0 && idx < cells.Count ? cells[idx].Trim() : string.Empty;

            if (!DateOnly.TryParse(Cell(cDate), CultureInfo.InvariantCulture, out var d) &&
                !DateOnly.TryParseExact(Cell(cDate), new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                throw new FormatException($"Row {i}: unparseable date '{Cell(cDate)}'.");

            decimal amount;
            if (cAmount >= 0)
                amount = ParseAmount(Cell(cAmount));
            else
            {
                var credit = cCredit >= 0 ? ParseAmount(Cell(cCredit)) : 0m;
                var debit  = cDebit  >= 0 ? ParseAmount(Cell(cDebit))  : 0m;
                amount = credit - debit; // positive = money in
            }
            if (amount == 0m) continue;

            rows.Add(new ParsedBankLine(d, Cell(cDesc), cRef >= 0 ? Cell(cRef) : null, amount));
        }

        if (rows.Count == 0) throw new FormatException("CSV contained no usable rows.");
        return rows;
    }

    private static decimal ParseAmount(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        var cleaned = s.Replace(",", "").Replace("₹", "").Replace("INR", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("(", "-").Replace(")", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    /// <summary>Minimal CSV row splitter: respects doubled quotes and quoted commas; no backslash escapes.</summary>
    private static List<string> SplitCsvRow(string line)
    {
        var cells = new List<string>();
        var buf = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { buf.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes) { cells.Add(buf.ToString()); buf.Clear(); }
            else buf.Append(ch);
        }
        cells.Add(buf.ToString());
        return cells;
    }
}
