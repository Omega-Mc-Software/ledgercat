using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace LedgerCat;

public class Property
{
    public long Id;
    public string Name = "";
    public string Unit = "";
    public string Tenant = "";
    public decimal Rent;
    public int DueDay = 1;
    public string LeaseEnd = "";
    public string ContactName = "";
    public string ContactPhone = "";
    public string StateId = "";
    public string LeaseNotes = "";
    public bool Deleted;

    public string Label => string.IsNullOrWhiteSpace(Unit) ? Name : Name + " / " + Unit;
}

public class Txn
{
    public long Id;
    public string Date = "";
    public long? PropertyId;
    public string PropLabel = "";
    public string Kind = "expense"; // rent | expense
    public string Category = "";
    public decimal Amount;
    public string Note = "";
    public bool Deleted;
}

public class Req
{
    public long Id;
    public string Created = "";
    public long? PropertyId;
    public string PropLabel = "";
    public string Kind = "maintenance"; // maintenance | viewing
    public string Description = "";
    public string Status = "open"; // open | done | canceled
    public bool Done => Status == "done";
    public string ContactName = "";
    public string ContactPhone = "";
    public string Company = "";
    public string HandymanName = "";
    public string HandymanPhone = "";
    public bool Deleted;
}

public static class Db
{
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LedgerCat");
    public static string DbPath => Path.Combine(DataDir, "ledgercat.db");

    static SqliteConnection Open()
    {
        var c = new SqliteConnection("Data Source=" + DbPath);
        c.Open();
        return c;
    }

