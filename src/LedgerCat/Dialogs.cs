using System;
using System.Collections.Generic;
using System.Drawing;
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
    readonly TextBox notesT = new() { Width = 240, Multiline = true, Height = 56 };

    public PropertyDialog(Property? existing)
    {
        Text = existing == null ? "Add property" : "Edit property";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 470);
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
            notesT.Text = P.LeaseNotes;
        }

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
        AddRow(tlp, "Lease / move notes", notesT);

        var hint = new Label
        {
            Text = "Lease date format: 2027-08-31 — leave empty if month-to-month.\n" +
                   "Notes are for renewal plans, move-out dates, deposit details.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(hint, 0, 10);
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

    public TxnDialog(string kind, List<Property> props) : this(kind, props, null) { }

    public TxnDialog(string kind, List<Property> props, Txn? existing)
    {
        this.kind = existing?.Kind ?? kind;
        this.props = props;

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
        propC.SelectedIndex = 0;
        if (existing?.PropertyId is long pid)
        {
            var idx = propIds.IndexOf(pid);
            if (idx >= 0) propC.SelectedIndex = idx;
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
        ClientSize = new Size(400, 500);
        Font = Theme.BaseFont;

        dateT.Text = existing?.Created ?? Ui.Today();
        descT.Text = existing?.Description ?? "";
        contactNameT.Text = existing?.ContactName ?? "";
        contactPhoneT.Text = existing?.ContactPhone ?? "";
        companyT.Text = existing?.Company ?? "";
        handyNameT.Text = existing?.HandymanName ?? "";
        handyPhoneT.Text = existing?.HandymanPhone ?? "";

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

        var hint = new Label
        {
            Text = "Tenant contact = who lives there. Handyman = who you call to fix it.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(hint, 0, 9);
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
        };
        DialogResult = DialogResult.OK;
    }
}
