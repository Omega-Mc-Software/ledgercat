using System;
using System.Windows.Forms;

namespace LedgerCat;

public class MainForm : Form
{
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly PropertiesTab propsTab = new();
    readonly MoneyTab moneyTab = new();
    readonly RequestsTab reqTab = new();
    readonly ImportExportTab ioTab = new();
    readonly AboutTab aboutTab = new();
    readonly Button themeBtn = new() { Dock = DockStyle.Right, Width = 150 };
    readonly Label title = new()
    {
        Text = "🐾 LedgerCat",
        Dock = DockStyle.Left,
        Width = 230,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        Tag = "head",
        Font = Theme.HeadFont,
    };

    public MainForm()
    {
        Text = "LedgerCat v1.2.2 — by Neko Omega";
        Width = 1120;
        Height = 720;
        MinimumSize = new System.Drawing.Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.BaseFont;

        var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10, 8, 10, 8) };
        themeBtn.Click += (_, _) => Theme.Toggle();
        top.Controls.Add(themeBtn);
        top.Controls.Add(title);

        AddTab("Properties & Units", propsTab);
        AddTab("Money In / Out", moneyTab);
        AddTab("Requests", reqTab);
        AddTab("Import / Export", ioTab);
        AddTab("About", aboutTab);

        Controls.Add(tabs);
        Controls.Add(top);

        ioTab.DataChanged += RefreshAll;
        Theme.Changed += ApplyTheme;

        Load += (_, _) =>
        {
            RefreshAll();
            ApplyTheme();
        };
    }

    void AddTab(string titleText, UserControl uc)
    {
        uc.Dock = DockStyle.Fill;
        var page = new TabPage(titleText);
        page.Controls.Add(uc);
        tabs.TabPages.Add(page);
    }

    void RefreshAll()
    {
        propsTab.RefreshData();
        moneyTab.RefreshData();
        reqTab.RefreshData();
    }

    void ApplyTheme()
    {
        Theme.Apply(this);
        themeBtn.Text = Theme.Dark ? "☀  Light mode" : "🌙  Dark mode";
        RefreshAll(); // re-applies data-dependent row colors (lease warnings, rent green)
    }
}
