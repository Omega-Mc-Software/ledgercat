using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace LedgerCat;

public class ColDef
{
    public string Key = "";
    public string Header = "";
    public int Width = 120;
    public bool Core; // core: reorder + hide only. user: rename, move, delete
    public bool Visible = true;
    public int DisplayIndex;
}

public class GridLayout
{
    public List<ColDef> Columns { get; set; } = new();
}

public static class ColStore
{
    static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };

    public static readonly string[] PropCoreKeys =
    {
        "property", "tenant", "contact", "pets", "due_day", "rent_this_month", "last_month",
        "security_dep", "rent", "pet_fee", "late_fee", "total_due", "lease_ends", "days_left", "notes",
    };

    public static readonly Dictionary<string, (string header, int width)> PropCoreMeta = new()
    {
        ["property"] = ("Property", 200),
        ["tenant"] = ("Tenant", 140),
        ["contact"] = ("Contact", 140),
        ["pets"] = ("Pets", 85),
        ["due_day"] = ("Due day", 70),
        ["rent_this_month"] = ("Rent this month", 110),
        ["last_month"] = ("Last month", 90),
        ["security_dep"] = ("Security dep", 95),
        ["rent"] = ("Rent", 85),
        ["pet_fee"] = ("Pet fee", 80),
        ["late_fee"] = ("Late fee", 80),
        ["total_due"] = ("Total due/mo", 95),
        ["lease_ends"] = ("Lease ends", 100),
        ["days_left"] = ("Days left", 80),
        ["notes"] = ("Notes", 70),
    };

    public static readonly string[] MoneyCoreKeys =
    {
        "date", "property", "type", "category", "amount", "note",
    };
    public static readonly Dictionary<string, (string header, int width)> MoneyCoreMeta = new()
    {
        ["date"] = ("Date", 100),
        ["property"] = ("Property", 180),
        ["type"] = ("Type", 80),
        ["category"] = ("Category", 120),
        ["amount"] = ("Amount", 105),
        ["note"] = ("Note", 240),
    };

    public static readonly string[] ReqCoreKeys =
    {
        "date", "property", "type", "description", "contact", "handyman", "status", "retry", "notes",
    };
    public static readonly Dictionary<string, (string header, int width)> ReqCoreMeta = new()
    {
        ["date"] = ("Date", 95),
        ["property"] = ("Property", 160),
        ["type"] = ("Type", 100),
        ["description"] = ("Description", 230),
        ["contact"] = ("Contact", 130),
        ["handyman"] = ("Handyman", 130),
        ["status"] = ("Status", 85),
        ["retry"] = ("Retry later", 95),
        ["notes"] = ("Notes", 220),
    };

    public static readonly string[] WaitCoreKeys =
    {
        "added", "name", "phone", "email", "desired", "status", "notes",
    };
    public static readonly Dictionary<string, (string header, int width)> WaitCoreMeta = new()
    {
        ["added"] = ("Added", 100),
        ["name"] = ("Name", 160),
        ["phone"] = ("Phone", 130),
        ["email"] = ("Email", 180),
        ["desired"] = ("Desired", 140),
        ["status"] = ("Status", 90),
        ["notes"] = ("Notes", 220),
    };

    public static GridLayout Load(string settingKey, string[] coreKeys, Dictionary<string, (string header, int width)> meta)
    {
        var raw = Db.GetSetting(settingKey, "");
        GridLayout? saved = null;
        if (raw.Length > 0)
        {
            try { saved = JsonSerializer.Deserialize<GridLayout>(raw, Opts); }
            catch { saved = null; }
        }

        var layout = new GridLayout();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (saved != null)
        {
            foreach (var c in saved.Columns.OrderBy(c => c.DisplayIndex))
            {
                if (c.Key.Length == 0) continue;
                if (coreKeys.Contains(c.Key))
                {
                    var m = meta[c.Key];
                    layout.Columns.Add(new ColDef
                    {
                        Key = c.Key,
                        Header = m.header,
                        Width = c.Width > 20 ? c.Width : m.width,
                        Core = true,
                        Visible = c.Visible,
                        DisplayIndex = layout.Columns.Count,
                    });
                    seen.Add(c.Key);
                }
                else if (c.Key.StartsWith("user_"))
                {
                    layout.Columns.Add(new ColDef
                    {
                        Key = c.Key,
                        Header = string.IsNullOrWhiteSpace(c.Header) ? c.Key : c.Header,
                        Width = c.Width > 20 ? c.Width : 120,
                        Core = false,
                        Visible = c.Visible,
                        DisplayIndex = layout.Columns.Count,
                    });
                    seen.Add(c.Key);
                }
            }
        }

        foreach (var k in coreKeys)
        {
            if (seen.Contains(k)) continue;
            var m = meta[k];
            layout.Columns.Add(new ColDef
            {
                Key = k,
                Header = m.header,
                Width = m.width,
                Core = true,
                Visible = true,
                DisplayIndex = layout.Columns.Count,
            });
        }

        Reindex(layout);
        return layout;
    }

    public static void Save(string settingKey, GridLayout layout)
    {
        Reindex(layout);
        Db.SetSetting(settingKey, JsonSerializer.Serialize(layout, Opts));
    }

    public static void Reindex(GridLayout layout)
    {
        for (int i = 0; i < layout.Columns.Count; i++)
            layout.Columns[i].DisplayIndex = i;
    }

    public static string NextUserKey(GridLayout layout)
    {
        int n = 1;
        var used = layout.Columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (used.Contains("user_" + n)) n++;
        return "user_" + n;
    }

    public static bool HeaderTaken(GridLayout layout, string header, string? exceptKey = null)
    {
        return layout.Columns.Any(c =>
            (exceptKey == null || !c.Key.Equals(exceptKey, StringComparison.OrdinalIgnoreCase))
            && c.Header.Equals(header, StringComparison.OrdinalIgnoreCase));
    }

    /// Duplicate name: rename the existing column to "name-old" (and -old2 if needed).
    public static void BumpOldName(GridLayout layout, string header)
    {
        var hit = layout.Columns.FirstOrDefault(c => c.Header.Equals(header, StringComparison.OrdinalIgnoreCase));
        if (hit == null) return;
        string baseName = header + "-old";
        string cand = baseName;
        int i = 2;
        while (layout.Columns.Any(c => c.Header.Equals(cand, StringComparison.OrdinalIgnoreCase)))
        {
            cand = baseName + i;
            i++;
        }
        hit.Header = cand;
    }

    public static DataGridViewColumn MakeColumn(ColDef def)
    {
        var align = DataGridViewContentAlignment.MiddleLeft;
        if (def.Key is "pets" or "due_day" or "rent_this_month" or "last_month" or "security_dep"
            or "rent" or "pet_fee" or "late_fee" or "total_due" or "lease_ends" or "days_left")
            align = DataGridViewContentAlignment.MiddleRight;

        var col = new DataGridViewTextBoxColumn
        {
            Name = def.Key,
            HeaderText = def.Header,
            Width = def.Width,
            Visible = def.Visible,
            MinimumWidth = def.Key == "property" ? 160 : 40,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Tag = def.Key,
            DefaultCellStyle = { Alignment = align },
        };
        if (def.Key == "notes")
            col.ToolTipText = "📝 means this property has notes — double-click here to read or edit them";
        return col;
    }

    public static void WirePersist(DataGridView g, string settingKey, GridLayout layout)
    {
        void Persist()
        {
            foreach (DataGridViewColumn c in g.Columns)
            {
                var key = c.Name;
                var def = layout.Columns.FirstOrDefault(x => x.Key == key);
                if (def == null) continue;
                def.Width = c.Width;
                def.Visible = c.Visible;
                def.DisplayIndex = c.DisplayIndex;
            }
            layout.Columns = layout.Columns.OrderBy(x => x.DisplayIndex).ToList();
            Save(settingKey, layout);
        }

        g.ColumnWidthChanged += (_, _) => Persist();
        g.ColumnDisplayIndexChanged += (_, _) => Persist();
        g.ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            if (e.ColumnIndex < 0) return;
            ShowMenu(g, settingKey, layout, g.Columns[e.ColumnIndex]);
        };
    }

    static void ShowMenu(DataGridView g, string settingKey, GridLayout layout, DataGridViewColumn col)
    {
        var def = layout.Columns.FirstOrDefault(x => x.Key == col.Name);
        if (def == null) return;
        var m = new ContextMenuStrip();

        if (!def.Core)
        {
            m.Items.Add("Rename column…", null, (_, _) =>
            {
                string name = PromptName("Rename column", def.Header);
                if (name == null) return;
                name = name.Trim();
                if (name.Length == 0) return;
                if (HeaderTaken(layout, name, def.Key))
                {
                    var r = MessageBox.Show(
                        $"A column named \"{name}\" already exists.\nRename the old one to \"{name}-old\" and keep this name?",
                        "Duplicate column name", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r != DialogResult.Yes) return;
                    BumpOldName(layout, name);
                }
                def.Header = name;
                col.HeaderText = name;
                foreach (DataGridViewColumn c in g.Columns)
                {
                    var d = layout.Columns.FirstOrDefault(x => x.Key == c.Name);
                    if (d != null) c.HeaderText = d.Header;
                }
                Save(settingKey, layout);
                g.Invalidate();
                g.Refresh();
            });
            m.Items.Add("Delete column", null, (_, _) =>
            {
                if (MessageBox.Show($"Remove column \"{def.Header}\"? Values stored on properties are kept in the data file but hidden.",
                        "Delete column", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                layout.Columns.Remove(def);
                g.Columns.Remove(col);
                Save(settingKey, layout);
            });
        }
        else
        {
            m.Items.Add(def.Visible ? "Hide column" : "Show column", null, (_, _) =>
            {
                def.Visible = !def.Visible;
                col.Visible = def.Visible;
                Save(settingKey, layout);
            });
        }

        var show = new ToolStripMenuItem("Show hidden columns");
        foreach (var hidden in layout.Columns.Where(c => !c.Visible))
        {
            var h = hidden;
            show.DropDownItems.Add(h.Header, null, (_, _) =>
            {
                h.Visible = true;
                if (g.Columns[h.Key] != null) g.Columns[h.Key].Visible = true;
                Save(settingKey, layout);
            });
        }
        if (show.DropDownItems.Count == 0) show.Enabled = false;
        m.Items.Add(show);
        m.Show(Cursor.Position);
    }

    public static string? PromptName(string title, string seed)
    {
        using var f = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 120),
            MaximizeBox = false,
            MinimizeBox = false,
            Font = Theme.BaseFont,
        };
        var tb = new TextBox { Left = 16, Top = 16, Width = 328, Text = seed };
        var ok = Ui.Btn("OK", 90, (_, _) => f.DialogResult = DialogResult.OK);
        ok.Left = 160; ok.Top = 70;
        var cancel = Ui.Btn("Cancel", 90, (_, _) => f.DialogResult = DialogResult.Cancel);
        cancel.Left = 254; cancel.Top = 70;
        f.Controls.Add(tb);
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }

    public static ColDef AddUserColumn(GridLayout layout, string header)
    {
        if (HeaderTaken(layout, header))
        {
            var r = MessageBox.Show(
                $"A column named \"{header}\" already exists.\nRename the old one to \"{header}-old\" and keep this name?",
                "Duplicate column name", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) throw new OperationCanceledException();
            BumpOldName(layout, header);
        }
        var def = new ColDef
        {
            Key = NextUserKey(layout),
            Header = header,
            Width = 120,
            Core = false,
            Visible = true,
            DisplayIndex = layout.Columns.Count,
        };
        layout.Columns.Add(def);
        return def;
    }

    public static ColDef AddUserColumnNoPrompt(GridLayout layout, string header)
    {
        var def = new ColDef
        {
            Key = NextUserKey(layout),
            Header = header,
            Width = 120,
            Core = false,
            Visible = true,
            DisplayIndex = layout.Columns.Count,
        };
        layout.Columns.Add(def);
        return def;
    }

    public static void ReloadInto(DataGridView g, string settingKey, GridLayout layout, string[] coreKeys, Dictionary<string, (string header, int width)> meta)
    {
        var fresh = Load(settingKey, coreKeys, meta);
        layout.Columns = fresh.Columns;
        g.Columns.Clear();
        foreach (var def in layout.Columns.OrderBy(c => c.DisplayIndex))
            g.Columns.Add(MakeColumn(def));
    }
}
