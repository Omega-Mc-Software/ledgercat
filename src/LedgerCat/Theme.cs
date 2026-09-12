using System;
using System.Drawing;
using System.Windows.Forms;

namespace LedgerCat;

public static class Theme
{
    public static bool Dark;
    public static event Action? Changed;

    public static void LoadSaved() => Dark = Db.GetSetting("theme", "light") == "dark";

    public static void Toggle()
    {
        Dark = !Dark;
        Db.SetSetting("theme", Dark ? "dark" : "light");
        Changed?.Invoke();
    }

    // warm cat palette
    public static Color Back   => Dark ? Color.FromArgb(28, 25, 23)    : Color.FromArgb(245, 243, 239);
    public static Color Surface => Dark ? Color.FromArgb(41, 37, 34)    : Color.FromArgb(255, 255, 255);
    public static Color Input  => Dark ? Color.FromArgb(53, 48, 44)    : Color.FromArgb(251, 249, 246);
    public static Color Text   => Dark ? Color.FromArgb(237, 231, 223) : Color.FromArgb(31, 29, 27);
    public static Color Muted  => Dark ? Color.FromArgb(158, 150, 141) : Color.FromArgb(110, 105, 97);
    public static Color Accent => Dark ? Color.FromArgb(232, 151, 63)  : Color.FromArgb(186, 112, 38);
    public static Color WarnBg   => Dark ? Color.FromArgb(84, 55, 24)    : Color.FromArgb(250, 224, 186);
    public static Color PinkBg   => Dark ? Color.FromArgb(97, 47, 61)    : Color.FromArgb(252, 209, 223);
    public static Color PinkDeep => Dark ? Color.FromArgb(122, 38, 56)   : Color.FromArgb(247, 168, 190);
    public static Color Good   => Dark ? Color.FromArgb(120, 180, 120) : Color.FromArgb(46, 120, 68);
    public static Color Danger => Dark ? Color.FromArgb(236, 122, 133) : Color.FromArgb(176, 32, 58);
    // v1.2.5 (Dad): the light-green wash for a paid-up property row
    public static Color GoodBg => Dark ? Color.FromArgb(42, 70, 50) : Color.FromArgb(214, 240, 219);
    public static Color GridLine => Dark ? Color.FromArgb(62, 56, 51)  : Color.FromArgb(221, 216, 207);

    public static readonly Font BaseFont = new Font("Segoe UI", 9.75f);
    public static readonly Font HeadFont = new Font("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font SubHeadFont = new Font("Segoe UI", 12f, FontStyle.Bold);

    public static string Money(decimal d) => "$" + d.ToString("N2");

    public static void Apply(Control root) => ApplyInner(root);

    static void ApplyInner(Control c)
    {
        switch (c)
        {
            case DataGridView g:
                StyleGrid(g);
                break;
            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Accent;
                b.BackColor = Surface;
                b.ForeColor = Text;
                break;
            case TextBox tb:
                tb.BackColor = Input;
                tb.ForeColor = Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox cb:
                cb.BackColor = Input;
                cb.ForeColor = Text;
                cb.FlatStyle = FlatStyle.Flat;
                break;
            case DateTimePicker dtp:
                dtp.CalendarMonthBackground = Input;
                dtp.CalendarForeColor = Text;
                break;
            case Label l:
                var tag = l.Tag as string;
                l.ForeColor = tag == "head" ? Accent : tag == "muted" ? Muted : Text;
                l.BackColor = Color.Transparent;
                break;
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case TabPage:
            case System.Windows.Forms.Panel:
            case GroupBox:
                c.BackColor = Surface;
                c.ForeColor = Text;
                break;
            default:
                c.ForeColor = Text;
                break;
        }

        foreach (Control child in c.Controls)
            ApplyInner(child);
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Back;
        g.BorderStyle = BorderStyle.None;
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.GridColor = GridLine;

        g.ColumnHeadersDefaultCellStyle.BackColor = Dark ? Color.FromArgb(52, 47, 43) : Color.FromArgb(236, 232, 225);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Accent;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = BaseFont;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        g.ColumnHeadersHeight = 34;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        g.DefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.ForeColor = Text;
        g.DefaultCellStyle.SelectionBackColor = Accent;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.DefaultCellStyle.Font = BaseFont;

        g.AlternatingRowsDefaultCellStyle.BackColor = Dark ? Color.FromArgb(46, 42, 38) : Color.FromArgb(250, 248, 244);
        g.AlternatingRowsDefaultCellStyle.ForeColor = Text;
        g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Accent;
        g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
    }
}