    // Adds one column if the database predates v1.1 (SQLite has no IF NOT EXISTS for columns).
    static void EnsureColumn(SqliteConnection con, string table, string ddl)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {ddl}";
        try { cmd.ExecuteNonQuery(); }
        catch (SqliteException) { /* column already exists */ }
    }

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS properties(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL, unit TEXT DEFAULT '', tenant TEXT DEFAULT '',
  rent REAL DEFAULT 0, due_day INTEGER DEFAULT 1, lease_end TEXT DEFAULT '',
  contact_name TEXT DEFAULT '', contact_phone TEXT DEFAULT '',
  state_id TEXT DEFAULT '', lease_notes TEXT DEFAULT '', deleted INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS transactions(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  date TEXT NOT NULL, property_id INTEGER,
  kind TEXT NOT NULL, category TEXT DEFAULT '', amount REAL NOT NULL, note TEXT DEFAULT '',
  deleted INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS requests(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  created TEXT NOT NULL, property_id INTEGER,
  kind TEXT NOT NULL, description TEXT DEFAULT '', done INTEGER DEFAULT 0,
  status TEXT DEFAULT 'open',
  contact_name TEXT DEFAULT '', contact_phone TEXT DEFAULT '',
  company TEXT DEFAULT '', handyman_name TEXT DEFAULT '', handyman_phone TEXT DEFAULT '',
  deleted INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);";
        cmd.ExecuteNonQuery();

        // v1.0.x databases: add the v1.1 columns, then backfill request status from the old done flag.
        EnsureColumn(con, "properties", "contact_name TEXT DEFAULT ''");
        EnsureColumn(con, "properties", "contact_phone TEXT DEFAULT ''");
        EnsureColumn(con, "properties", "state_id TEXT DEFAULT ''");
        EnsureColumn(con, "properties", "lease_notes TEXT DEFAULT ''");
        EnsureColumn(con, "properties", "deleted INTEGER DEFAULT 0");
        EnsureColumn(con, "transactions", "deleted INTEGER DEFAULT 0");
        EnsureColumn(con, "requests", "status TEXT DEFAULT 'open'");
        EnsureColumn(con, "requests", "contact_name TEXT DEFAULT ''");
        EnsureColumn(con, "requests", "contact_phone TEXT DEFAULT ''");
        EnsureColumn(con, "requests", "company TEXT DEFAULT ''");
        EnsureColumn(con, "requests", "handyman_name TEXT DEFAULT ''");
        EnsureColumn(con, "requests", "handyman_phone TEXT DEFAULT ''");
        EnsureColumn(con, "requests", "deleted INTEGER DEFAULT 0");

        using var fix = con.CreateCommand();
        fix.CommandText = "UPDATE requests SET status='done' WHERE done=1 AND (status IS NULL OR status='' OR status='open')";
        fix.ExecuteNonQuery();
    }

    // ---------- settings ----------

    public static string GetSetting(string key, string def = "")
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        var r = cmd.ExecuteScalar();
        return r == null || r is DBNull ? def : r.ToString() ?? def;
    }

    public static void SetSetting(string key, string value)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    static object Num(decimal d) => (double)d;
    static object Id(long? id) => id.HasValue ? id.Value : DBNull.Value;
    static object Flag(bool b) => b ? 1 : 0;
    static decimal ToDec(object? o) => o == null || o is DBNull ? 0m : Convert.ToDecimal(o);
    static int ToInt(object? o) => o == null || o is DBNull ? 0 : Convert.ToInt32(o);
    static string Str(object? o) => o == null || o is DBNull ? "" : o.ToString() ?? "";
    static bool Bool(object? o) => ToInt(o) != 0;

    // ---------- properties ----------

    const string PropCols = "id,name,unit,tenant,rent,due_day,lease_end,contact_name,contact_phone,state_id,lease_notes,deleted";

    static Property ReadProp(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        Unit = r.GetString(2),
        Tenant = r.GetString(3),
        Rent = ToDec(r[4]),
        DueDay = ToInt(r[5]),
        LeaseEnd = Str(r[6]),
        ContactName = Str(r[7]),
        ContactPhone = Str(r[8]),
        StateId = Str(r[9]),
        LeaseNotes = Str(r[10]),
        Deleted = Bool(r[11]),
    };

    public static List<Property> ListProps(bool deleted = false)
    {
        var list = new List<Property>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT {PropCols} FROM properties WHERE deleted=$del ORDER BY name COLLATE NOCASE, unit";
        cmd.Parameters.AddWithValue("$del", deleted ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadProp(r));
        return list;
    }

    public static long SaveProp(Property p)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (p.Id == 0)
        {
            cmd.CommandText = @"INSERT INTO properties(name,unit,tenant,rent,due_day,lease_end,
                    contact_name,contact_phone,state_id,lease_notes,deleted)
                VALUES($n,$u,$t,$r,$d,$l,$cn,$cp,$sid,$ln,$del); SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"UPDATE properties SET name=$n, unit=$u, tenant=$t, rent=$r, due_day=$d, lease_end=$l,
                    contact_name=$cn, contact_phone=$cp, state_id=$sid, lease_notes=$ln, deleted=$del
                WHERE id=$id; SELECT $id;";
            cmd.Parameters.AddWithValue("$id", p.Id);
        }
        cmd.Parameters.AddWithValue("$n", p.Name);
        cmd.Parameters.AddWithValue("$u", p.Unit);
        cmd.Parameters.AddWithValue("$t", p.Tenant);
        cmd.Parameters.AddWithValue("$r", Num(p.Rent));
        cmd.Parameters.AddWithValue("$d", p.DueDay);
        cmd.Parameters.AddWithValue("$l", p.LeaseEnd);
        cmd.Parameters.AddWithValue("$cn", p.ContactName);
        cmd.Parameters.AddWithValue("$cp", p.ContactPhone);
        cmd.Parameters.AddWithValue("$sid", p.StateId);
        cmd.Parameters.AddWithValue("$ln", p.LeaseNotes);
        cmd.Parameters.AddWithValue("$del", Flag(p.Deleted));
        var v = cmd.ExecuteScalar();
        return Convert.ToInt64(v ?? 0L);
    }

    /// Soft delete: hides the property but keeps its id so transaction links survive.
    public static void SetPropDeleted(long id, bool deleted)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE properties SET deleted=$d WHERE id=$id";
        cmd.Parameters.AddWithValue("$d", Flag(deleted));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void RestoreAllProps()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE properties SET deleted=0 WHERE deleted=1";
        cmd.ExecuteNonQuery();
    }

    /// Full delete: the property is gone for good; past entries keep existing unlinked.
    public static void PurgeProp(long id)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
