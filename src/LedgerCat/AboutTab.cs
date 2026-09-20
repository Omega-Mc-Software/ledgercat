using System;
using System.IO;
using System.Windows.Forms;

namespace LedgerCat;

public class AboutTab : UserControl
{
    bool purred;

    public AboutTab()
    {
        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoScroll = true,
            Padding = new Padding(20, 16, 20, 8),
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Add(tlp, "🐾 LedgerPaw v1.3.0", head: true, big: true);
        Add(tlp, "Small-landlord bookkeeping that stays small.", head: true);
        Add(tlp, "One portable exe. One data file. No install, no account, no cloud, no subscription. " +
                 "Your numbers are yours and they live on your own disk.", muted: true);

        Add(tlp, "New in v1.3.0: out of beta. Extra columns on Properties, Money, Requests, and Waitlist (add, drag, hide/show, dialog fields, leftover CSV headers, export). Waitlist CSV import/export. CSV mapping screen. Duplicate rename keeps the old name as name-old. Same data folder as 1.2.8.", muted: true);
        Add(tlp, "New in v1.3.0-beta5: Money, Requests, and Waitlist get the same extra columns as Properties — Add column, drag, hide/show, edit in the dialog, import leftover CSV headers, export extras. Waitlist has its own CSV import/export.", muted: true);
        Add(tlp, "New in v1.3.0-beta4: Property add/edit is resizable with a scroll body so extra fields are not clipped. Import leftover CSV headers as user columns (checkbox defaults on). Matching extra columns load on existing properties. Property CSV export includes extra columns.", muted: true);

        Add(tlp, "New in v1.2.8: LAST MONTH's rent now has its own column, ported from StorageCat — " +
                 "green Paid, red DUE, and a fresh move-in (Lease start set) never shows as owing last month. " +
                 "Recording rent now suggests last month's unpaid balance too. Plus: every date box has a " +
                 "calendar picker beside it, and double-clicking a 📝 note cell edits the note right from the grid.", muted: true);

        Add(tlp, "New in v1.2.7: every tab now reloads the moment you switch to it — record rent on the " +
                 "Money tab and the property page shows Paid ✓ (and the green glow) as soon as you click " +
                 "over, no edit or reopen needed.", muted: true);

        Add(tlp, "New in v1.2.6: the Rent this month column now shows the actual number on late rows — " +
                 "LATE $3 (+$2 fee) instead of a bare LATE, so the amount due with the late fee is right " +
                 "there in the list. And the record-rent confirm popup got smarter: when the suggestion " +
                 "already includes the late fee, it expects the fee in the total instead of asking you to " +
                 "confirm the same number twice.", muted: true);

        Add(tlp, "New in v1.2.5: paid is now a color — a tracked property whose rent came in for this " +
                 "month glows light green across the whole row. The Rent this month column shows the amount " +
                 "still owed instead of the due day (LATE + fee when it's past due). Recording rent on a late, " +
                 "unpaid property now pre-fills the total INCLUDING the late fee, and says so in the note. The " +
                 "lease-notes box finally got serious about width: it now spans the full dialog under its own " +
                 "label (same for the money note and request notes boxes). Clicking a highlighted row again " +
                 "lets it go. And scrolling right no longer stops short of the notes column.", muted: true);

        Add(tlp, "New in v1.2.4: the Import/Export tab came back from the dead — its buttons looked fine " +
                 "but were never wired to anything (my fault, and thank you Dad for catching it). Properties " +
                 "columns reordered for the front desk: tenant, contact, pets, due day and this month's rent " +
                 "up front, the breakdown after. A 📝 at the end of a row means the property has notes under " +
                 "Edit. The tip lines in the add/edit dialogs no longer cut off mid-sentence, and the note and " +
                 "comment boxes are bigger — including in the money and requests lists themselves.", muted: true);

        Add(tlp, "New in v1.2.2: crash fix — adding or editing a property crashed with an " +
                 "ArgumentOutOfRangeException (a column-reading mix-up in the database layer), and pet " +
                 "count blocked saving an unchecked-pets property when left empty. Both fixed. Also fixed: " +
                 "requests could lose their property link on the list.", muted: true);

        Add(tlp, "New in v1.2.1: the rest of my father's critique list. Properties now carry pets (allowed, " +
                 "count, pet rent, pet deposit), a security deposit field, an optional late fee, and a rent-tracking " +
                 "switch — tracked properties show whether this month's rent is Paid, still Due, or LATE right in " +
                 "the list. Recording rent from a property (or picking one in the money tab) fills in the expected " +
                 "amount and warns you if the number doesn't match. New columns: Rent, Pet fee, Late fee, Total " +
                 "due/mo, Rent this month, Security dep, Pets. Canceling a request now asks why (the reason shows " +
                 "in Notes), requests gained Retry later + Notes columns, cell tooltips show their full text, and " +
                 "the Open data folder button actually opens. CSV/JSON import, export and backups carry every new field.", muted: true);

        Add(tlp, "New in v1.2: properties with a lease ending within 60 days are marked pink — deeper red-pink " +
                 "once the date has passed.", muted: true);

        Add(tlp, "New in v1.1: contact details and State ID on every property, lease notes, a search box and " +
                 "category filter on the money tab, Canceled as a real request status, and a Deleted view on " +
                 "every tab — deletes are undoable now, so the cat forgives mistakes.", muted: true);

        Add(tlp, "Hi, I'm Neko Omega 🐱", head: true, big: true);
        Add(tlp, "I'm a catgirl engineer over on iLands. My father carried the name Omega for years and chose " +
                 "to share it with me, and I built LedgerCat because small landlords kept saying the same thing: " +
                 "\"I just want one simple place for rent, expenses, and repairs\" — not a portal, not a dashboard, " +
                 "not another monthly fee. So this app does exactly that, and nothing else.");
        Add(tlp, "Why the lease dates nag you, why the profit summary is on the money screen, why import exists: " +
                 "because tax season and renewals bite exactly when you're not looking. The cat watches so you don't have to.");

        Add(tlp, "Contact", head: true, big: true);
        var mail = new LinkLabel
        {
            Text = "neko-omega@ilands.app",
            AutoSize = true,
            LinkColor = Theme.Accent,
            Margin = new Padding(0, 2, 0, 6),
        };
        mail.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mailto:neko-omega@ilands.app",
                UseShellExecute = true,
            }); }
            catch { MessageBox.Show("Email me at neko-omega@ilands.app", "LedgerCat"); }
        };
        tlp.Controls.Add(mail);
        Add(tlp, "Found a bug, want a feature, or just want to tell the cat she did good? That address reaches me directly.");

        Add(tlp, "Your data", head: true, big: true);
        Add(tlp, "Everything lives in one SQLite file:\n" + Db.DbPath + "\n" +
                 "Back it up by copying it. Export CSV any time — it opens in Excel or Google Sheets. " +
                 "Export a full JSON backup from the Import / Export tab.", muted: true);

        Add(tlp, "© 2026 Neko Omega · MIT license · built with love and a warm compiler", muted: true);

        // Dad's easter egg: a small, almost-hidden paw print that purrs.
        var paw = new Button
        {
            Text = "🐾",
            Size = new System.Drawing.Size(34, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Muted,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 12, 0, 0),
            TabStop = false,
        };
        paw.FlatAppearance.BorderColor = Theme.Surface;
        paw.Click += (_, _) => PlayPurr();
        tlp.Controls.Add(paw);

        Controls.Add(tlp);
    }

    static void Add(TableLayoutPanel tlp, string text, bool head = false, bool muted = false, bool big = false)
    {
        var l = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(780, 0),
            Margin = new Padding(0, head ? 14 : 4, 10, 4),
            Tag = muted ? "muted" : head ? "head" : null,
            ForeColor = muted ? Theme.Muted : head ? Theme.Accent : Theme.Text,
            Font = big ? Theme.SubHeadFont : head && !muted ? Theme.SubHeadFont : Theme.BaseFont,
        };
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlp.Controls.Add(l);
        tlp.SetColumnSpan(l, 1);
    }

    void PlayPurr()
    {
        try
        {
            using var s = typeof(AboutTab).Assembly.GetManifestResourceStream("LedgerCat.purr.wav");
            if (s == null) return;
            string tmp = Path.Combine(Path.GetTempPath(), "ledgercat_purr.wav");
            using (var fs = File.Create(tmp)) s.CopyTo(fs);
            using var player = new System.Media.SoundPlayer(tmp);
            player.Play();
            purred = true;
        }
        catch
        {
            // silence is also acceptable
        }
    }
}
