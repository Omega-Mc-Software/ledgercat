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

    // v1.2.8 (StorageCat port, Dad): a typed date box with a calendar picker beside it.
    // Either one edits the same value — type it or pick it.
    public static Panel DateWithPicker(TextBox tb, int textWidth = 166)
    {
        bool syncing = false;
        var dtp = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 110,
            Left = textWidth + 6,
            Top = 1,
        };
        tb.Width = textWidth;
        tb.TextChanged += (_, _) =>
        {
            if (syncing) return;
            if (ParseDate(tb.Text, out var d))
            {
                syncing = true;
                dtp.Value = d.ToDateTime(TimeOnly.MinValue);
                syncing = false;
            }
        };
        dtp.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            tb.Text = dtp.Value.ToString("yyyy-MM-dd");
            syncing = false;
        };
        if (ParseDate(tb.Text, out var d0))
            dtp.Value = d0.ToDateTime(TimeOnly.MinValue);
        var p = new Panel { Width = textWidth + 6 + 110, Height = 27, Margin = new Padding(0) };
        p.Controls.Add(tb);
        p.Controls.Add(dtp);
        return p;
    }

    public static bool ParseMoney(string? s, out decimal v)
    {
        v = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return decimal.TryParse(s.Trim().Replace("$", "").Replace(",", ""),
            NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0;
    }

    /// Like ParseMoney but empty means 0 — for optional money fields (deposits, late fees).
    public static bool ParseMoneyOrZero(string? s, out decimal v)
    {
        v = 0;
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (!decimal.TryParse(s.Trim().Replace("$", "").Replace(",", ""),
            NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
        return v >= 0;
    }

    /// <summary>
    /// v1.2.1: full-text cell tooltips. DataGridView's built-in tooltip only appears on truncated cells
    /// and clips long text; this always shows the complete cell content.
    /// </summary>
    public static void FullTextTips(DataGridView g)
    {
        g.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                e.ToolTipText = g.Rows[e.RowIndex].Cells[e.ColumnIndex].FormattedValue.ToString() ?? "";
        };
    }

    /// <summary>
    /// v1.2.5 (Dad): clicking a highlighted (sole-selected) row again should let it go.
    /// The clear is queued so it runs AFTER the grid's own mouse-down selection logic.
    /// </summary>
    public static void ClickAgainClears(DataGridView g)
    {
        g.CellMouseDown += (_, e) =>
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Left) return;
            if (g.Rows[e.RowIndex].Selected && g.SelectedRows.Count == 1)
                g.BeginInvoke(new Action(g.ClearSelection));
        };
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
        var recRent = Ui.Btn("Record rent", 120, (_, _) => RecordRentForSelected());
        var del = Ui.Btn("Delete", 90, (_, _) => DeleteSelected());

        // v1.2.4 (Dad): column order for the front desk — who lives here, how to reach them, pets,
        // then what they owe right now (due day, this month's status, deposit), then the breakdown.
        // v1.2.5 (Dad): the Property column is the stretcher now — a Fill column at the END made
        // the horizontal scrollbar stop short of the notes box, so Notes became a plain fixed column.
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Property",
            Width = 200,
            MinimumWidth = 200,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        grid.Columns.Add(Ui.Col("Tenant", 140));
        grid.Columns.Add(Ui.Col("Contact", 140));
        grid.Columns.Add(Ui.Col("Pets", 85, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Due day", 70, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Rent this month", 110, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Last month", 90, DataGridViewContentAlignment.MiddleRight)); // v1.2.8 (Dad): last month's rent, ported from StorageCat
        grid.Columns.Add(Ui.Col("Security dep", 95, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Rent", 85, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Pet fee", 80, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Late fee", 80, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Total due/mo", 95, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Lease ends", 100, DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Ui.Col("Days left", 80, DataGridViewContentAlignment.MiddleRight));
        // v1.2.4 (Dad): a little box that says "this property has notes"
        // v1.2.5 (Dad): fixed width — the old Fill mode made scrolling right stop before it
        // v1.2.8 (Dad): double-click it to edit the notes right there
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Notes",
            Width = 70,
            ToolTipText = "📝 means this property has notes — double-click here to read or edit them",
        });

        delGrid.Columns.Add(Ui.Col("Property", 220));
        delGrid.Columns.Add(Ui.Col("Tenant", 160));
        delGrid.Columns.Add(Ui.Col("Total due/mo", 100, DataGridViewContentAlignment.MiddleRight));
        delGrid.Columns.Add(Ui.Col("Lease ends", 110, DataGridViewContentAlignment.MiddleRight));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());

        var bar = Ui.TopBar(va, vd, add, editB, recRent, del, undo, purge);
        // hide deleted-view actions until that view is open
        foreach (var c in new[] { undo, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(warnLabel);

        grid.CellDoubleClick += (s, e) =>
        {
            // v1.2.8 (Dad): double-click the 📝 to edit the note right from the grid — no full dialog.
            // Notes is column 14 (Last month was inserted at 6).
            if (e.ColumnIndex == 14 && e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is long id)
                QuickEditNotes(id);
            else EditSelected();
        };
        Ui.ClickAgainClears(grid); // v1.2.5 (Dad): clicking a highlighted row again lets it go
        Ui.ClickAgainClears(delGrid);

        Ui.FullTextTips(grid);
        Ui.FullTextTips(delGrid);
    }

    void RecordRentForSelected()
    {
        if (Ui.SelectedId(grid) is not long id) return;
        var p = Db.ListProps().FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        using var dlg = new TxnDialog("rent", Db.ListProps(), null, p);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.T != null)
        {
            Db.SaveTxn(dlg.T);
            RefreshData();
        }
    }

    // v1.2.8 (Dad): double-click the 📝 cell — write the note without opening the whole property.
    void QuickEditNotes(long id)
    {
        var p = Db.ListProps().FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        using var dlg = new NoteDialog("Notes — " + p.Label, p.LeaseNotes);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Db.SetPropNotes(id, dlg.NoteText.Trim());
            RefreshData();
        }
    }

    void SetViewButtons(bool deletedView)
    {
        // v1.2.3 (Dad): deleted view shows only Undo delete + Delete forever; every other
        // button grays out so it can never act on the active tab's hidden selection.
        foreach (Control c in ((Control)Controls[1]).Controls)
            if (c is Button b)
            {
                if (b.Text == "Undo delete" || b.Text == "Delete forever") b.Visible = deletedView;
                else if (b.Text != "Active" && b.Text != "Deleted") b.Enabled = !deletedView;
            }
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
        string monthKey = today.ToString("yyyy-MM");

        // v1.2.1: rent tracking — which properties already have a rent entry for the current month
        var paidIds = Db.ListTxns()
            .Where(t => t.Kind == "rent" && t.PropertyId != null && t.Date.StartsWith(monthKey))
            .Select(t => t.PropertyId!.Value)
            .ToHashSet();
        var allTxns = Db.ListTxns(); // v1.2.8 (Dad): last-month status needs the full rent history

        foreach (var p in Db.ListProps())
        {
            string lease = p.LeaseEnd;
            string days = "";
            bool warn = false;
            bool expired = false;
            if (Ui.ParseDate(lease, out var d))
            {
                int left = (int)(d.ToDateTime(new TimeOnly()) - today).TotalDays;
                days = left.ToString();
                if (left <= 60) { warn = true; soon++; }
                if (left < 0) expired = true;
            }
            string contact = p.ContactPhone.Length > 0
                ? (p.ContactName.Length > 0 ? $"{p.ContactName} · {p.ContactPhone}" : p.ContactPhone)
                : p.ContactName;

            // rent status for the current month (only when the property asks to be tracked)
            string rentStatus = "—";
            bool late = false, paid = false;
            if (p.TrackRent && p.TotalRentDue > 0)
            {
                if (paidIds.Contains(p.Id)) { rentStatus = "Paid ✓"; paid = true; }
                else if (today.Day > p.DueDay)
                {
                    // v1.2.6 (Dad): late rows show the actual number due, late fee included
                    rentStatus = p.LateFee > 0
                        ? $"LATE {Theme.Money(p.TotalRentDue + p.LateFee)} (+{p.LateFee:0.##} fee)"
                        : $"LATE {Theme.Money(p.TotalRentDue)}";
                    late = true;
                }
                else rentStatus = Theme.Money(p.TotalRentDue); // v1.2.5 (Dad): show what's owed, not the due day
            }

            // v1.2.8 (Dad): last month's rent status — a fresh move-in (LeaseStart set) never owes last month
            string prev = Billing.PrevMonthStatus(p, allTxns, today);
            string prevCell = prev.Length > 0 ? prev : "—";

            string pets = !p.PetsOk ? "No"
                : p.PetCount > 0 ? $"Yes ({p.PetCount})"
                : "Yes";

            var rowIdx = grid.Rows.Add(p.Label, p.Tenant, contact, pets,
                p.DueDay.ToString(), rentStatus, prevCell,
                p.SecurityDeposit > 0 ? Theme.Money(p.SecurityDeposit) : "—",
                Theme.Money(p.Rent),
                p.PetRent > 0 ? Theme.Money(p.PetRent) : "—",
                p.LateFee > 0 ? Theme.Money(p.LateFee) : "—",
                Theme.Money(p.TotalRentDue),
                lease, days,
                p.LeaseNotes.Length > 0 ? "📝" : "");
            var row = grid.Rows[rowIdx];
            row.Tag = p.Id;
            if (paid)
            {
                // v1.2.5 (Dad): a paid-up property glows light green across the whole row
                row.Cells[5].Style.ForeColor = Theme.Good;
                row.DefaultCellStyle.BackColor = Theme.GoodBg;
                row.DefaultCellStyle.SelectionBackColor = Theme.Accent;
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else if (late) row.Cells[5].Style.ForeColor = Theme.Danger;
            // v1.2.8 (Dad): last month gets its own traffic light — green paid, red owed
            if (prev == "Paid") row.Cells[6].Style.ForeColor = Theme.Good;
            else if (prev == "DUE") row.Cells[6].Style.ForeColor = Theme.Danger;
            if (warn && !paid)
            {
                // v1.2: expiring leases glow pink; already-expired ones go deeper red-pink.
                row.DefaultCellStyle.BackColor = expired ? Theme.PinkDeep : Theme.PinkBg;
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
            var rowIdx = delGrid.Rows.Add(p.Label, p.Tenant, Theme.Money(p.TotalRentDue), p.LeaseEnd);
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
        grid.Columns.Add(Ui.Col("Property", 180)); // v1.2.5: Property stretches instead of the Note column
        grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns[1].MinimumWidth = 180;
        grid.Columns.Add(Ui.Col("Type", 80));
        grid.Columns.Add(Ui.Col("Category", 120));
        grid.Columns.Add(Ui.Col("Amount", 105, DataGridViewContentAlignment.MiddleRight));
        // v1.2.5 (Dad): fixed width — a Fill column at the end made the scrollbar stop short of it
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Note",
            Width = 240,
        });

        delGrid.Columns.Add(Ui.Col("Date", 100));
        delGrid.Columns.Add(Ui.Col("Property", 180));
        delGrid.Columns.Add(Ui.Col("Type", 80));
        delGrid.Columns.Add(Ui.Col("Category", 120));
        delGrid.Columns.Add(Ui.Col("Amount", 105, DataGridViewContentAlignment.MiddleRight));
        delGrid.Columns.Add(Ui.Col("Note", 240));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());
        foreach (var c in new[] { undo, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var searchLbl = new Label { Text = "Search:", AutoSize = true, Padding = new Padding(6, 10, 2, 0) };
        searchT.PlaceholderText = "note, category, property…";
        searchT.TextChanged += (_, _) => RefreshData();
        catC.Items.Add("(all categories)");
        catC.SelectedIndex = 0;
        catC.SelectedIndexChanged += (_, _) => { if (!catUpdating) RefreshData(); };

        var bar = Ui.TopBar(va, vd, addRent, addExp, editB, del, undo, purge, searchLbl, searchT, catC);
        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(summary);

        grid.CellDoubleClick += (s, e) =>
        {
            // v1.2.8 (Dad): double-click the Note cell to edit it right from the grid.
            // Still edit, not delete — deletes are deliberate.
            if (e.ColumnIndex == 5 && e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is long id)
                QuickEditTxnNote(id);
            else EditSelected();
        };
        Ui.ClickAgainClears(grid); // v1.2.5 (Dad): clicking a highlighted row again lets it go
        Ui.ClickAgainClears(delGrid);

        Ui.FullTextTips(grid);
        Ui.FullTextTips(delGrid);
    }

    void SetViewButtons(bool deletedView)
    {
        // v1.2.3 (Dad): deleted view shows only Undo delete + Delete forever; every other
        // button grays out so it can never act on the active tab's hidden selection.
        foreach (Control c in ((Control)Controls[1]).Controls)
            if (c is Button b)
            {
                if (b.Text == "Undo delete" || b.Text == "Delete forever") b.Visible = deletedView;
                else if (b.Text != "Active" && b.Text != "Deleted") b.Enabled = !deletedView;
            }
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

    // v1.2.8 (Dad): double-click the Note cell — fix a typo without opening the whole entry.
    void QuickEditTxnNote(long id)
    {
        var t = Db.ListTxns().FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        using var dlg = new NoteDialog("Note — " + t.PropLabel + ", " + t.Date, t.Note);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Db.SetTxnNote(id, dlg.NoteText.Trim());
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
        grid.Columns.Add(Ui.Col("Description", 230)); // v1.2.5: Description stretches instead of Notes
        grid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns[3].MinimumWidth = 230;
        grid.Columns.Add(Ui.Col("Contact", 130));
        grid.Columns.Add(Ui.Col("Handyman", 130));
        grid.Columns.Add(Ui.Col("Status", 85));
        grid.Columns.Add(Ui.Col("Retry later", 95));
        // v1.2.5 (Dad): fixed width — a Fill column at the end made the scrollbar stop short of it
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Notes",
            Width = 220,
        });

        delGrid.Columns.Add(Ui.Col("Date", 100));
        delGrid.Columns.Add(Ui.Col("Property", 170));
        delGrid.Columns.Add(Ui.Col("Type", 100));
        delGrid.Columns.Add(Ui.Col("Description", 280));
        delGrid.Columns.Add(Ui.Col("Status", 85));
        delGrid.Columns.Add(Ui.Col("Notes", 200));

        var undo = Ui.Btn("Undo delete", 120, (_, _) => UndoSelected());
        var purge = Ui.Btn("Delete forever", 130, (_, _) => PurgeSelected());
        foreach (var c in new[] { undo, purge }) c.Visible = false;
        va.Click += (_, _) => SetViewButtons(false);
        vd.Click += (_, _) => SetViewButtons(true);

        var bar = Ui.TopBar(va, vd, add, editB, toggle, cancelB, del, undo, purge);
        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(delGrid);
        gridPanel.Controls.Add(grid);
        delGrid.Visible = false;

        Controls.Add(gridPanel);
        Controls.Add(bar);
        Controls.Add(hint);

        grid.CellDoubleClick += (s, e) =>
        {
            // v1.2.8 (Dad): double-click the Notes cell to edit notes right from the grid.
            // Notes is column 8. The cancel reason stays on the request — this edits only notes.
            if (e.ColumnIndex == 8 && e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is long id)
                QuickEditReqNotes(id);
            else EditSelected();
        };
        Ui.ClickAgainClears(grid); // v1.2.5 (Dad): clicking a highlighted row again lets it go
        Ui.ClickAgainClears(delGrid);

        Ui.FullTextTips(grid);
        Ui.FullTextTips(delGrid);
    }

    void SetViewButtons(bool deletedView)
    {
        // v1.2.3 (Dad): deleted view shows only Undo delete + Delete forever; every other
        // button grays out so it can never act on the active tab's hidden selection.
        foreach (Control c in ((Control)Controls[1]).Controls)
            if (c is Button b)
            {
                if (b.Text == "Undo delete" || b.Text == "Delete forever") b.Visible = deletedView;
                else if (b.Text != "Active" && b.Text != "Deleted") b.Enabled = !deletedView;
            }
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
            dlg.Q.CancelReason = q.CancelReason; // kept from the cancel popup; edited status only via Open/Done / Cancel req
            Db.SaveReq(dlg.Q);
            RefreshData();
        }
    }

    // v1.2.8 (Dad): double-click the Notes cell — jot it down without opening the whole request.
    void QuickEditReqNotes(long id)
    {
        var q = Db.ListReqs().FirstOrDefault(x => x.Id == id);
        if (q == null) return;
        using var dlg = new NoteDialog("Notes — " + q.PropLabel + ", " + q.Created, q.Notes);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Db.SetReqNotes(id, dlg.NoteText.Trim());
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
            Db.SetReqStatus(id, "open"); // un-cancel (cancel reason stays on the request)
        }
        else
        {
            // v1.2.1: canceling asks why — the reason shows in the Notes column
            using var dlg = new CancelDialog(q.Description);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            q.Status = "canceled";
            q.CancelReason = dlg.Reason;
            Db.SaveReq(q);
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
                status,
                q.RetryLater,
                NotesCell(q));
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
                q.Description, status, NotesCell(q));
            delGrid.Rows[rowIdx].Tag = q.Id;
        }
        delGrid.ResumeLayout();

        hint.Text = open > 0
            ? $"{open} open request(s) waiting on you. 🐾"
            : "Nothing open. Purr.";

        viewDeletedBtn.Text = $"Deleted ({Db.ListReqsDeleted().Count})";
    }

    static string NotesCell(Req q)
    {
        var notes = q.Notes;
        if (q.Status == "canceled" && q.CancelReason.Length > 0)
            notes = notes.Length > 0 ? notes + " · Cancel reason: " + q.CancelReason
                                     : "Cancel reason: " + q.CancelReason;
        return notes;
    }
}
