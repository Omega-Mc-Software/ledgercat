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
    readonly TextBox rentT = new() { Width = 240 };
    readonly TextBox dueT = new() { Width = 240 };
    readonly TextBox leaseT = new() { Width = 240 };

    public PropertyDialog(Property? existing)
    {
        Text = existing == null ? "Add property" : "Edit property";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 300);
        Font = Theme.BaseFont;

        if (existing != null)
        {
            P = existing;
            nameT.Text = P.Name;
            unitT.Text = P.Unit;
            tenantT.Text = P.Tenant;
            rentT.Text = P.Rent == 0 ? "" : P.Rent.ToString("0.##");
            dueT.Text = P.DueDay.ToString();
            leaseT.Text = P.LeaseEnd;
        }

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 7,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(tlp, "Property *", nameT);
        AddRow(tlp, "Unit / apt", unitT);
        AddRow(tlp, "Tenant", tenantT);
        AddRow(tlp, "Rent $/mo", rentT);
        AddRow(tlp, "Due day (1-28)", dueT);
        AddRow(tlp, "Lease ends", leaseT);

        var hint = new Label
        {
            Text = "Lease date format: 2027-08-31 — leave empty if month-to-month.",
            Tag = "muted",
            AutoSize = true,
            ForeColor = Theme.Muted,
        };
        tlp.Controls.Add(hint, 0, 6);
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

        P.Name = nameT.Text.Trim();
        P.Unit = unitT.Text.Trim();
        P.Tenant = tenantT.Text.Trim();
        P.Rent = rent;
        P.DueDay = due;
        P.LeaseEnd = lease;
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

    public TxnDialog(string kind, List<Property> props)
    {
        this.kind = kind;
        this.props = props;

        Text = kind == "rent" ? "Record rent (money in)" : "Add expense (money out)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 280);
        Font = Theme.BaseFont;

        dateT.Text = Ui.Today();
        propIds.Add(null);
        propC.Items.Add("(no property)");
        foreach (var p in props)
        {
            propIds.Add(p.Id);
            propC.Items.Add(p.Label);
        }
        propC.SelectedIndex = 0;

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
            Text = kind == "rent"
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
    readonly TextBox descT = new() { Width = 240, Multiline = true, Height = 70 };
    readonly List<Property> props;
    readonly List<long?> propIds = new();

    public ReqDialog(List<Property> props)
    {
        this.props = props;

        Text = "Add request";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 320);
        Font = Theme.BaseFont;

        dateT.Text = Ui.Today();
        propIds.Add(null);
        propC.Items.Add("(no property)");
        foreach (var p in props)
        {
            propIds.Add(p.Id);
            propC.Items.Add(p.Label);
        }
        propC.SelectedIndex = 0;
        kindC.Items.Add("Maintenance");
        kindC.Items.Add("Viewing");
        kindC.SelectedIndex = 0;

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
        AddRow(tlp, "Type", kindC);
        AddRow(tlp, "What's needed", descT);

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
            Done = false,
        };
        DialogResult = DialogResult.OK;
    }
}
