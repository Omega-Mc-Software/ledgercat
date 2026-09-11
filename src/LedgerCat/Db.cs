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
}

public class Req
{
    public long Id;
    public string Created = "";
    public long? PropertyId;
    public string PropLabel = "";
    public string Kind = "maintenance"; // maintenance | viewing
    public string Description = "";
    public bool Done;
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

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS properties(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL, unit TEXT DEFAULT '', tenant TEXT DEFAULT '',
  rent REAL DEFAULT 0, due_day INTEGER DEFAULT 1, lease_end TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS transactions(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  date TEXT NOT NULL, property_id INTEGER,
  kind TEXT NOT NULL, category TEXT DEFAULT '', amount REAL NOT NULL, note TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS requests(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  created TEXT NOT NULL, property_id INTEGER,
  kind TEXT NOT NULL, description TEXT DEFAULT '', done INTEGER DEFAULT 0);
CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
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
    static decimal ToDec(object? o) => o == null || o is DBNull ? 0m : Convert.ToDecimal(o);
    static int ToInt(object? o) => o == null || o is DBNull ? 0 : Convert.ToInt32(o);

    // ---------- properties ----------

    public static List<Property> ListProps()
    {
        var list = new List<Property>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,name,unit,tenant,rent,due_day,lease_end FROM properties ORDER BY name COLLATE NOCASE, unit";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Property
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Unit = r.GetString(2),
                Tenant = r.GetString(3),
                Rent = ToDec(r[4]),
                DueDay = ToInt(r[5]),
                LeaseEnd = r.IsDBNull(6) ? "" : r.GetString(6),
            });
        return list;
    }

    public static long SaveProp(Property p)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (p.Id == 0)
        {
            cmd.CommandText = @"INSERT INTO properties(name,unit,tenant,rent,due_day,lease_end)
                VALUES($n,$u,$t,$r,$d,$l); SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"UPDATE properties SET name=$n, unit=$u, tenant=$t, rent=$r, due_day=$d, lease_end=$l
                WHERE id=$id; SELECT $id;";
            cmd.Parameters.AddWithValue("$id", p.Id);
        }
        cmd.Parameters.AddWithValue("$n", p.Name);
        cmd.Parameters.AddWithValue("$u", p.Unit);
        cmd.Parameters.AddWithValue("$t", p.Tenant);
        cmd.Parameters.AddWithValue("$r", Num(p.Rent));
        cmd.Parameters.AddWithValue("$d", p.DueDay);
        cmd.Parameters.AddWithValue("$l", p.LeaseEnd);
        var v = cmd.ExecuteScalar();
        return Convert.ToInt64(v ?? 0L);
    }

    public static void DeleteProp(long id)
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

    public static List<Txn> ListTxns()
    {
        var list = new List<Txn>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT t.id, t.date, t.property_id,
  CASE WHEN p.id IS NULL THEN '' ELSE p.name || CASE WHEN IFNULL(p.unit,'')='' THEN '' ELSE ' / ' || p.unit END END,
  t.kind, t.category, t.amount, t.note
FROM transactions t LEFT JOIN properties p ON p.id = t.property_id
ORDER BY t.date DESC, t.id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Txn
            {
                Id = r.GetInt64(0),
                Date = r.GetString(1),
                PropertyId = r.IsDBNull(2) ? null : r.GetInt64(2),
                PropLabel = r.IsDBNull(3) ? "" : r.GetString(3),
                Kind = r.GetString(4),
                Category = r.GetString(5),
                Amount = ToDec(r[6]),
                Note = r.GetString(7),
            });
        return list;
    }

    public static void SaveTxn(Txn t)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (t.Id == 0)
            cmd.CommandText = @"INSERT INTO transactions(date,property_id,kind,category,amount,note)
                VALUES($d,$p,$k,$c,$a,$n)";
        else
        {
            cmd.CommandText = @"UPDATE transactions SET date=$d, property_id=$p, kind=$k, category=$c, amount=$a, note=$n
                WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", t.Id);
        }
        cmd.Parameters.AddWithValue("$d", t.Date);
        cmd.Parameters.AddWithValue("$p", Id(t.PropertyId));
        cmd.Parameters.AddWithValue("$k", t.Kind);
        cmd.Parameters.AddWithValue("$c", t.Category);
        cmd.Parameters.AddWithValue("$a", Num(t.Amount));
        cmd.Parameters.AddWithValue("$n", t.Note);
        cmd.ExecuteNonQuery();
    }

    public static void DeleteTxn(long id)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM transactions WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- requests ----------

    public static List<Req> ListReqs()
    {
        var list = new List<Req>();
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT q.id, q.created, q.property_id,
  CASE WHEN p.id IS NULL THEN '' ELSE p.name || CASE WHEN IFNULL(p.unit,'')='' THEN '' ELSE ' / ' || p.unit END END,
  q.kind, q.description, q.done
FROM requests q LEFT JOIN properties p ON p.id = q.property_id
ORDER BY q.done ASC, q.created DESC, q.id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Req
            {
                Id = r.GetInt64(0),
                Created = r.GetString(1),
                PropertyId = r.IsDBNull(2) ? null : r.GetInt64(2),
                PropLabel = r.IsDBNull(3) ? "" : r.GetString(3),
                Kind = r.GetString(4),
                Description = r.GetString(5),
                Done = ToInt(r[6]) != 0,
            });
        return list;
    }

    public static void SaveReq(Req q)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        if (q.Id == 0)
            cmd.CommandText = @"INSERT INTO requests(created,property_id,kind,description,done)
                VALUES($d,$p,$k,$s,$o)";
        else
        {
            cmd.CommandText = @"UPDATE requests SET created=$d, property_id=$p, kind=$k, description=$s, done=$o
                WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", q.Id);
        }
        cmd.Parameters.AddWithValue("$d", q.Created);
        cmd.Parameters.AddWithValue("$p", Id(q.PropertyId));
        cmd.Parameters.AddWithValue("$k", q.Kind);
        cmd.Parameters.AddWithValue("$s", q.Description);
        cmd.Parameters.AddWithValue("$o", q.Done ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public static void SetReqDone(long id, bool done)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE requests SET done=$o WHERE id=$id";
        cmd.Parameters.AddWithValue("$o", done ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void DeleteReq(long id)
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
