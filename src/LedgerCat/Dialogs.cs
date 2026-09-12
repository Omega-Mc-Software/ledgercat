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
    readonly TextBox secDepT = new() { Width = 240 };
    readonly CheckBox petsC = new() { Text = "Pets allowed", AutoSize = true };
    readonly TextBox petCountT = new() { Width = 240 };
    readonly TextBox petRentT = new() { Width = 240 };
    readonly TextBox petDepT = new() { Width = 240 };
    readonly CheckBox trackRentC = new() { Text = "Watch this property's rent (due day + late fee)", AutoSize = true, Checked = true };
    readonly TextBox lateFeeT = new() { Width = 240 };
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 56 };
    readonly Label totalLbl = new() { AutoSize = true, ForeColor = Theme.Muted };

    public PropertyDialog(Property? existing)
    {
        Text = existing == null ? "Add property" : "Edit property";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(410, 700);
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
        AddRow(tlp, "Lease ends", leaseT);
        AddRow(tlp, "Security deposit $", secDepT);
        AddRow(tlp, "Pets", petsC);
        AddRow(tlp, "Pet count", petCountT);
        AddRow(tlp, "Pet rent $/mo", petRentT);
        AddRow(tlp, "Pet deposit $", petDepT);
        AddRow(tlp, "Rent tracking", trackRentC);
        AddRow(tlp, "Late fee $", lateFeeT);
        AddRow(tlp, "Lease / move notes", notesT);

        tlp.Controls.Add(totalLbl, 1, 16);

        var hint = new Label
        {
            Text = "Lease date format: 2027-08-31 — leave empty if month-to-month.\n" +
                   "Total due/mo = rent + pet rent. The late fee only applies when rent passes the due day.\n" +
                   "Notes are for renewal plans, move-out dates, deposit details.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(hint, 0, 17);
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
            MessageBox.Show("Property needs a name.", "LedgerCat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoney(rentT.Text, out var rent))
        {
            MessageBox.Show("Rent must be a number greater than 0 (0 is fine too — type 0).", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!int.TryParse(dueT.Text, out var due) || due < 1 || due > 28)
        {
            MessageBox.Show("Due day must be a number from 1 to 28.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string lease = leaseT.Text.Trim();
        if (lease.Length > 0 && !Ui.ParseDate(lease, out _))
        {
            MessageBox.Show("Lease end date should look like 2027-08-31, or stay empty.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(secDepT.Text, out var secDep))
        {
            MessageBox.Show("Security deposit must be a number (0 or more), or stay empty.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var petCount = 0;
        if (petCountT.Text.Trim() != "" &&
            (!int.TryParse(petCountT.Text.Trim(), out petCount) || petCount < 0))
        {
            MessageBox.Show("Pet count must be a whole number (0 or more), or stay empty.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(petRentT.Text, out var petRent))
        {
            MessageBox.Show("Pet rent must be a number (0 or more), or stay empty.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(petDepT.Text, out var petDep))
        {
            MessageBox.Show("Pet deposit must be a number (0 or more), or stay empty.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoneyOrZero(lateFeeT.Text, out var lateFee))
        {
            MessageBox.Show("Late fee must be a number (0 or more), or stay empty.", "LedgerCat",
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
        P.LeaseNotes = notesT.Text.Trim();
        P.SecurityDeposit = secDep;
        P.PetsOk = petsC.Checked;
        P.PetCount = petsC.Checked ? petCount : 0;
        P.PetRent = petsC.Checked ? petRent : 0;
        P.PetDeposit = petsC.Checked ? petDep : 0;
        P.TrackRent = trackRentC.Checked;
        P.LateFee = lateFee;
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
    readonly TextBox noteT = new() { Width = 240 };
    readonly List<Property> props;
    readonly List<long?> propIds = new();
    readonly Txn? existing;
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
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 280);
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
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(tlp, "Date *", dateT);
        AddRow(tlp, "Property", propC);
        AddRow(tlp, "Category", catT);
        AddRow(tlp, "Amount $ *", amountT);
        AddRow(tlp, "Note", noteT);

        var catHint = new Label
        {
            Text = this.kind == "rent"
                ? "Tip: category can be which month this rent covers, e.g. \"September\"."
                : "e.g. repair, plumbing, supplies, insurance, tax.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(catHint, 1, 5);

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
        if (p.TotalRentDue > 0 && !touchedAmount && amountT.Text.Length == 0)
            amountT.Text = p.TotalRentDue.ToString("0.##");
        if (!touchedCat && catT.Text.Length == 0)
            catT.Text = DateTime.Today.ToString("MMMM", CultureInfo.InvariantCulture);
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
            MessageBox.Show("Date should look like 2026-09-11.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Ui.ParseMoney(amountT.Text, out var amount))
        {
            MessageBox.Show("Amount must be a number greater than 0.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // v1.2.1: rent entries are checked against the property's tracked rent
        if (kind == "rent" && propC.SelectedIndex > 0)
        {
            var p = props[propC.SelectedIndex - 1];
            if (p.TotalRentDue > 0 && amount != p.TotalRentDue && MessageBox.Show(
                    $"This rent entry is {Theme.Money(amount)}, but {p.Label}'s rent to collect is {Theme.Money(p.TotalRentDue)} (rent + pet rent).\n\nSave anyway?",
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
        DialogResult = DialogResult.OK;
    }
}

public class ReqDialog : Form
{
    public Req? Q;

    readonly TextBox dateT = new() { Width = 240 };
    readonly ComboBox propC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox kindC = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox descT = new() { Width = 240, Multiline = true, Height = 60 };
    readonly TextBox contactNameT = new() { Width = 240 };
    readonly TextBox contactPhoneT = new() { Width = 240 };
    readonly TextBox companyT = new() { Width = 240 };
    readonly TextBox handyNameT = new() { Width = 240 };
    readonly TextBox handyPhoneT = new() { Width = 240 };
    readonly TextBox retryT = new() { Width = 240 };
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 56 };
    readonly List<Property> props;
    readonly List<long?> propIds = new();

    public ReqDialog(List<Property> props) : this(props, null) { }

    public ReqDialog(List<Property> props, Req? existing)
    {
        this.props = props;

        Text = existing == null ? "Add request" : "Edit request";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 590);
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

        AddRow(tlp, "Date *", dateT);
        AddRow(tlp, "Property", propC);
        AddRow(tlp, "Type", kindC);
        AddRow(tlp, "What's needed", descT);
        AddRow(tlp, "Contact name", contactNameT);
        AddRow(tlp, "Contact phone", contactPhoneT);
        AddRow(tlp, "Company", companyT);
        AddRow(tlp, "Handyman name", handyNameT);
        AddRow(tlp, "Handyman phone", handyPhoneT);
        AddRow(tlp, "Retry later", retryT);
        AddRow(tlp, "Notes / comments", notesT);

        var hint = new Label
        {
            Text = "Tenant contact = who lives there. Handyman = who you call to fix it.\n" +
                   "Retry later: when to come back to this — a date like 2026-09-20, or a short note.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(hint, 0, 11);
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
            MessageBox.Show("Date should look like 2026-09-11.", "LedgerCat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(descT.Text))
        {
            MessageBox.Show("Write a short description of what's needed.", "LedgerCat",
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
