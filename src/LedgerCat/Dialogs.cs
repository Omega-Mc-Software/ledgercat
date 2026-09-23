using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LedgerCat;

public class PropertyDialog : Form
{
    public Property P = new();

    readonly TextBox nameT = new() { Width = 240 };
    readonly TextBox unitT = new() { Width = 240 };
    readonly TextBox tenantT = new() { Width = 240 };
    readonly TextBox contactNameT = new() { Width = 240 };
    readonly TextBox contactPhoneT = new() { Width = 240 };
    readonly TextBox stateIdT = new() { Width = 240 };
    readonly TextBox rentT = new() { Width = 240 };
    readonly TextBox dueT = new() { Width = 240 };
    readonly TextBox leaseT = new() { Width = 240 };
    readonly TextBox leaseStartT = new() { Width = 240 }; // v1.2.8 (Dad): when rent tracking starts — guards last-month tracking
    readonly TextBox secDepT = new() { Width = 240 };
    readonly CheckBox petsC = new() { Text = "Pets allowed", AutoSize = true };
    readonly TextBox petCountT = new() { Width = 240 };
    readonly TextBox petRentT = new() { Width = 240 };
    readonly TextBox petDepT = new() { Width = 240 };
    readonly CheckBox trackRentC = new() { Text = "Watch this property's rent (due day + late fee)", AutoSize = true, Checked = true };
    readonly TextBox lateFeeT = new() { Width = 240 };
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 80 };
    readonly Label totalLbl = new() { AutoSize = true, ForeColor = Theme.Muted };
    readonly Dictionary<string, TextBox> extraBoxes = new();

    public PropertyDialog(Property? existing)
    {
        Text = existing == null ? "Add property" : "Edit property";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 720);
        MinimumSize = new Size(440, 480);
        Font = Theme.BaseFont;

        if (existing != null)
        {
            P = existing;
            nameT.Text = P.Name;
            unitT.Text = P.Unit;
            tenantT.Text = P.Tenant;
            contactNameT.Text = P.ContactName;
            contactPhoneT.Text = P.ContactPhone;
            stateIdT.Text = P.StateId;
            rentT.Text = P.Rent == 0 ? "" : P.Rent.ToString("0.##");
            dueT.Text = P.DueDay.ToString();
            leaseT.Text = P.LeaseEnd;
            leaseStartT.Text = P.LeaseStart;
            secDepT.Text = P.SecurityDeposit == 0 ? "" : P.SecurityDeposit.ToString("0.##");
            petsC.Checked = P.PetsOk;
            petCountT.Text = P.PetCount == 0 ? "" : P.PetCount.ToString();
            petRentT.Text = P.PetRent == 0 ? "" : P.PetRent.ToString("0.##");
            petDepT.Text = P.PetDeposit == 0 ? "" : P.PetDeposit.ToString("0.##");
            trackRentC.Checked = P.TrackRent;
            lateFeeT.Text = P.LateFee == 0 ? "" : P.LateFee.ToString("0.##");
            notesT.Text = P.LeaseNotes;
        }
        petsC.CheckedChanged += (_, _) => SyncPetFields();
        rentT.TextChanged += (_, _) => UpdateTotal();
        petRentT.TextChanged += (_, _) => UpdateTotal();

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoScroll = true,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(tlp, "Property *", nameT);
        AddRow(tlp, "Unit / apt", unitT);
        AddRow(tlp, "Tenant", tenantT);
        AddRow(tlp, "Contact name", contactNameT);
        AddRow(tlp, "Contact phone", contactPhoneT);
        AddRow(tlp, "State ID / DL", stateIdT);
        AddRow(tlp, "Rent $/mo *", rentT);
        AddRow(tlp, "Due day * (1-28)", dueT);
        AddRow(tlp, "Lease start", Ui.DateWithPicker(leaseStartT)); // v1.2.8: calendar picker beside the box (StorageCat port)
        AddRow(tlp, "Lease ends", Ui.DateWithPicker(leaseT)); // v1.2.8: calendar picker beside the box
        AddRow(tlp, "Security deposit $", secDepT);
        AddRow(tlp, "Pets", petsC);
        AddRow(tlp, "Pet count", petCountT);
        AddRow(tlp, "Pet rent $/mo", petRentT);
        AddRow(tlp, "Pet deposit $", petDepT);
        AddRow(tlp, "Rent tracking", trackRentC);
        AddRow(tlp, "Late fee $", lateFeeT);

        var layout = ColStore.Load("prop_grid_layout", ColStore.PropCoreKeys, ColStore.PropCoreMeta);
        foreach (var def in layout.Columns.Where(c => !c.Core))
        {
            var tb = new TextBox { Width = 240 };
            if (existing != null && existing.Extra.TryGetValue(def.Key, out var v)) tb.Text = v;
            extraBoxes[def.Key] = tb;
            AddRow(tlp, def.Header, tb);
        }

        tlp.Controls.Add(totalLbl);
        tlp.SetColumnSpan(totalLbl, 2);

        var notesLbl = new Label { Text = "Lease / move notes", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        tlp.Controls.Add(notesLbl);
        tlp.SetColumnSpan(notesLbl, 2);
        tlp.Controls.Add(notesT);
        tlp.SetColumnSpan(notesT, 2);

        var hint = new Label
        {
            Text = "Dates look like 2026-09-01 — leave empty if month-to-month. Lease start keeps a fresh move-in " +
                   "from showing last month as owed.\n" +
                   "Total due/mo = rent + pet rent. The late fee only applies when rent passes the due day.\n" +
                   "Notes are for renewal plans, move-out dates, deposit details. Double-click a 📝 in the grid to edit them fast.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
            MaximumSize = new Size(420, 0),
        };
        tlp.Controls.Add(hint);
        tlp.SetColumnSpan(hint, 2);

        var ok = Ui.Btn("Save", 100, (_, _) => Save());
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);

        Controls.Add(tlp);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;

        SyncPetFields();
        UpdateTotal();
    }

    void SyncPetFields()
    {
        bool on = petsC.Checked;
        petCountT.Enabled = on;
        petRentT.Enabled = on;
        petDepT.Enabled = on;
    }

    void UpdateTotal()
    {
        decimal rent = Ui.ParseMoneyOrZero(rentT.Text, out var r) ? r : 0;
        decimal petRent = Ui.ParseMoneyOrZero(petRentT.Text, out var pr) ? pr : 0;
        totalLbl.Text = petsC.Checked && petRent > 0
            ? $"Total due each month: {Theme.Money(rent + petRent)} (rent + pet rent)"
            : $"Total due each month: {Theme.Money(rent)}";
    }

    static void AddRow(TableLayoutPanel tlp, string label, Control c)
    {
        var l = new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        c.Dock = DockStyle.Fill;
        tlp.Controls.Add(l);
        tlp.Controls.Add(c);
    }

    static bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    void Save()
    {
        if (string.IsNullOrWhiteSpace(nameT.Text))
        {
            MessageBox.Show("Property needs a name.", "LedgerPaw", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoney(rentT.Text, out var rent))
        {
            MessageBox.Show("Rent must be a number greater than 0 (0 is fine too — type 0).", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!int.TryParse(dueT.Text, out var due) || due < 1 || due > 28)
        {
            MessageBox.Show("Due day must be a number from 1 to 28.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string lease = leaseT.Text.Trim();
        DateOnly leaseEnd = default, start = default;
        if (lease.Length > 0 && !Ui.ParseDate(lease, out leaseEnd))
        {
            MessageBox.Show("Lease end date should look like 2027-08-31, or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        // v1.2.8 (Dad): lease start guards last-month tracking — it has to be a real date,
        // and it can't sit after the lease ends.
        string leaseStart = leaseStartT.Text.Trim();
        if (leaseStart.Length > 0 && !Ui.ParseDate(leaseStart, out start))
        {
            MessageBox.Show("Lease start date should look like 2026-09-01, or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (leaseStart.Length > 0 && lease.Length > 0 && start > leaseEnd)
        {
            MessageBox.Show("Lease start is after lease end — double-check the two dates.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(secDepT.Text, out var secDep))
        {
            MessageBox.Show("Security deposit must be a number (0 or more), or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var petCount = 0;
        if (petCountT.Text.Trim() != "" &&
            (!int.TryParse(petCountT.Text.Trim(), out petCount) || petCount < 0))
        {
            MessageBox.Show("Pet count must be a whole number (0 or more), or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(petRentT.Text, out var petRent))
        {
            MessageBox.Show("Pet rent must be a number (0 or more), or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(petDepT.Text, out var petDep))
        {
            MessageBox.Show("Pet deposit must be a number (0 or more), or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(lateFeeT.Text, out var lateFee))
        {
            MessageBox.Show("Late fee must be a number (0 or more), or stay empty.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Guard rails: an empty tenant or phone is allowed, but never by accident.
        if (string.IsNullOrWhiteSpace(tenantT.Text) && !Confirm(
                "No tenant is listed for this property.\n\nSave anyway?\n(You can add the tenant later in Edit.)",
                "No tenant listed"))
            return;
        if (string.IsNullOrWhiteSpace(contactPhoneT.Text) && !Confirm(
                "No contact phone number is filled in.\n\nSave anyway?\n(When rent is late or a pipe bursts, this number is the one you want.)",
                "No contact phone"))
            return;

        P.Name = nameT.Text.Trim();
        P.Unit = unitT.Text.Trim();
        P.Tenant = tenantT.Text.Trim();
        P.ContactName = contactNameT.Text.Trim();
        P.ContactPhone = contactPhoneT.Text.Trim();
        P.StateId = stateIdT.Text.Trim();
        P.Rent = rent;
        P.DueDay = due;
        P.LeaseEnd = lease;
        P.LeaseStart = leaseStart;
        P.LeaseNotes = notesT.Text.Trim();
        P.SecurityDeposit = secDep;
        P.PetsOk = petsC.Checked;
        P.PetCount = petsC.Checked ? petCount : 0;
        P.PetRent = petsC.Checked ? petRent : 0;
        P.PetDeposit = petsC.Checked ? petDep : 0;
        P.TrackRent = trackRentC.Checked;
        P.LateFee = lateFee;
        foreach (var kv in extraBoxes)
            P.Extra[kv.Key] = kv.Value.Text.Trim();
        DialogResult = DialogResult.OK;
    }
}

public class TxnDialog : Form
{
    public Txn? T;

    readonly string kind;
    readonly TextBox dateT = new() { Width = 240 };
    readonly ComboBox propC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox catT = new() { Width = 240 };
    readonly TextBox amountT = new() { Width = 240 };
    readonly TextBox noteT = new() { Width = 240, Multiline = true, Height = 64 };
    readonly List<Property> props;
    readonly List<long?> propIds = new();
    readonly Txn? existing;
    readonly Dictionary<string, TextBox> extraBoxes = new();
    bool touchedAmount;
    bool touchedCat;

    public TxnDialog(string kind, List<Property> props) : this(kind, props, null) { }

    public TxnDialog(string kind, List<Property> props, Txn? existing, Property? prefillFrom = null)
    {
        this.kind = existing?.Kind ?? kind;
        this.props = props;
        this.existing = existing;

        bool editing = existing != null;
        Text = editing
            ? (this.kind == "rent" ? "Edit rent entry" : "Edit expense entry")
            : (this.kind == "rent" ? "Record rent (money in)" : "Add expense (money out)");
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 420);
        MinimumSize = new Size(400, 360);
        Font = Theme.BaseFont;

        dateT.Text = existing?.Date ?? Ui.Today();
        catT.Text = existing?.Category ?? "";
        amountT.Text = existing is { Amount: > 0 } ? existing.Amount.ToString("0.##") : "";
        noteT.Text = existing?.Note ?? "";

        propIds.Add(null);
        propC.Items.Add("(no property)");
        foreach (var p in props)
        {
            propIds.Add(p.Id);
            propC.Items.Add(p.Label);
        }

        // v1.2.1: picking a property on a new rent entry suggests its total rent + the current month
        amountT.TextChanged += (_, _) => touchedAmount = true;
        catT.TextChanged += (_, _) => touchedCat = true;
        propC.SelectedIndexChanged += (_, _) => PrefillForProperty();

        propC.SelectedIndex = 0;
        if (existing?.PropertyId is long pid)
        {
            var idx = propIds.IndexOf(pid);
            if (idx >= 0) propC.SelectedIndex = idx;
        }
        else if (prefillFrom != null)
        {
            var idx = propIds.IndexOf(prefillFrom.Id);
            if (idx > 0) propC.SelectedIndex = idx;
        }

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoScroll = true,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(tlp, "Date *", Ui.DateWithPicker(dateT)); // v1.2.8: calendar picker beside the box (StorageCat port)
        AddRow(tlp, "Property", propC);
        AddRow(tlp, "Category", catT);
        AddRow(tlp, "Amount $ *", amountT);
        var moneyLayout = ColStore.Load("money_grid_layout", ColStore.MoneyCoreKeys, ColStore.MoneyCoreMeta);
        foreach (var def in moneyLayout.Columns.Where(c => !c.Core))
        {
            var tb = new TextBox { Width = 240 };
            if (existing != null && existing.Extra.TryGetValue(def.Key, out var v)) tb.Text = v;
            extraBoxes[def.Key] = tb;
            AddRow(tlp, def.Header, tb);
        }

        // v1.2.5 (Dad): note box gets the whole dialog width, label above it
        var noteLbl = new Label { Text = "Note", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        tlp.Controls.Add(noteLbl, 0, 4);
        tlp.Controls.Add(noteT, 0, 5);
        tlp.SetColumnSpan(noteT, 2);

        var catHint = new Label
        {
            Text = this.kind == "rent"
                ? "Tip: category can be which month this rent covers, e.g. \"September\"."
                : "e.g. repair, plumbing, supplies, insurance, tax.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
            MaximumSize = new Size(270, 0), // v1.2.4: wrap instead of getting clipped
        };
        tlp.Controls.Add(catHint, 0, 6);
        tlp.SetColumnSpan(catHint, 2);

        var ok = Ui.Btn("Save", 100, (_, _) => Save());
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);

        Controls.Add(tlp);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    void PrefillForProperty()
    {
        if (existing != null || kind != "rent" || propC.SelectedIndex <= 0) return;
        var p = props[propC.SelectedIndex - 1];
        decimal amount = ExpectedRent(p, out bool withFee, out decimal prevDue); // v1.2.6 (Dad): shared expected-rent math so the prefill and the confirm popup always agree
        if (amount > 0 && noteT.Text.Length == 0)
        {
            // v1.2.8 (Dad): the note now says what the number is made of, carryover included
            string note = withFee ? $"includes {p.LateFee:0.##} late fee" : "";
            if (prevDue > 0)
                note = note.Length > 0
                    ? note + $" + {Theme.Money(prevDue)} from last month"
                    : $"{Theme.Money(prevDue)} carried over from last month";
            if (note.Length > 0) noteT.Text = note;
        }
        if (amount > 0 && !touchedAmount && amountT.Text.Length == 0)
            amountT.Text = amount.ToString("0.##");
        if (!touchedCat && catT.Text.Length == 0)
            catT.Text = DateTime.Today.ToString("MMMM", CultureInfo.InvariantCulture);
    }

    // What this month's rent entry for this property should total: rent + pet rent,
    // plus the late fee only when the property is tracked, charges one, is past due, and hasn't paid yet.
    // v1.2.8 (Dad): plus anything still unpaid from LAST month, so collecting both months in
    // one entry doesn't trip the differs-from-expected popup.
    decimal ExpectedRent(Property p, out bool lateFeeIncluded, out decimal prevUnpaid)
    {
        lateFeeIncluded = false;
        decimal expected = p.TotalRentDue;
        var txns = Db.ListTxns();
        if (p.TrackRent && p.TotalRentDue > 0 && p.LateFee > 0 && DateTime.Today.Day > p.DueDay)
        {
            string mk = DateTime.Today.ToString("yyyy-MM");
            bool paidAlready = txns.Any(t =>
                t.Kind == "rent" && t.PropertyId == p.Id && t.Date.StartsWith(mk));
            if (!paidAlready)
            {
                expected += p.LateFee;
                lateFeeIncluded = true;
            }
        }
        prevUnpaid = Billing.PrevMonthUnpaid(p, txns, DateTime.Today);
        expected += prevUnpaid;
        return expected;
    }

    static void AddRow(TableLayoutPanel tlp, string label, Control c)
    {
        var l = new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        c.Dock = DockStyle.Fill;
        tlp.Controls.Add(l);
        tlp.Controls.Add(c);
    }

    void Save()
    {
        if (!Ui.ParseDate(dateT.Text, out var d))
        {
            MessageBox.Show("Date should look like 2026-09-11.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoney(amountT.Text, out var amount))
        {
            MessageBox.Show("Amount must be a number greater than 0.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // v1.2.1: rent entries are checked against the property's tracked rent
        if (kind == "rent" && propC.SelectedIndex > 0)
        {
            var p = props[propC.SelectedIndex - 1];
            decimal expected = ExpectedRent(p, out bool withFee, out decimal prevDue); // v1.2.6 (Dad): expected includes the late fee too, so a correct prefill no longer triggers this
            if (p.TotalRentDue > 0 && amount != expected && MessageBox.Show(
                    $"This rent entry is {Theme.Money(amount)}, but {p.Label}'s rent to collect is {Theme.Money(expected)} (rent + pet rent{(withFee ? " + late fee" : "")}{(prevDue > 0 ? $" + {Theme.Money(prevDue)} still owed from last month" : "")}).\n\nSave anyway?",
                    "Amount differs from expected rent",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        var idx = propC.SelectedIndex;
        T = new Txn
        {
            Date = d.ToString("yyyy-MM-dd"),
            PropertyId = idx >= 0 ? propIds[idx] : null,
            Kind = kind,
            Category = catT.Text.Trim(),
            Amount = amount,
            Note = noteT.Text.Trim(),
        };
        foreach (var kv in extraBoxes)
            T.Extra[kv.Key] = kv.Value.Text.Trim();
        DialogResult = DialogResult.OK;
    }
}

public class ReqDialog : Form
{
    public Req? Q;

    readonly TextBox dateT = new() { Width = 240 };
    readonly ComboBox propC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox kindC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox descT = new() { Width = 240, Multiline = true, Height = 70 };
    readonly TextBox contactNameT = new() { Width = 240 };
    readonly TextBox contactPhoneT = new() { Width = 240 };
    readonly TextBox companyT = new() { Width = 240 };
    readonly TextBox handyNameT = new() { Width = 240 };
    readonly TextBox handyPhoneT = new() { Width = 240 };
    readonly TextBox retryT = new() { Width = 240 };
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 80 };
    readonly List<Property> props;
    readonly List<long?> propIds = new();
    readonly Dictionary<string, TextBox> extraBoxes = new();
    readonly Req? existing;

    public ReqDialog(List<Property> props) : this(props, null) { }

    public ReqDialog(List<Property> props, Req? existing)
    {
        this.props = props;
        this.existing = existing;

        Text = existing == null ? "Add request" : "Edit request";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 700);
        MinimumSize = new Size(420, 480);
        Font = Theme.BaseFont;

        dateT.Text = existing?.Created ?? Ui.Today();
        descT.Text = existing?.Description ?? "";
        contactNameT.Text = existing?.ContactName ?? "";
        contactPhoneT.Text = existing?.ContactPhone ?? "";
        companyT.Text = existing?.Company ?? "";
        handyNameT.Text = existing?.HandymanName ?? "";
        handyPhoneT.Text = existing?.HandymanPhone ?? "";
        retryT.Text = existing?.RetryLater ?? "";
        notesT.Text = existing?.Notes ?? "";

        propIds.Add(null);
        propC.Items.Add("(no property)");
        foreach (var p in props)
        {
            propIds.Add(p.Id);
            propC.Items.Add(p.Label);
        }
        propC.SelectedIndex = 0;
        if (existing?.PropertyId is long pid)
        {
            var idx = propIds.IndexOf(pid);
            if (idx >= 0) propC.SelectedIndex = idx;
        }
        kindC.Items.Add("Maintenance");
        kindC.Items.Add("Viewing");
        kindC.SelectedIndex = existing?.Kind == "viewing" ? 1 : 0;

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoScroll = true,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(tlp, "Date *", Ui.DateWithPicker(dateT)); // v1.2.8: calendar picker beside the box (StorageCat port)
        AddRow(tlp, "Property", propC);
        AddRow(tlp, "Type", kindC);
        AddRow(tlp, "What's needed", descT);
        AddRow(tlp, "Contact name", contactNameT);
        AddRow(tlp, "Contact phone", contactPhoneT);
        AddRow(tlp, "Company", companyT);
        AddRow(tlp, "Handyman name", handyNameT);
        AddRow(tlp, "Handyman phone", handyPhoneT);
        AddRow(tlp, "Retry later", retryT);
        var reqLayout = ColStore.Load("req_grid_layout", ColStore.ReqCoreKeys, ColStore.ReqCoreMeta);
        foreach (var def in reqLayout.Columns.Where(c => !c.Core))
        {
            var tb = new TextBox { Width = 240 };
            if (existing != null && existing.Extra.TryGetValue(def.Key, out var v)) tb.Text = v;
            extraBoxes[def.Key] = tb;
            AddRow(tlp, def.Header, tb);
        }

        // v1.2.5 (Dad): notes box gets the whole dialog width, label above it
        var notesLbl = new Label { Text = "Notes / comments", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        tlp.Controls.Add(notesLbl, 0, 10);
        tlp.Controls.Add(notesT, 0, 11);
        tlp.SetColumnSpan(notesT, 2);

        var hint = new Label
        {
            Text = "Tenant contact = who lives there. Handyman = who you call to fix it.\n" +
                   "Retry later: when to come back to this — a date like 2026-09-20, or a short note.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
            MaximumSize = new Size(400, 0), // v1.2.4: wrap instead of getting clipped
        };
        tlp.Controls.Add(hint, 0, 12);
        tlp.SetColumnSpan(hint, 2);

        var ok = Ui.Btn("Save", 100, (_, _) => Save());
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);

        Controls.Add(tlp);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    static void AddRow(TableLayoutPanel tlp, string label, Control c)
    {
        var l = new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        c.Dock = DockStyle.Fill;
        tlp.Controls.Add(l);
        tlp.Controls.Add(c);
    }

    void Save()
    {
        if (!Ui.ParseDate(dateT.Text, out var d))
        {
            MessageBox.Show("Date should look like 2026-09-11.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(descT.Text))
        {
            MessageBox.Show("Write a short description of what's needed.", "LedgerPaw",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var idx = propC.SelectedIndex;
        Q = new Req
        {
            Created = d.ToString("yyyy-MM-dd"),
            PropertyId = idx >= 0 ? propIds[idx] : null,
            Kind = kindC.SelectedIndex == 1 ? "viewing" : "maintenance",
            Description = descT.Text.Trim(),
            Status = "open",
            ContactName = contactNameT.Text.Trim(),
            ContactPhone = contactPhoneT.Text.Trim(),
            Company = companyT.Text.Trim(),
            HandymanName = handyNameT.Text.Trim(),
            HandymanPhone = handyPhoneT.Text.Trim(),
            RetryLater = retryT.Text.Trim(),
            Notes = notesT.Text.Trim(),
        };
        foreach (var kv in extraBoxes)
            Q.Extra[kv.Key] = kv.Value.Text.Trim();
        DialogResult = DialogResult.OK;
    }
}

/// <summary>
/// v1.2.1: canceling a request asks why. The reason lands in the request's Notes column.
/// </summary>
public class CancelDialog : Form
{
    public string Reason = "";

    readonly TextBox reasonT = new() { Multiline = true, Height = 64, Dock = DockStyle.Fill };

    public CancelDialog(string what)
    {
        Text = "Cancel request";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 220);
        Font = Theme.BaseFont;

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 3,
        };
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var ask = new Label
        {
            Text = $"Cancel \"{what}\"?\n\nWhy cancel? (optional — it shows in the Notes column so future-you remembers)",
            AutoSize = true,
        };
        tlp.Controls.Add(ask, 0, 0);
        tlp.Controls.Add(reasonT, 0, 1);

        var ok = Ui.Btn("Cancel request", 130, (_, _) => { Reason = reasonT.Text.Trim(); DialogResult = DialogResult.OK; });
        var cancel = Ui.Btn("Keep it open", 120, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);

        Controls.Add(tlp);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

// v1.2.8 (Dad): a tiny note editor — double-click a 📝 cell in any grid and write,
// no full dialog needed.
public class NoteDialog : Form
{
    public string NoteText => noteT.Text;
    readonly TextBox noteT = new()
    {
        Multiline = true,
        Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical,
    };

    public NoteDialog(string title, string text)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 300);
        Font = Theme.BaseFont;
        noteT.Text = text;

        var ok = Ui.Btn("Save", 100, (_, _) => DialogResult = DialogResult.OK);
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);

        Controls.Add(noteT);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public class WaitDialog : Form
{
    public Db.WaitRow W = new();
    readonly TextBox nameT = new() { Width = 240 };
    readonly TextBox phoneT = new() { Width = 240 };
    readonly TextBox emailT = new() { Width = 240 };
    readonly TextBox desiredT = new() { Width = 240 };
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 64 };
    readonly ComboBox statusC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Dictionary<string, TextBox> extraBoxes = new();

    public WaitDialog(Db.WaitRow? existing)
    {
        Text = existing == null ? "Add waitlist" : "Edit waitlist";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 400);
        MinimumSize = new Size(400, 320);
        Font = Theme.BaseFont;
        statusC.Items.AddRange(new object[] { "open", "placed", "canceled" });
        statusC.SelectedIndex = 0;
        if (existing != null)
        {
            W = existing;
            nameT.Text = W.Name;
            phoneT.Text = W.Phone;
            emailT.Text = W.Email;
            desiredT.Text = W.Desired;
            notesT.Text = W.Notes;
            var i = statusC.Items.IndexOf(W.Status);
            statusC.SelectedIndex = i >= 0 ? i : 0;
        }
        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2 };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string l, Control c)
        {
            tlp.Controls.Add(new Label { Text = l, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            c.Dock = DockStyle.Fill;
            tlp.Controls.Add(c);
        }
        Row("Name *", nameT);
        Row("Phone", phoneT);
        Row("Email", emailT);
        Row("Desired unit", desiredT);
        Row("Status", statusC);
        var waitLayout = ColStore.Load("wait_grid_layout", ColStore.WaitCoreKeys, ColStore.WaitCoreMeta);
        foreach (var def in waitLayout.Columns.Where(c => !c.Core))
        {
            var tb = new TextBox { Width = 240 };
            if (existing != null && existing.Extra.TryGetValue(def.Key, out var v)) tb.Text = v;
            extraBoxes[def.Key] = tb;
            Row(def.Header, tb);
        }
        Row("Notes", notesT);
        var ok = Ui.Btn("Save", 100, (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameT.Text))
            {
                MessageBox.Show("Needs a name.", "Waitlist");
                return;
            }
            if (W.Created.Length == 0) W.Created = Ui.Today();
            W.Name = nameT.Text.Trim();
            W.Phone = phoneT.Text.Trim();
            W.Email = emailT.Text.Trim();
            W.Desired = desiredT.Text.Trim();
            W.Notes = notesT.Text.Trim();
            W.Status = statusC.SelectedItem?.ToString() ?? "open";
            foreach (var kv in extraBoxes)
                W.Extra[kv.Key] = kv.Value.Text.Trim();
            DialogResult = DialogResult.OK;
        });
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);
        Controls.Add(tlp);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public class CsvMapDialog : Form
{
    public Dictionary<string, int> Map = new();
    public HashSet<string> Skip = new();
    public bool CreateUnmapped;

    public CsvMapDialog(string[] header, string[] targets, Dictionary<string, string[]> aliases)
    {
        Text = "Map CSV columns";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 680);
        MinimumSize = new Size(540, 480);
        Font = Theme.BaseFont;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
        flow.Controls.Add(new Label { Text = "Match each LedgerPaw field to a column in your file. Skip ones you don't have. Duplicate header names in the file are flagged.", AutoSize = true, MaximumSize = new Size(480, 0), Tag = "muted" });

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            var h = header[i].Trim();
            if (h.Length == 0) continue;
            if (seen.ContainsKey(h)) dups.Add(h);
            else seen[h] = i;
        }
        if (dups.Count > 0)
            flow.Controls.Add(new Label { Text = "Warning: duplicate column names in the file: " + string.Join(", ", dups) + ". The first one wins unless you pick another.", AutoSize = true, ForeColor = Theme.Danger, MaximumSize = new Size(480, 0) });

        var combos = new Dictionary<string, ComboBox>();
        string[] choices = new[] { "(skip)" }.Concat(header.Select((h, i) => $"{i}: {h}")).ToArray();

        foreach (var t in targets)
        {
            var row = new FlowLayoutPanel { Width = 480, Height = 32, FlowDirection = FlowDirection.LeftToRight };
            row.Controls.Add(new Label { Text = t, Width = 150, TextAlign = ContentAlignment.MiddleLeft });
            var cb = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cb.Items.AddRange(choices);
            int auto = AutoMatch(header, t, aliases);
            cb.SelectedIndex = auto >= 0 ? auto + 1 : 0;
            combos[t] = cb;
            row.Controls.Add(cb);
            flow.Controls.Add(row);
        }

        var create = new CheckBox { Text = "Create a user column for leftover CSV headers that didn't map", AutoSize = true, Checked = true, MaximumSize = new Size(520, 0) };

        var ok = Ui.Btn("Import", 110, (_, _) =>
        {
            Map.Clear(); Skip.Clear();
            foreach (var kv in combos)
            {
                int i = kv.Value.SelectedIndex - 1;
                if (i < 0) Skip.Add(kv.Key);
                else Map[kv.Key] = i;
            }
            CreateUnmapped = create.Checked;
            DialogResult = DialogResult.OK;
        });
        var cancel = Ui.Btn("Cancel", 100, (_, _) => DialogResult = DialogResult.Cancel);
        var foot = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(12, 6, 12, 8) };
        create.Location = new Point(12, 6);
        var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0) };
        btns.Controls.Add(cancel);
        btns.Controls.Add(ok);
        foot.Controls.Add(create);
        foot.Controls.Add(btns);
        Controls.Add(flow);
        Controls.Add(foot);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    static int AutoMatch(string[] header, string target, Dictionary<string, string[]> aliases)
    {
        var want = new List<string> { target.Replace("_", " "), target };
        if (aliases.TryGetValue(target, out var extra)) want.AddRange(extra);
        for (int i = 0; i < header.Length; i++)
        {
            var h = header[i].Trim().Trim('"').ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            foreach (var w in want)
                if (h == w.ToLowerInvariant().Replace(" ", "_")) return i;
        }
        return -1;
    }
}
