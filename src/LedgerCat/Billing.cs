using System;
using System.Collections.Generic;
using System.Linq;

namespace LedgerCat;

// v1.2.8 (StorageCat port, Dad): last-month rent tracking, shared by the Properties
// grid and the record-rent dialog so they can never disagree.
public static class Billing
{
    // Everything the tenant paid that counts as rent in the given month (yyyy-MM).
    public static decimal PaidInMonth(List<Txn> txns, long propId, string monthKey) =>
        txns.Where(t => t.Kind == "rent" && t.PropertyId == propId &&
                        t.Date.Length >= 7 && t.Date[..7] == monthKey)
            .Sum(t => t.Amount);

    // Last month only counts once the lease actually reaches back that far —
    // a fresh move-in doesn't owe last month (Dad, StorageCat v0.1.0 test).
    public static string PrevMonthStatus(Property p, List<Txn> txns, DateTime today)
    {
        if (!p.TrackRent || p.TotalRentDue <= 0) return "";
        var prev = today.AddMonths(-1);
        if (Ui.ParseDate(p.LeaseStart, out var start))
        {
            var prevEnd = new DateOnly(prev.Year, prev.Month, DateTime.DaysInMonth(prev.Year, prev.Month));
            if (start > prevEnd) return ""; // lease started this month or later
        }
        return PaidInMonth(txns, p.Id, prev.ToString("yyyy-MM")) >= p.TotalRentDue ? "Paid" : "DUE";
    }

    // Unpaid balance still owed for last month, or 0.
    public static decimal PrevMonthUnpaid(Property p, List<Txn> txns, DateTime today) =>
        PrevMonthStatus(p, txns, today) == "DUE"
            ? Math.Max(0, p.TotalRentDue - PaidInMonth(txns, p.Id, today.AddMonths(-1).ToString("yyyy-MM")))
            : 0m;
}
