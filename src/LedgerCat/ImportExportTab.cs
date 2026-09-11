using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace LedgerCat;

public static class FlowExt
{
    public static void Add(this FlowLayoutPanel p, Control c) => p.Controls.Add(c);
}

public class ImportExportTab : UserControl
{
    public event Action? DataChanged;

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    class Backup
    {
        public string exported = "";
        public List<PropRow> properties = new();
        public List<TxnRow> transactions = new();
        public List<ReqRow> requests = new();
    }

    class PropRow
    {
        public long id;
        public string name = "", unit = "", tenant = "", lease_end = "";
        public decimal rent;
        public int due_day = 1;
    }

    class TxnRow
    {
        public string date = "", kind = "expense", category = "", note = "";
        public long? property_id;
        public decimal amount;
    }

    class ReqRow
    {
        public string created = "", kind = "maintenance", description = "";
        public long? property_id;
        public bool done;
    }

    readonly Label dbLabel = new()
    {
        AutoSize = true,
        Tag = "muted",
        Margin = new Padding(0, 2, 0, 8),
    };

    public ImportExportTab()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16),
        };

        flow.Add(Head("CSV — bring your old spreadsheets in, take your data out any time"));
        flow.Add(Btn("Import transactions CSV", 260, (_, _) => ImportCsv("transactions")));
        flow.Add(Btn("Import properties CSV", 260, (_, _) => ImportCsv("properties")));
        flow.Add(Btn("Import requests CSV", 260, (_, _) => ImportCsv("requests")));
        flow.Add(Btn("Export transactions CSV", 260, (_, _) => ExportCsv("transactions")));
        flow.Add(Btn("Export properties CSV", 260, (_, _) => ExportCsv("properties")));
        flow.Add(Btn("Export requests CSV", 260, (_, _) => ExportCsv("requests")));
        flow.Add(Spacer());

        flow.Add(Head("Full backup (JSON) — everything in one file"));
        flow.Add(Btn("Export full backup (JSON)", 260, (_, _) => ExportJson()));
        flow.Add(Btn("Restore from backup (JSON)", 260, (_, _) => ImportJson()));
        flow.Add(Spacer());

        flow.Add(Head("Your data file"));
        dbLabel.Text = "Data lives in one SQLite file you can copy and back up like a photo:\n" + Db.DbPath;
        flow.Add(dbLabel);
        flow.Add(Btn("Open data folder", 260, (_, _) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Db.DbPath + "\""); }
            catch { MessageBox.Show("Data folder: " + Db.DataDir, "LedgerCat"); }
        }));

        Controls.Add(flow);
    }

    static Label Head(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Tag = "head",
        Font = Theme.SubHeadFont,
        ForeColor = Theme.Accent,
        Margin = new Padding(0, 8, 0, 8),
        MaximumSize = new Size(760, 0),
    };

    static Button Btn(string text, int width, EventHandler onClick) => new()
    {
        Text = text,
        Width = width,
        Height = 34,
        Margin = new Padding(0, 2, 0, 2),
    };

    static Label Spacer() => new() { Height = 10, Width = 10, Margin = new Padding(0) };

    // ---------- CSV ----------

    static readonly Dictionary<string, (string filter, string[] header, string filePrefix)> CsvKinds = new()
    {
        ["transactions"] = ("Transactions CSV|*.csv",
            new[] { "date", "property", "kind", "category", "amount", "note" }, "transactions"),
        ["properties"] = ("Properties CSV|*.csv",
            new[] { "name", "unit", "tenant", "rent", "due_day", "lease_end" }, "properties"),
        ["requests"] = ("Requests CSV|*.csv",
            new[] { "created", "property", "kind", "description", "status" }, "requests"),
    };

    void ImportCsv(string kindKey)
    {
        using var dlg = new OpenFileDialog { Filter = CsvKinds[kindKey].filter, Title = "Import " + kindKey };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var rows = Csv.Parse(File.ReadAllText(dlg.FileName));
            if (rows.Count < 2)
            {
                MessageBox.Show("That file has a header but no data rows.", "Import",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var header = rows[0];
            int ok = 0, skipped = 0;
            var props = Db.ListProps();

            foreach (var r in rows.Skip(1))
            {
                bool added = kindKey switch
                {
                    "transactions" => ImportTxnRow(header, r, ref props),
                    "properties" => ImportPropRow(header, r, ref props),
                    "requests" => ImportReqRow(header, r, ref props),
                    _ => false,
                };
                if (added) ok++; else skipped++;
            }

            DataChanged?.Invoke();
            MessageBox.Show($"Imported {ok} row(s)" + (skipped > 0 ? $", skipped {skipped} row(s) with missing or bad values." : "."),
                "Import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not import that file:\n" + ex.Message, "Import failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    long? ResolveProperty(string raw, ref List<Property> props)
    {
        raw = raw.Trim();
        if (raw.Length == 0) return null;
        var p = props.FirstOrDefault(x =>
            x.Label.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (p != null) return p.Id;

        // create on the fly; "Name / Unit" splits
        var np = new Property();
        var parts = raw.Split(" / ", 2);
        np.Name = parts[0].Trim();
        if (parts.Length > 1) np.Unit = parts[1].Trim();
        np.Id = Db.SaveProp(np);
        props.Add(np);
        return np.Id;
    }

    static decimal? ParseAmountCell(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().Replace("$", "").Replace(",", "").TrimStart('+', '−', '-');
        if (decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0)
            return v;
        return null;
    }

    bool ImportTxnRow(string[] header, string[] r, ref List<Property> props)
    {
        int cDate = Csv.FindCol(header, "date");
        int cProp = Csv.FindCol(header, "property");
        int cKind = Csv.FindCol(header, "kind");
        int cCat = Csv.FindCol(header, "category");
        int cAmt = Csv.FindCol(header, "amount");
        int cNote = Csv.FindCol(header, "note");
        if (cDate < 0 || cAmt < 0) return false;

        string date = Cell(r, cDate).Trim();
        if (!Ui.ParseDate(date, out var d)) return false;
        var amountOpt = ParseAmountCell(Cell(r, cAmt));
        if (amountOpt is not { } amount) return false;

        string kindRaw = Cell(r, cKind).Trim().ToLowerInvariant();
        string kind = kindRaw.StartsWith("rent") || kindRaw.StartsWith("in") ? "rent" : "expense";

        Db.SaveTxn(new Txn
        {
            Date = d.ToString("yyyy-MM-dd"),
            PropertyId = cProp >= 0 ? ResolveProperty(Cell(r, cProp), ref props) : null,
            Kind = kind,
            Category = cCat >= 0 ? Cell(r, cCat).Trim() : "",
            Amount = amount,
            Note = cNote >= 0 ? Cell(r, cNote).Trim() : "",
        });
        return true;
    }

    bool ImportPropRow(string[] header, string[] r, ref List<Property> props)
    {
        int cName = Csv.FindCol(header, "name");
        int cUnit = Csv.FindCol(header, "unit");
        int cTenant = Csv.FindCol(header, "tenant");
        int cRent = Csv.FindCol(header, "rent");
        int cDue = Csv.FindCol(header, "due_day");
        int cLease = Csv.FindCol(header, "lease_end");
        if (cName < 0) return false;

        string name = Cell(r, cName).Trim();
        if (name.Length == 0) return false;

        string unit = cUnit >= 0 ? Cell(r, cUnit).Trim() : "";
        if (props.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        && x.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)))
            return false; // already exists

        var rent = cRent >= 0 ? ParseAmountCell(Cell(r, cRent)) ?? 0m : 0m;
        var lease = cLease >= 0 ? Cell(r, cLease).Trim() : "";
        if (lease.Length > 0 && !Ui.ParseDate(lease, out var ld)) lease = "";
        int due = 1;
        if (cDue >= 0 && int.TryParse(Cell(r, cDue), out var dd) && dd >= 1 && dd <= 28) due = dd;

        var np = new Property
        {
            Name = name,
            Unit = unit,
            Tenant = cTenant >= 0 ? Cell(r, cTenant).Trim() : "",
            Rent = rent,
            DueDay = due,
            LeaseEnd = lease,
        };
        np.Id = Db.SaveProp(np);
        props.Add(np);
        return true;
    }

    bool ImportReqRow(string[] header, string[] r, ref List<Property> props)
    {
        int cDate = Csv.FindCol(header, "created");
        int cProp = Csv.FindCol(header, "property");
        int cKind = Csv.FindCol(header, "kind");
        int cDesc = Csv.FindCol(header, "description");
        int cStatus = Csv.FindCol(header, "status");
        if (cDate < 0 || cDesc < 0) return false;

        if (!Ui.ParseDate(Cell(r, cDate).Trim(), out var d)) return false;
        string desc = Cell(r, cDesc).Trim();
        if (desc.Length == 0) return false;

        string kindRaw = Cell(r, cKind).Trim().ToLowerInvariant();
        string kind = kindRaw.StartsWith("view") ? "viewing" : "maintenance";
        string statusRaw = cStatus >= 0 ? Cell(r, cStatus).Trim().ToLowerInvariant() : "";
        bool done = statusRaw is "done" or "closed" or "complete" or "completed";

        Db.SaveReq(new Req
        {
            Created = d.ToString("yyyy-MM-dd"),
            PropertyId = cProp >= 0 ? ResolveProperty(Cell(r, cProp), ref props) : null,
            Kind = kind,
            Description = desc,
            Done = done,
        });
        return true;
    }

    static string Cell(string[] r, int i) => i >= 0 && i < r.Length ? r[i] : "";

    void ExportCsv(string kindKey)
    {
        var (filter, _, prefix) = CsvKinds[kindKey];
        using var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = $"{prefix}-{DateTime.Today:yyyy-MM-dd}.csv",
            Title = "Export " + kindKey,
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var sb = new System.Text.StringBuilder();
        var props = Db.ListProps().ToDictionary(p => p.Id, p => p.Label);

        switch (kindKey)
        {
            case "transactions":
                sb.AppendLine("date,property,kind,category,amount,note");
                foreach (var t in Db.ListTxns())
                    sb.AppendLine(Csv.Row(t.Date, t.PropLabel, t.Kind, t.Category, t.Amount.ToString("0.##", CultureInfo.InvariantCulture), t.Note));
                break;
            case "properties":
                sb.AppendLine("name,unit,tenant,rent,due_day,lease_end");
                foreach (var p in Db.ListProps())
                    sb.AppendLine(Csv.Row(p.Name, p.Unit, p.Tenant, p.Rent.ToString("0.##", CultureInfo.InvariantCulture), p.DueDay, p.LeaseEnd));
                break;
            case "requests":
                sb.AppendLine("created,property,kind,description,status");
                foreach (var q in Db.ListReqs())
                    sb.AppendLine(Csv.Row(q.Created, q.PropLabel, q.Kind, q.Description, q.Done ? "done" : "open"));
                break;
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageBox.Show("Exported to:\n" + dlg.FileName, "Export complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------- JSON backup ----------

    void ExportJson()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "LedgerCat backup (JSON)|*.json",
            FileName = $"ledgercat-backup-{DateTime.Today:yyyy-MM-dd}.json",
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var backup = new Backup { exported = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        foreach (var p in Db.ListProps())
            backup.properties.Add(new PropRow
            {
                id = p.Id,
                name = p.Name, unit = p.Unit, tenant = p.Tenant,
                rent = p.Rent, due_day = p.DueDay, lease_end = p.LeaseEnd,
            });
        foreach (var t in Db.ListTxns())
            backup.transactions.Add(new TxnRow
            {
                date = t.Date, property_id = t.PropertyId, kind = t.Kind,
                category = t.Category, amount = t.Amount, note = t.Note,
            });
        foreach (var q in Db.ListReqs())
            backup.requests.Add(new ReqRow
            {
                created = q.Created, property_id = q.PropertyId, kind = q.Kind,
                description = q.Description, done = q.Done,
            });

        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(backup, JsonOpts));
        MessageBox.Show("Backup saved to:\n" + dlg.FileName, "Backup complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void ImportJson()
    {
        using var dlg = new OpenFileDialog { Filter = "LedgerCat backup (JSON)|*.json" };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var backup = JsonSerializer.Deserialize<Backup>(File.ReadAllText(dlg.FileName), JsonOpts);
            if (backup == null)
            {
                MessageBox.Show("That file doesn't look like a LedgerCat backup.", "Restore",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(
                    "Restoring a backup REPLACES everything currently in LedgerCat.\nContinue?",
                    "Restore backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Db.WipeAll();
            var idMap = new Dictionary<long, long>();
            foreach (var p in backup.properties)
            {
                var np = new Property
                {
                    Name = p.name, Unit = p.unit, Tenant = p.tenant,
                    Rent = p.rent, DueDay = p.due_day, LeaseEnd = p.lease_end,
                };
                np.Id = Db.SaveProp(np);
                idMap[p.id] = np.Id;
            }
            foreach (var t in backup.transactions)
            {
                long? pid = null;
                if (t.property_id.HasValue && idMap.TryGetValue(t.property_id.Value, out var m1)) pid = m1;
                Db.SaveTxn(new Txn
                {
                    Date = t.date, PropertyId = pid, Kind = t.kind,
                    Category = t.category, Amount = t.amount, Note = t.note,
                });
            }
            foreach (var q in backup.requests)
            {
                long? pid = null;
                if (q.property_id.HasValue && idMap.TryGetValue(q.property_id.Value, out var m2)) pid = m2;
                Db.SaveReq(new Req
                {
                    Created = q.created, PropertyId = pid, Kind = q.kind,
                    Description = q.description, Done = q.done,
                });
            }

            DataChanged?.Invoke();
            MessageBox.Show($"Restored {backup.properties.Count} propert(ies), {backup.transactions.Count} transaction(s), {backup.requests.Count} request(s).",
                "Restore complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not restore that backup:\n" + ex.Message, "Restore failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
