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
        public string name = "", unit = "", tenant = "", lease_end = "", lease_start = "";
        public string contact_name = "", contact_phone = "", state_id = "", lease_notes = "";
        public decimal rent;
        public int due_day = 1;
        public bool pets_ok;
        public int pet_count;
        public decimal pet_rent, pet_deposit, security_deposit, late_fee;
        public bool track_rent = true;
    }

    class TxnRow
    {
        public string date = "", kind = "expense", category = "", note = "";
        public long? property_id;
        public decimal amount;
    }

    class ReqRow
    {
        public string created = "", kind = "maintenance", description = "", status = "open";
        public string contact_name = "", contact_phone = "", company = "";
        public string handyman_name = "", handyman_phone = "";
        public string cancel_reason = "", retry_later = "", notes = "";
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
            try
            {
                // v1.2.1 fix: UseShellExecute is required for explorer.exe /select to actually show the file.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + Db.DbPath + "\"",
                    UseShellExecute = true,
                });
            }
            catch
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Db.DataDir, UseShellExecute = true }); }
                catch { MessageBox.Show("Data folder: " + Db.DataDir, "LedgerCat"); }
            }
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

    // v1.2.4 (Dad, MAJOR): the click handler was passed in and never wired up — an event can't be
    // assigned inside an object initializer, so every Import/Export button was dead. Wired now.
    static Button Btn(string text, int width, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = width, Height = 34, Margin = new Padding(0, 2, 0, 2) };
        b.Click += onClick;
        return b;
    }

    static Label Spacer() => new() { Height = 10, Width = 10, Margin = new Padding(0) };

    // ---------- CSV ----------

    static readonly Dictionary<string, (string filter, string[] header, string filePrefix)> CsvKinds = new()
    {
        ["transactions"] = ("Transactions CSV|*.csv",
            new[] { "date", "property", "kind", "category", "amount", "note" }, "transactions"),
        ["properties"] = ("Properties CSV|*.csv",
            new[] { "name", "unit", "tenant", "contact_name", "contact_phone", "state_id", "rent", "pet_rent", "late_fee", "due_day", "security_deposit", "pets_ok", "pet_count", "pet_deposit", "track_rent", "lease_start", "lease_end", "lease_notes" }, "properties"),
        ["requests"] = ("Requests CSV|*.csv",
            new[] { "created", "property", "kind", "description", "status", "contact_name", "contact_phone", "company", "handyman_name", "handyman_phone", "retry_later", "notes", "cancel_reason" }, "requests"),
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
        int cContactName = Csv.FindCol(header, "contact_name");
        int cContactPhone = Csv.FindCol(header, "contact_phone");
        int cStateId = Csv.FindCol(header, "state_id");
        int cRent = Csv.FindCol(header, "rent");
        int cDue = Csv.FindCol(header, "due_day");
        int cLease = Csv.FindCol(header, "lease_end");
        int cLeaseStart = Csv.FindCol(header, "lease_start");
        int cLeaseNotes = Csv.FindCol(header, "lease_notes");
        if (cName < 0) return false;

        string name = Cell(r, cName).Trim();
        if (name.Length == 0) return false;

        string unit = cUnit >= 0 ? Cell(r, cUnit).Trim() : "";
        if (props.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        && x.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)))
            return false; // already exists

        var rent = cRent >= 0 ? ParseAmountCell(Cell(r, cRent)) ?? 0m : 0m;
        var petRent = Csv.FindCol(header, "pet_rent") is { } cPR && cPR >= 0 ? ParseAmountCell(Cell(r, cPR)) ?? 0m : 0m;
        var lateFee = Csv.FindCol(header, "late_fee") is { } cLF && cLF >= 0 ? ParseAmountCell(Cell(r, cLF)) ?? 0m : 0m;
        var secDep = Csv.FindCol(header, "security_deposit") is { } cSD && cSD >= 0 ? ParseAmountCell(Cell(r, cSD)) ?? 0m : 0m;
        var petDep = Csv.FindCol(header, "pet_deposit") is { } cPD && cPD >= 0 ? ParseAmountCell(Cell(r, cPD)) ?? 0m : 0m;
        var lease = cLease >= 0 ? Cell(r, cLease).Trim() : "";
        if (lease.Length > 0 && !Ui.ParseDate(lease, out var ld)) lease = "";
        int due = 1;
        if (cDue >= 0 && int.TryParse(Cell(r, cDue), out var dd) && dd >= 1 && dd <= 28) due = dd;

        bool petsOk = false;
        int petCount = 0;
        var cPets = Csv.FindCol(header, "pets_ok");
        if (cPets >= 0)
        {
            var raw = Cell(r, cPets).Trim().ToLowerInvariant();
            petsOk = raw is "1" or "yes" or "true" or "y";
        }
        var cPetCount = Csv.FindCol(header, "pet_count");
        if (cPetCount >= 0 && int.TryParse(Cell(r, cPetCount), out var pc) && pc > 0) petCount = pc;
        if (petCount > 0) petsOk = true;

        bool trackRent = true;
        var cTrack = Csv.FindCol(header, "track_rent");
        if (cTrack >= 0)
        {
            var raw = Cell(r, cTrack).Trim().ToLowerInvariant();
            if (raw is "0" or "no" or "false" or "n") trackRent = false;
        }

        var np = new Property
        {
            Name = name,
            Unit = unit,
            Tenant = cTenant >= 0 ? Cell(r, cTenant).Trim() : "",
            ContactName = cContactName >= 0 ? Cell(r, cContactName).Trim() : "",
            ContactPhone = cContactPhone >= 0 ? Cell(r, cContactPhone).Trim() : "",
            StateId = cStateId >= 0 ? Cell(r, cStateId).Trim() : "",
            Rent = rent,
            DueDay = due,
            LeaseEnd = lease,
            LeaseStart = cLeaseStart >= 0 ? Cell(r, cLeaseStart).Trim() : "",
            LeaseNotes = cLeaseNotes >= 0 ? Cell(r, cLeaseNotes).Trim() : "",
            PetsOk = petsOk,
            PetCount = petCount,
            PetRent = petRent,
            PetDeposit = petDep,
            SecurityDeposit = secDep,
            TrackRent = trackRent,
            LateFee = lateFee,
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
        int cContactName = Csv.FindCol(header, "contact_name");
        int cContactPhone = Csv.FindCol(header, "contact_phone");
        int cCompany = Csv.FindCol(header, "company");
        int cHandyName = Csv.FindCol(header, "handyman_name");
        int cHandyPhone = Csv.FindCol(header, "handyman_phone");
        int cRetry = Csv.FindCol(header, "retry_later");
        int cNotes = Csv.FindCol(header, "notes");
        int cCancel = Csv.FindCol(header, "cancel_reason");
        if (cDate < 0 || cDesc < 0) return false;

        if (!Ui.ParseDate(Cell(r, cDate).Trim(), out var d)) return false;
        string desc = Cell(r, cDesc).Trim();
        if (desc.Length == 0) return false;

        string kindRaw = Cell(r, cKind).Trim().ToLowerInvariant();
        string kind = kindRaw.StartsWith("view") ? "viewing" : "maintenance";
        string statusRaw = cStatus >= 0 ? Cell(r, cStatus).Trim().ToLowerInvariant() : "";
        string status = statusRaw switch
        {
            "done" or "closed" or "complete" or "completed" => "done",
            "canceled" or "cancelled" => "canceled",
            _ => "open",
        };

        Db.SaveReq(new Req
        {
            Created = d.ToString("yyyy-MM-dd"),
            PropertyId = cProp >= 0 ? ResolveProperty(Cell(r, cProp), ref props) : null,
            Kind = kind,
            Description = desc,
            Status = status,
            ContactName = cContactName >= 0 ? Cell(r, cContactName).Trim() : "",
            ContactPhone = cContactPhone >= 0 ? Cell(r, cContactPhone).Trim() : "",
            Company = cCompany >= 0 ? Cell(r, cCompany).Trim() : "",
            HandymanName = cHandyName >= 0 ? Cell(r, cHandyName).Trim() : "",
            HandymanPhone = cHandyPhone >= 0 ? Cell(r, cHandyPhone).Trim() : "",
            RetryLater = cRetry >= 0 ? Cell(r, cRetry).Trim() : "",
            Notes = cNotes >= 0 ? Cell(r, cNotes).Trim() : "",
            CancelReason = cCancel >= 0 ? Cell(r, cCancel).Trim() : "",
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
                sb.AppendLine("name,unit,tenant,contact_name,contact_phone,state_id,rent,pet_rent,late_fee,due_day,security_deposit,pets_ok,pet_count,pet_deposit,track_rent,lease_start,lease_end,lease_notes");
                foreach (var p in Db.ListProps())
                    sb.AppendLine(Csv.Row(p.Name, p.Unit, p.Tenant, p.ContactName, p.ContactPhone, p.StateId,
                        p.Rent.ToString("0.##", CultureInfo.InvariantCulture),
                        p.PetRent.ToString("0.##", CultureInfo.InvariantCulture),
                        p.LateFee.ToString("0.##", CultureInfo.InvariantCulture),
                        p.DueDay,
                        p.SecurityDeposit.ToString("0.##", CultureInfo.InvariantCulture),
                        p.PetsOk ? "yes" : "no", p.PetCount,
                        p.PetDeposit.ToString("0.##", CultureInfo.InvariantCulture),
                        p.TrackRent ? "yes" : "no",
                        p.LeaseStart, p.LeaseEnd, p.LeaseNotes));
                break;
            case "requests":
                sb.AppendLine("created,property,kind,description,status,contact_name,contact_phone,company,handyman_name,handyman_phone,retry_later,notes,cancel_reason");
                foreach (var q in Db.ListReqs())
                    sb.AppendLine(Csv.Row(q.Created, q.PropLabel, q.Kind, q.Description, q.Status,
                        q.ContactName, q.ContactPhone, q.Company, q.HandymanName, q.HandymanPhone,
                        q.RetryLater, q.Notes, q.CancelReason));
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
                contact_name = p.ContactName, contact_phone = p.ContactPhone,
                state_id = p.StateId, lease_notes = p.LeaseNotes,
                rent = p.Rent, due_day = p.DueDay, lease_end = p.LeaseEnd, lease_start = p.LeaseStart,
                pets_ok = p.PetsOk, pet_count = p.PetCount,
                pet_rent = p.PetRent, pet_deposit = p.PetDeposit,
                security_deposit = p.SecurityDeposit, track_rent = p.TrackRent, late_fee = p.LateFee,
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
                description = q.Description, status = q.Status, done = q.Done,
                contact_name = q.ContactName, contact_phone = q.ContactPhone,
                company = q.Company, handyman_name = q.HandymanName, handyman_phone = q.HandymanPhone,
                cancel_reason = q.CancelReason, retry_later = q.RetryLater, notes = q.Notes,
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
                    ContactName = p.contact_name, ContactPhone = p.contact_phone,
                    StateId = p.state_id, LeaseNotes = p.lease_notes,
                    Rent = p.rent, DueDay = p.due_day, LeaseEnd = p.lease_end, LeaseStart = p.lease_start,
                    PetsOk = p.pets_ok, PetCount = p.pet_count,
                    PetRent = p.pet_rent, PetDeposit = p.pet_deposit,
                    SecurityDeposit = p.security_deposit, TrackRent = p.track_rent, LateFee = p.late_fee,
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
                    Description = q.description, Status = q.status,
                    ContactName = q.contact_name, ContactPhone = q.contact_phone,
                    Company = q.company, HandymanName = q.handyman_name, HandymanPhone = q.handyman_phone,
                    CancelReason = q.cancel_reason, RetryLater = q.retry_later, Notes = q.notes,
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
