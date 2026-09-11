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

    /// Shared Active / Deleted view switcher. Returns the two buttons and a helper to apply a switch.
    public static (Button active, Button deleted, Action<bool> apply) ViewSwitch(DataGridView activeGrid, DataGridView deletedGrid)
    {
        var active = Btn("Active", 90);
        var deleted = Btn("Deleted", 110);

        void Apply(bool showDeleted)
        {
            active.Enabled = showDeleted;
            deleted.Enabled = !showDeleted;
            activeGrid.Visible = !showDeleted;
            deletedGrid.Visible = showDeleted;
        }
        active.Click += (_, _) => Apply(false);
        deleted.Click += (_, _) => Apply(true);
        Apply(false); // we always start on the active view
        return (active, deleted, Apply);
    }

    public static long? SelectedId(DataGridView grid)
    {
        if (grid.SelectedRows.Count == 0) return null;
        return grid.SelectedRows[0].Tag as long?;
    }

    public static bool ConfirmHardDelete(string what) =>
        MessageBox.Show($"Delete {what} forever?\n\nThis cannot be undone.",
            "Delete forever", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
}

public class PropertiesTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly DataGridView delGrid = Ui.MakeGrid();
    readonly Label warnLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 4, 4, 0),
        Tag = "muted",
    };
    readonly Button viewDeletedBtn;

    public PropertiesTab()
    {
        var (va, vd, _) = Ui.ViewSwitch(grid, delGrid);
        viewDeletedBtn = vd;

        var add = Ui.Btn("Add property", 120, (_, _) => Edit(null));
        var editB = Ui.Btn("Edit", 80, (_, _) => EditSelected());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Property", 200));
        grid.Columns.Add(Ui.Col("Tenant", 140));
        grid.Columns.Add(Ui.Col("Contact", 140));
        grid.Columns.Add(Ui.Col("Rent / month", 105, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Due day", 75, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Lease ends", 100, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Days left", 80, DataGridViewContentAlignment.MiddleRight));

        delGrid.Columns.Add(Ui.Col("Property", 220));
        delGrid.Columns.Add(Ui.Col("Tenant", 160));
        delGrid.Columns.Add(Ui.Col("Rent / month", 110, DataGridViewContentAlignment.MiddleRight));
        delGrid.Columns.Add(Ui.Col("Lease ends", 110, DataGridViewContentAlignment.MiddleRight));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var undoAll = Ui.Btn("Undo all", 100, (_, _) => UndoAll());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());

        var bar = Ui.TopBar(va, vd, add, editB, del, undo, undoAll, purge);
        // hide deleted-view actions until that view is open
        foreach (var c in new[] { undo, undoAll, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(warnLabel);

        grid.CellDoubleClick += (_, _) => EditSelected();
    }

    void SetViewButtons(bool deletedView)
    {
        foreach (Control c in ((Control)Controls[1]).Controls) // the top bar
            if (c is Button b && (b.Text == "Undo delete" || b.Text == "Undo all" || b.Text == "Delete forever"))
                b.Visible = deletedView;
    }

    void EditSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var p = Db.ListProps().FirstOrDefault(x => x.Id == id);
        if (p != null) Edit(p);
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
        if (Ui.SelectedId(grid) is not long id) return;
        if (MessageBox.Show("Move this property to the Deleted view?\n\nPast transactions are kept and the link is restored if you undo.",
                "Delete property", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.SetPropDeleted(id, true);
        RefreshData();
    }

    void UndoSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        Db.SetPropDeleted(id, false);
        RefreshData();
    }

    void UndoAll()
    {
        var n = Db.ListProps(deleted: true).Count;
        if (n == 0) return;
        if (MessageBox.Show($"Restore all {n} deleted propert(y/ies)?", "Undo all",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.RestoreAllProps();
        RefreshData();
    }

    void PurgeSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        var p = Db.ListProps(deleted: true).FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        if (!Ui.ConfirmHardDelete($"the property \"{p.Label}\"")) return;
        Db.PurgeProp(id);
        RefreshData();
    }

    public void RefreshData()
    {
        // ---- active ----
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
            string contact = p.ContactPhone.Length > 0
                ? (p.ContactName.Length > 0 ? $"{p.ContactName} · {p.ContactPhone}" : p.ContactPhone)
                : p.ContactName;
            var rowIdx = grid.Rows.Add(p.Label, p.Tenant, contact, Theme.Money(p.Rent),
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

        // ---- deleted ----
        delGrid.SuspendLayout();
        delGrid.Rows.Clear();
        foreach (var p in Db.ListProps(deleted: true))
        {
            var rowIdx = delGrid.Rows.Add(p.Label, p.Tenant, Theme.Money(p.Rent), p.LeaseEnd);
            delGrid.Rows[rowIdx].Tag = p.Id;
        }
        delGrid.ResumeLayout();

        viewDeletedBtn.Text = $"Deleted ({Db.ListProps(deleted: true).Count})";
    }
}

public class MoneyTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly DataGridView delGrid = Ui.MakeGrid();
    readonly Label summary = new()
    {
        Dock = DockStyle.Top,
        Height = 58,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 8, 4, 0),
    };
    readonly TextBox searchT = new() { Width = 180 };
    readonly ComboBox catC = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    bool catUpdating;
    readonly Button viewDeletedBtn;

    public MoneyTab()
    {
        var (va, vd, _) = Ui.ViewSwitch(grid, delGrid);
        viewDeletedBtn = vd;

        var addRent = Ui.Btn("Record rent", 120, (_, _) => AddTxn("rent"));
        var addExp = Ui.Btn("Add expense", 120, (_, _) => AddTxn("expense"));
        var editB = Ui.Btn("Edit", 80, (_, _) => EditSelected());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Date", 100));
        grid.Columns.Add(Ui.Col("Property", 180));
        grid.Columns.Add(Ui.Col("Type", 80));
        grid.Columns.Add(Ui.Col("Category", 120));
        grid.Columns.Add(Ui.Col("Amount", 105, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Note", 240));

        delGrid.Columns.Add(Ui.Col("Date", 100));
        delGrid.Columns.Add(Ui.Col("Property", 180));
        delGrid.Columns.Add(Ui.Col("Type", 80));
        delGrid.Columns.Add(Ui.Col("Category", 120));
        delGrid.Columns.Add(Ui.Col("Amount", 105, DataGridViewContentAlignment.MiddleRight));
        delGrid.Columns.Add(Ui.Col("Note", 240));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var undoAll = Ui.Btn("Undo all", 100, (_, _) => UndoAll());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());
        foreach (var c in new[] { undo, undoAll, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var searchLbl = new Label { Text = "Search:", AutoSize = true, Padding = new Padding(6, 10, 2, 0) };
        searchT.PlaceholderText = "note, category, property…";
        searchT.TextChanged += (_, _) => RefreshData();
        catC.Items.Add("(all categories)");
        catC.SelectedIndex = 0;
        catC.SelectedIndexChanged += (_, _) => { if (!catUpdating) RefreshData(); };

        var bar = Ui.TopBar(va, vd, addRent, addExp, editB, del, searchLbl, searchT, catC);
        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(summary);

        grid.CellDoubleClick += (_, _) => EditSelected(); // edit, not delete — deletes are deliberate
    }

    void SetViewButtons(bool deletedView)
    {
        foreach (Control c in ((Control)Controls[1]).Controls)
            if (c is Button b && (b.Text == "Undo delete" || b.Text == "Undo all" || b.Text == "Delete forever"))
                b.Visible = deletedView;
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

    void EditSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var t = Db.ListTxns().FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        using var dlg = new TxnDialog(t.Kind, Db.ListProps(), t);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.T != null)
        {
            dlg.T.Id = t.Id;
            Db.SaveTxn(dlg.T);
            RefreshData();
        }
    }

    void DeleteSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        if (MessageBox.Show("Move this entry to the Deleted view?\n\nYou can undo it there.",
                "Delete entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.SetTxnDeleted(id, true);
        RefreshData();
    }

    void UndoSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        Db.SetTxnDeleted(id, false);
        RefreshData();
    }

    void UndoAll()
    {
        var n = Db.ListTxnsDeleted().Count;
        if (n == 0) return;
        if (MessageBox.Show($"Restore all {n} deleted entr(y/ies)?", "Undo all",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.RestoreAllTxns();
        RefreshData();
    }

    void PurgeSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        if (!Ui.ConfirmHardDelete("this entry")) return;
        Db.PurgeTxn(id);
        RefreshData();
    }

    bool Matches(Txn t, string q)
    {
        if (q.Length == 0) return true;
        return t.Date.Contains(q, StringComparison.OrdinalIgnoreCase)
            || t.PropLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
            || t.Kind.Contains(q, StringComparison.OrdinalIgnoreCase)
            || t.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
            || t.Note.Contains(q, StringComparison.OrdinalIgnoreCase)
            || Theme.Money(t.Amount).Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    public void RefreshData()
    {
        grid.SuspendLayout();
        delGrid.SuspendLayout();
        grid.Rows.Clear();
        delGrid.Rows.Clear();

        var all = Db.ListTxns();
        var q = searchT.Text.Trim();
        string? cat = catC.SelectedIndex > 0 ? catC.SelectedItem?.ToString() : null;
        var txns = all.Where(t => Matches(t, q) && (cat == null || t.Category == cat)).ToList();

        var now = DateTime.Today;
        string monthKey = now.ToString("yyyy-MM");
        string yearKey = now.ToString("yyyy");

        decimal rentM = 0, expM = 0, rentY = 0, expY = 0;
        foreach (var t in all) // summary always reflects reality, not the filter
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

        foreach (var t in Db.ListTxnsDeleted())
        {
            var sign = t.Kind == "rent" ? "+" : "−";
            var rowIdx = delGrid.Rows.Add(t.Date, t.PropLabel,
                t.Kind == "rent" ? "Rent" : "Expense",
                t.Category, sign + Theme.Money(t.Amount), t.Note);
            delGrid.Rows[rowIdx].Tag = t.Id;
        }
        delGrid.ResumeLayout();

        // category dropdown refresh (keep current selection; don't re-trigger refresh)
        string? current = catC.SelectedIndex > 0 ? catC.SelectedItem?.ToString() : null;
        catUpdating = true;
        catC.Items.Clear();
        catC.Items.Add("(all categories)");
        foreach (var c in all.Select(t => t.Category).Where(c => c.Length > 0).Distinct().OrderBy(c => c))
            catC.Items.Add(c);
        int idx = current != null ? catC.Items.IndexOf(current) : 0;
        catC.SelectedIndex = idx < 0 ? 0 : idx;
        catUpdating = false;

        viewDeletedBtn.Text = $"Deleted ({Db.ListTxnsDeleted().Count})";
    }
}

public class RequestsTab : UserControl
{
    readonly DataGridView grid = Ui.MakeGrid();
    readonly DataGridView delGrid = Ui.MakeGrid();
    readonly Label hint = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 4, 4, 0),
        Tag = "muted",
    };
    readonly Button viewDeletedBtn;

    public RequestsTab()
    {
        var (va, vd, _) = Ui.ViewSwitch(grid, delGrid);
        viewDeletedBtn = vd;

        var add = Ui.Btn("Add request", 120, (_, _) => Add());
        var editB = Ui.Btn("Edit", 80, (_, _) => EditSelected());
        var toggle = Ui.Btn("Open / Done", 120, (_, _) => Toggle());
        var cancelB = Ui.Btn("Cancel req", 110, (_, _) => CancelReq());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        grid.Columns.Add(Ui.Col("Date", 95));
        grid.Columns.Add(Ui.Col("Property", 160));
        grid.Columns.Add(Ui.Col("Type", 100));
        grid.Columns.Add(Ui.Col("Description", 230));
        grid.Columns.Add(Ui.Col("Contact", 130));
        grid.Columns.Add(Ui.Col("Handyman", 130));
        grid.Columns.Add(Ui.Col("Status", 85));

        delGrid.Columns.Add(Ui.Col("Date", 100));
        delGrid.Columns.Add(Ui.Col("Property", 170));
        delGrid.Columns.Add(Ui.Col("Type", 100));
        delGrid.Columns.Add(Ui.Col("Description", 280));
        delGrid.Columns.Add(Ui.Col("Status", 85));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var undoAll = Ui.Btn("Undo all", 100, (_, _) => UndoAll());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());
        foreach (var c in new[] { undo, undoAll, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var bar = Ui.TopBar(va, vd, add, editB, toggle, cancelB, del);
        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(hint);

        grid.CellDoubleClick += (_, _) => EditSelected();
    }

    void SetViewButtons(bool deletedView)
    {
        foreach (Control c in ((Control)Controls[1]).Controls)
            if (c is Button b && (b.Text == "Undo delete" || b.Text == "Undo all" || b.Text == "Delete forever"))
                b.Visible = deletedView;
    }

    static string ContactCell(string name, string phone)
    {
        if (name.Length > 0 && phone.Length > 0) return $"{name} · {phone}";
        return name.Length > 0 ? name : phone;
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

    void EditSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var q = Db.ListReqs().FirstOrDefault(x => x.Id == id);
        if (q == null) return;
        using var dlg = new ReqDialog(Db.ListProps(), q);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.Q != null)
        {
            dlg.Q.Id = q.Id;
            dlg.Q.Status = q.Status;
            Db.SaveReq(dlg.Q);
            RefreshData();
        }
    }

    void Toggle()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var q = Db.ListReqs().FirstOrDefault(x => x.Id == id);
        if (q == null) return;
        Db.SetReqStatus(id, q.Status == "open" ? "done" : "open");
        RefreshData();
    }

    void CancelReq()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var q = Db.ListReqs().FirstOrDefault(x => x.Id == id);
        if (q == null) return;
        if (q.Status == "canceled")
        {
            Db.SetReqStatus(id, "open"); // un-cancel
        }
        else
        {
            if (MessageBox.Show("Mark this request as canceled?\n\nIt stays on the list (so you remember why) but nothing waits on it.",
                    "Cancel request", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Db.SetReqStatus(id, "canceled");
        }
        RefreshData();
    }

    void DeleteSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        if (MessageBox.Show("Move this request to the Deleted view?\n\nYou can undo it there.",
                "Delete request", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.SetReqDeleted(id, true);
        RefreshData();
    }

    void UndoSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        Db.SetReqDeleted(id, false);
        RefreshData();
    }

    void UndoAll()
    {
        var n = Db.ListReqsDeleted().Count;
        if (n == 0) return;
        if (MessageBox.Show($"Restore all {n} deleted request(s)?", "Undo all",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Db.RestoreAllReqs();
        RefreshData();
    }

    void PurgeSelected()
    {
        if (Ui.SelectedId(delGrid) is not long id) return;
        if (!Ui.ConfirmHardDelete("this request")) return;
        Db.PurgeReq(id);
        RefreshData();
    }

    public void RefreshData()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        int open = 0;
        foreach (var q in Db.ListReqs())
        {
            if (q.Status == "open") open++;
            string status = q.Status switch
            {
                "open" => "Open",
                "done" => "Done",
                _ => "Canceled",
            };
            var rowIdx = grid.Rows.Add(q.Created, q.PropLabel,
                q.Kind == "viewing" ? "Viewing" : "Maintenance",
                q.Description,
                ContactCell(q.ContactName, q.ContactPhone),
                ContactCell(q.HandymanName, q.HandymanPhone),
                status);
            var row = grid.Rows[rowIdx];
            row.Tag = q.Id;
            if (q.Status == "open")
                row.Cells[6].Style.ForeColor = Theme.Accent;
            else if (q.Status == "canceled")
                row.Cells[6].Style.ForeColor = Theme.Muted;
        }
        grid.ResumeLayout();

        delGrid.SuspendLayout();
        delGrid.Rows.Clear();
        foreach (var q in Db.ListReqsDeleted())
        {
            string status = q.Status switch
            {
                "open" => "Open",
                "done" => "Done",
                _ => "Canceled",
            };
            var rowIdx = delGrid.Rows.Add(q.Created, q.PropLabel,
                q.Kind == "viewing" ? "Viewing" : "Maintenance",
                q.Description, status);
            delGrid.Rows[rowIdx].Tag = q.Id;
        }
        delGrid.ResumeLayout();

        hint.Text = open > 0
            ? $"{open} open request(s) waiting on you. 🐾"
            : "Nothing open. Purr.";

        viewDeletedBtn.Text = $"Deleted ({Db.ListReqsDeleted().Count})";
    }
}
