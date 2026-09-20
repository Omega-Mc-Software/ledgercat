using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LedgerCat;

public class MainForm : Form
{
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly Dictionary<TabPage, Action> tabRefresh = new();
    readonly PropertiesTab propsTab = new();
    readonly MoneyTab moneyTab = new();
    readonly RequestsTab reqTab = new();
    readonly ImportExportTab ioTab = new();
    readonly AboutTab aboutTab = new();
    readonly WaitlistTab waitTab = new();
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
        Text = "LedgerPaw v1.3.0 — by Neko Omega";
        Width = 1120;
        Height = 720;
        MinimumSize = new System.Drawing.Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.BaseFont;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10, 8, 10, 8) };
        themeBtn.Click += (_, _) => Theme.Toggle();
        top.Controls.Add(themeBtn);
        top.Controls.Add(title);

        AddTab("Properties & Units", propsTab);
        AddTab("Money In / Out", moneyTab);
        AddTab("Requests", reqTab);
        AddTab("Waitlist", waitTab);
        AddTab("Import / Export", ioTab);
        AddTab("About", aboutTab);

        Controls.Add(tabs);
        Controls.Add(top);

        // v1.2.7 (Dad): a tab always shows the freshest data — recording rent on the Money tab
        // now updates the Properties page the moment you switch over, no edit/reopen needed
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab != null && tabRefresh.TryGetValue(tabs.SelectedTab, out var refresh))
                refresh();
        };

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
        if (uc is PropertiesTab p) tabRefresh[page] = p.RefreshData;
        else if (uc is MoneyTab m) tabRefresh[page] = m.RefreshData;
        else if (uc is RequestsTab r) tabRefresh[page] = r.RefreshData;
        else if (uc is WaitlistTab w) tabRefresh[page] = w.RefreshData;
    }

    void RefreshAll()
    {
        propsTab.ReloadLayout();
        moneyTab.ReloadLayout();
        reqTab.ReloadLayout();
        waitTab.ReloadLayout();
    }

    void ApplyTheme()
    {
        Theme.Apply(this);
        themeBtn.Text = Theme.Dark ? "☀  Light mode" : "🌙  Dark mode";
        RefreshAll(); // re-applies data-dependent row colors (lease warnings, rent green)
    }
}