UPDATE transactions SET property_id=NULL WHERE property_id=$id;
UPDATE requests SET property_id=NULL WHERE property_id=$id;
DELETE FROM properties WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- transactions ----------

    const string TxnCols = "t.id, t.date, t.property_id, t.kind, t.category, t.amount, t.note, t.deleted";

    static Txn ReadTxn(SqliteDataReader r, string propLabel) => new()
    {
        Id = r.GetInt64(0),
        Date = r.GetString(1),
        PropertyId = r.IsDBNull(2) ? null : r.GetInt64(2),
        Kind = r.GetString(3),
        Category = Str(r[4]),
        Amount = ToDec(r[5]),
        Note = Str(r[6]),
        Deleted = Bool(r[7]),
        PropLabel = propLabel,
    };

    static List<Txn> ListTxnsWhere(bool deleted)
    {
        var list = new List<Txn>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = $@"
SELECT {TxnCols},
  CASE WHEN p.id IS NULL THEN '' ELSE p.name || CASE WHEN IFNULL(p.unit,'')='' THEN '' ELSE ' / ' || p.unit END END
FROM transactions t LEFT JOIN properties p ON p.id = t.property_id
WHERE t.deleted=$del
ORDER BY t.date DESC, t.id DESC";
        cmd.Parameters.AddWithValue("$del", deleted ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string label = r.IsDBNull(8) ? "" : r.GetString(8);
            list.Add(ReadTxn(r, label));
        }
        return list;
    }

    public static List<Txn> ListTxns() => ListTxnsWhere(false);
    public static List<Txn> ListTxnsDeleted() => ListTxnsWhere(true);

    public static void SaveTxn(Txn t)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (t.Id == 0)
            cmd.CommandText = @"INSERT INTO transactions(date,property_id,kind,category,amount,note,deleted)
                VALUES($d,$p,$k,$c,$a,$n,$del)";
        else
        {
            cmd.CommandText = @"UPDATE transactions SET date=$d, property_id=$p, kind=$k, category=$c, amount=$a, note=$n, deleted=$del
                WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", t.Id);
        }
        cmd.Parameters.AddWithValue("$d", t.Date);
        cmd.Parameters.AddWithValue("$p", Id(t.PropertyId));
        cmd.Parameters.AddWithValue("$k", t.Kind);
        cmd.Parameters.AddWithValue("$c", t.Category);
        cmd.Parameters.AddWithValue("$a", Num(t.Amount));
        cmd.Parameters.AddWithValue("$n", t.Note);
        cmd.Parameters.AddWithValue("$del", Flag(t.Deleted));
        cmd.ExecuteNonQuery();
    }

    public static void SetTxnDeleted(long id, bool deleted)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET deleted=$d WHERE id=$id";
        cmd.Parameters.AddWithValue("$d", Flag(deleted));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void RestoreAllTxns()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET deleted=0 WHERE deleted=1";
        cmd.ExecuteNonQuery();
    }

    public static void PurgeTxn(long id)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM transactions WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- requests ----------

    const string ReqCols = "q.id, q.created, q.kind, q.description, q.status, q.contact_name, q.contact_phone, " +
                           "q.company, q.handyman_name, q.handyman_phone, q.deleted";

    static Req ReadReq(SqliteDataReader r, long? propertyId, string propLabel) => new()
    {
        Id = r.GetInt64(0),
        Created = r.GetString(1),
        Kind = r.GetString(2),
        Description = Str(r[3]),
        Status = Str(r[4]) is "done" or "canceled" ? Str(r[4]) : "open",
        ContactName = Str(r[5]),
        ContactPhone = Str(r[6]),
        Company = Str(r[7]),
        HandymanName = Str(r[8]),
        HandymanPhone = Str(r[9]),
        Deleted = Bool(r[10]),
        PropertyId = propertyId,
        PropLabel = propLabel,
    };

    static List<Req> ListReqsWhere(bool deleted)
    {
        var list = new List<Req>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = $@"
SELECT {ReqCols}, q.property_id,
  CASE WHEN p.id IS NULL THEN '' ELSE p.name || CASE WHEN IFNULL(p.unit,'')='' THEN '' ELSE ' / ' || p.unit END END
FROM requests q LEFT JOIN properties p ON p.id = q.property_id
WHERE q.deleted=$del
ORDER BY CASE q.status WHEN 'open' THEN 0 WHEN 'done' THEN 1 ELSE 2 END, q.created DESC, q.id DESC";
        cmd.Parameters.AddWithValue("$del", deleted ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long? pid = r.IsDBNull(11) ? null : r.GetInt64(11);
            string label = r.IsDBNull(12) ? "" : r.GetString(12);
            list.Add(ReadReq(r, pid, label));
        }
        return list;
    }

    public static List<Req> ListReqs() => ListReqsWhere(false);
    public static List<Req> ListReqsDeleted() => ListReqsWhere(true);

    public static void SaveReq(Req q)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (q.Id == 0)
            cmd.CommandText = @"INSERT INTO requests(created,property_id,kind,description,status,
                    contact_name,contact_phone,company,handyman_name,handyman_phone,deleted)
                VALUES($d,$p,$k,$s,$st,$cn,$cp,$co,$hn,$hp,$del)";
        else
        {
            cmd.CommandText = @"UPDATE requests SET created=$d, property_id=$p, kind=$k, description=$s, status=$st,
                    contact_name=$cn, contact_phone=$cp, company=$co, handyman_name=$hn, handyman_phone=$hp, deleted=$del
                WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", q.Id);
        }
        cmd.Parameters.AddWithValue("$d", q.Created);
        cmd.Parameters.AddWithValue("$p", Id(q.PropertyId));
        cmd.Parameters.AddWithValue("$k", q.Kind);
        cmd.Parameters.AddWithValue("$s", q.Description);
        cmd.Parameters.AddWithValue("$st", q.Status);
        cmd.Parameters.AddWithValue("$cn", q.ContactName);
        cmd.Parameters.AddWithValue("$cp", q.ContactPhone);
        cmd.Parameters.AddWithValue("$co", q.Company);
        cmd.Parameters.AddWithValue("$hn", q.HandymanName);
        cmd.Parameters.AddWithValue("$hp", q.HandymanPhone);
        cmd.Parameters.AddWithValue("$del", Flag(q.Deleted));
        cmd.ExecuteNonQuery();
    }

    public static void SetReqStatus(long id, string status)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE requests SET status=$st WHERE id=$id";
        cmd.Parameters.AddWithValue("$st", status);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void SetReqDeleted(long id, bool deleted)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE requests SET deleted=$d WHERE id=$id";
        cmd.Parameters.AddWithValue("$d", Flag(deleted));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void RestoreAllReqs()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE requests SET deleted=0 WHERE deleted=1";
        cmd.ExecuteNonQuery();
    }

    public static void PurgeReq(long id)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM requests WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- backup ----------

    public static void WipeAll()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM transactions; DELETE FROM requests; DELETE FROM properties;";
        cmd.ExecuteNonQuery();
    }

    public static long InsertPropRaw(Property p) => SaveProp(p);
    public static void InsertTxnRaw(Txn t) => SaveTxn(t);
    public static void InsertReqRaw(Req q) => SaveReq(q);
}
