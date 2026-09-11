using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LedgerCat;

public static class Ui
{
    public static DataGridViewTextBoxColumn Col(string header, int width,
        DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            Width = width,
            DefaultCellStyle = { Alignment = align },
        };
    }

    public static Button Btn(string text, int width = 110, EventHandler? onClick = null)
    {
        // 32px: default 23px clips the bottom of 9.75pt Segoe UI text (found by Dad on v1.0)
        var b = new Button { Text = text, Width = width, Height = 32 };
        if (onClick != null) b.Click += onClick;
        return b;
    }

    public static string Today() => DateTime.Today.ToString("yyyy-MM-dd");

    public static bool ParseDate(string? s, out DateOnly d) =>
        DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    public static bool ParseMoney(string? s, out decimal v)
    {
        v = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return decimal.TryParse(s.Trim().Replace("$", "").Replace(",", ""),
            NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0;
    }

    public static Panel TopBar(params Control[] buttons)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 8, 12, 8) };
        int x = 0;
        foreach (var b in buttons)
        {
            b.Location = new Point(x, 8);
            p.Controls.Add(b);
            x += b.Width + 8;
        }
        return p;
    }

    public static DataGridView MakeGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
    }
}

public class PropertiesTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly Label warnLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 4, 4, 0),
        Tag = "muted",
    };

    public PropertiesTab()
    {
        var add = Ui.Btn("Add property", 120, (_, _) => Edit(null));
        var editB = Ui.Btn("Edit", 80, (_, _) => EditSelected());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Property", 210));
        grid.Columns.Add(Ui.Col("Tenant", 150));
        grid.Columns.Add(Ui.Col("Rent / month", 110, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Rent due day", 95, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Lease ends", 110, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Days left", 90, DataGridViewContentAlignment.MiddleRight));

        var bar = Ui.TopBar(add, editB, del);

        Controls.Add(grid);
        Controls.Add(bar);
        Controls.Add(warnLabel);

        grid.CellDoubleClick += (_, _) => EditSelected();
    }

    void EditSelected()
    {
        if (grid.SelectedRows.Count == 0) return;
        if (grid.SelectedRows[0].Tag is long id)
        {
            var p = Db.ListProps().FirstOrDefault(x => x.Id == id);
            if (p != null) Edit(p);
        }
    }

    void Edit(Property? existing)
    {
        using var dlg = new PropertyDialog(existing);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Db.SaveProp(dlg.P);
            RefreshData();
        }
    }

    void DeleteSelected()
    {
        if (grid.SelectedRows.Count == 0) return;
        if (grid.SelectedRows[0].Tag is not long id) return;
        if (MessageBox.Show("Delete this property? Past transactions are kept but lose the property link.",
                "Delete property", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.DeleteProp(id);
        RefreshData();
    }

    public void RefreshData()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        int soon = 0;
        var today = DateTime.Today;

        foreach (var p in Db.ListProps())
        {
            string lease = p.LeaseEnd;
            string days = "";
            bool warn = false;
            if (Ui.ParseDate(lease, out var d))
            {
                int left = (int)(d.ToDateTime(new TimeOnly()) - today).TotalDays;
                days = left.ToString();
                if (left <= 60) { warn = true; soon++; }
            }
            var rowIdx = grid.Rows.Add(p.Label, p.Tenant, Theme.Money(p.Rent),
                p.DueDay.ToString(), lease, days);
            var row = grid.Rows[rowIdx];
            row.Tag = p.Id;
            if (warn)
            {
                row.DefaultCellStyle.BackColor = Theme.WarnBg;
                row.DefaultCellStyle.ForeColor = Theme.Text;
                row.DefaultCellStyle.SelectionBackColor = Theme.Accent;
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
        }
        grid.ResumeLayout();

        warnLabel.Text = soon > 0
            ? $"🐾 {soon} lease(s) end within 60 days — time to chase a renewal or wave goodbye."
            : "No lease endings in the next 60 days. The cat is calm.";
    }
}

public class MoneyTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly Label summary = new()
    {
        Dock = DockStyle.Top,
        Height = 58,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 8, 4, 0),
    };

    public MoneyTab()
    {
        var addRent = Ui.Btn("Record rent", 120, (_, _) => AddTxn("rent"));
        var addExp = Ui.Btn("Add expense", 120, (_, _) => AddTxn("expense"));
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Date", 100));
        grid.Columns.Add(Ui.Col("Property", 190));
        grid.Columns.Add(Ui.Col("Type", 80));
        grid.Columns.Add(Ui.Col("Category", 130));
        grid.Columns.Add(Ui.Col("Amount", 110, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Note", 260));

        var bar = Ui.TopBar(addRent, addExp, del);

        Controls.Add(grid);
        Controls.Add(bar);
        Controls.Add(summary);

        grid.CellDoubleClick += (_, _) => DeleteSelected(); // simple; no inline editing in v1
    }

    void AddTxn(string kind)
    {
        using var dlg = new TxnDialog(kind, Db.ListProps());
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.T != null)
        {
            Db.SaveTxn(dlg.T);
            RefreshData();
        }
    }

    void DeleteSelected()
    {
        if (grid.SelectedRows.Count == 0) return;
        if (grid.SelectedRows[0].Tag is not long id) return;
        if (MessageBox.Show("Delete this entry?", "Delete entry",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.DeleteTxn(id);
        RefreshData();
    }

    public void RefreshData()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        var txns = Db.ListTxns();

        var now = DateTime.Today;
        string monthKey = now.ToString("yyyy-MM");
        string yearKey = now.ToString("yyyy");

        decimal rentM = 0, expM = 0, rentY = 0, expY = 0;
        foreach (var t in txns)
        {
            if (t.Date.Length >= 7 && t.Date[..7] == monthKey)
            {
                if (t.Kind == "rent") rentM += t.Amount; else expM += t.Amount;
            }
            if (t.Date.Length >= 4 && t.Date[..4] == yearKey)
            {
                if (t.Kind == "rent") rentY += t.Amount; else expY += t.Amount;
            }
        }

        summary.Text = $"This month:  in {Theme.Money(rentM)}   ·   out {Theme.Money(expM)}   ·   profit {Theme.Money(rentM - expM)}\n" +
                       $"This year ({yearKey}):  in {Theme.Money(rentY)}   ·   out {Theme.Money(expY)}   ·   profit {Theme.Money(rentY - expY)}";

        foreach (var t in txns)
        {
            var sign = t.Kind == "rent" ? "+" : "−";
            var rowIdx = grid.Rows.Add(t.Date, t.PropLabel,
                t.Kind == "rent" ? "Rent" : "Expense",
                t.Category, sign + Theme.Money(t.Amount), t.Note);
            var row = grid.Rows[rowIdx];
            row.Tag = t.Id;
            if (t.Kind == "rent")
            {
                row.Cells[4].Style.ForeColor = Theme.Good;
            }
        }
        grid.ResumeLayout();
    }
}

public class RequestsTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly Label hint = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 4, 4, 0),
        Tag = "muted",
    };

    public RequestsTab()
    {
        var add = Ui.Btn("Add request", 120, (_, _) => Add());
        var toggle = Ui.Btn("Open / Done", 120, (_, _) => Toggle());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Date", 100));
        grid.Columns.Add(Ui.Col("Property", 190));
        grid.Columns.Add(Ui.Col("Type", 110));
        grid.Columns.Add(Ui.Col("Description", 320));
        grid.Columns.Add(Ui.Col("Status", 90));

        var bar = Ui.TopBar(add, toggle, del);

        Controls.Add(grid);
        Controls.Add(bar);
        Controls.Add(hint);
    }

    void Add()
    {
        using var dlg = new ReqDialog(Db.ListProps());
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.Q != null)
        {
            Db.SaveReq(dlg.Q);
            RefreshData();
        }
    }

    void Toggle()
    {
        if (grid.SelectedRows.Count == 0) return;
        if (grid.SelectedRows[0].Tag is long id)
        {
            var q = Db.ListReqs().FirstOrDefault(x => x.Id == id);
            if (q != null)
            {
                Db.SetReqDone(id, !q.Done);
                RefreshData();
            }
        }
    }

    void DeleteSelected()
    {
        if (grid.SelectedRows.Count == 0) return;
        if (grid.SelectedRows[0].Tag is not long id) return;
        if (MessageBox.Show("Delete this request?", "Delete request",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.DeleteReq(id);
        RefreshData();
    }

    public void RefreshData()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        int open = 0;
        foreach (var q in Db.ListReqs())
        {
            if (!q.Done) open++;
            var rowIdx = grid.Rows.Add(q.Created, q.PropLabel,
                q.Kind == "viewing" ? "Viewing" : "Maintenance",
                q.Description, q.Done ? "Done" : "Open");
            var row = grid.Rows[rowIdx];
            row.Tag = q.Id;
            if (!q.Done)
                row.Cells[4].Style.ForeColor = Theme.Accent;
        }
        grid.ResumeLayout();
        hint.Text = open > 0
            ? $"{open} open request(s) waiting on you. 🐾"
            : "Nothing open. Purr.";
    }
}
