using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;

namespace PoultryERP
{
    /// <summary>Role names used everywhere (must match CK_User_Role in the database).</summary>
    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string FarmManager = "FarmManager";
        public const string Supervisor = "Supervisor";
        public const string Veterinarian = "Veterinarian";
        public const string Accountant = "Accountant";

        public static readonly string[] All = { SuperAdmin, FarmManager, Supervisor, Veterinarian, Accountant };
        public static readonly string[] Managers = { SuperAdmin, FarmManager };

        public static string Display(string role)
        {
            switch (role)
            {
                case SuperAdmin: return "Super Admin";
                case FarmManager: return "Farm / Production Manager";
                case Supervisor: return "Shed Supervisor / Data Entry";
                case Veterinarian: return "Veterinarian / Quality Officer";
                case Accountant: return "Accountant / Finance Officer";
                default: return role;
            }
        }
    }

    /// <summary>Thin, parameterised ADO.NET helper. Never concatenate user input into SQL.</summary>
    public static class Db
    {
        public static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["PoultryERP"].ConnectionString; }
        }

        public static SqlConnection Open()
        {
            var c = new SqlConnection(ConnectionString);
            c.Open();
            return c;
        }

        public static SqlParameter P(string name, object value)
        {
            var p = new SqlParameter(name, value ?? DBNull.Value);
            if (value is string) { p.SqlDbType = SqlDbType.NVarChar; int n = ((string)value).Length; p.Size = n > 4000 ? -1 : Math.Max(n, 1); }
            if (value is DateTime) p.SqlDbType = SqlDbType.Date;
            return p;
        }

        /// <summary>DateTime parameter that keeps the time part.</summary>
        public static SqlParameter PTime(string name, DateTime? value)
        {
            var p = new SqlParameter(name, SqlDbType.DateTime2);
            p.Value = value.HasValue ? (object)value.Value : DBNull.Value;
            return p;
        }

        static SqlCommand Cmd(SqlConnection c, SqlTransaction t, string sql, SqlParameter[] ps)
        {
            var cmd = new SqlCommand(sql, c, t);
            cmd.CommandTimeout = 120;
            if (ps != null)
                foreach (var p in ps) if (p != null) cmd.Parameters.Add(p);
            return cmd;
        }

        public static DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var c = Open()) return Query(c, null, sql, ps);
        }

        public static DataTable Query(SqlConnection c, SqlTransaction t, string sql, params SqlParameter[] ps)
        {
            if (c == null) { using (var own = Open()) return Query(own, null, sql, ps); }
            using (var cmd = Cmd(c, t, sql, ps))
            using (var da = new SqlDataAdapter(cmd))
            {
                var dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        public static DataRow Row(string sql, params SqlParameter[] ps)
        {
            var dt = Query(sql, ps);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var c = Open()) return Scalar(c, null, sql, ps);
        }

        public static object Scalar(SqlConnection c, SqlTransaction t, string sql, params SqlParameter[] ps)
        {
            if (c == null) { using (var own = Open()) return Scalar(own, null, sql, ps); }
            using (var cmd = Cmd(c, t, sql, ps))
            {
                var o = cmd.ExecuteScalar();
                return o == DBNull.Value ? null : o;
            }
        }

        public static int Exec(string sql, params SqlParameter[] ps)
        {
            using (var c = Open()) return Exec(c, null, sql, ps);
        }

        public static int Exec(SqlConnection c, SqlTransaction t, string sql, params SqlParameter[] ps)
        {
            if (c == null) { using (var own = Open()) return Exec(own, null, sql, ps); }
            using (var cmd = Cmd(c, t, sql, ps)) return cmd.ExecuteNonQuery();
        }

        /// <summary>Runs work inside a transaction; commits on success, rolls back on any exception.</summary>
        public static T Tx<T>(Func<SqlConnection, SqlTransaction, T> work)
        {
            using (var c = Open())
            using (var t = c.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    var r = work(c, t);
                    t.Commit();
                    return r;
                }
                catch
                {
                    try { t.Rollback(); } catch { }
                    throw;
                }
            }
        }

        public static int Int(object o) { return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o); }
        public static decimal Dec(object o) { return o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o); }
        public static int? NInt(object o) { return o == null || o == DBNull.Value ? (int?)null : Convert.ToInt32(o); }
        public static decimal? NDec(object o) { return o == null || o == DBNull.Value ? (decimal?)null : Convert.ToDecimal(o); }
        public static DateTime? NDate(object o) { return o == null || o == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(o); }
        public static string Str(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToString(o); }

        /// <summary>Friendly text for common SQL errors (FK, unique, check constraints).</summary>
        public static string FriendlyError(Exception ex)
        {
            var se = ex as SqlException;
            if (se == null && ex.InnerException != null) se = ex.InnerException as SqlException;
            if (se != null)
            {
                switch (se.Number)
                {
                    case 547: return "This record is referenced by other records or a value is outside the allowed range (" + Trim(se.Message) + ").";
                    case 2601:
                    case 2627: return "A record with the same code / key already exists.";
                    case 8152:
                    case 2628: return "One of the values is too long.";
                    case -2: return "The database did not respond in time. Please retry.";
                }
                return "Database error: " + Trim(se.Message);
            }
            return ex.Message;
        }

        static string Trim(string m) { return m.Length > 220 ? m.Substring(0, 220) + "..." : m; }
    }

    /// <summary>PBKDF2 password hashing: PBKDF2$SHA256$iterations$salt$hash</summary>
    public static class PasswordHasher
    {
        const int Iterations = 100000;

        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] key;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256)) key = kdf.GetBytes(32);
            return "PBKDF2$SHA256$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored) || password == null) return false;
            var parts = stored.Split('$');
            if (parts.Length != 5 || parts[0] != "PBKDF2" || parts[1] != "SHA256") return false;
            int iter;
            if (!int.TryParse(parts[2], out iter)) return false;
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            byte[] actual;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iter, HashAlgorithmName.SHA256)) actual = kdf.GetBytes(expected.Length);
            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        public static string StrengthError(string pw)
        {
            if (pw == null || pw.Length < 8) return "Password must be at least 8 characters.";
            bool up = false, low = false, dig = false;
            foreach (var ch in pw) { if (char.IsUpper(ch)) up = true; else if (char.IsLower(ch)) low = true; else if (char.IsDigit(ch)) dig = true; }
            if (!(up && low && dig)) return "Password must contain upper-case, lower-case letters and a digit.";
            return null;
        }
    }

    public class UserInfo
    {
        public int UserId;
        public string UserName;
        public string FullName;
        public string Role;
        public int? CompanyId;
        public bool MustChangePassword;

        public string Serialize()
        {
            return UserId + "|" + Role + "|" + (CompanyId.HasValue ? CompanyId.Value.ToString() : "") + "|" + (MustChangePassword ? "1" : "0") + "|" + (FullName ?? "").Replace("|", " ");
        }

        public static UserInfo Parse(string name, string data)
        {
            var p = (data ?? "").Split(new[] { '|' }, 5);
            if (p.Length < 5) return null;
            var u = new UserInfo();
            u.UserName = name;
            u.UserId = int.Parse(p[0]);
            u.Role = p[1];
            if (p[2].Length > 0) u.CompanyId = int.Parse(p[2]);
            u.MustChangePassword = p[3] == "1";
            u.FullName = p[4];
            return u;
        }
    }

    /// <summary>Company-level settings that drive stage rules, trays, edit window, costing.</summary>
    public class CompanySettings
    {
        public int CompanyId;
        public string Code;
        public string Name;
        public int ChickEndWk = 8, GrowerEndWk = 15, PreLayEndWk = 18;
        public int EggsPerTray = 30;
        public int EditWindowDays = 1;
        public bool CrackedIsSaleable;
        public int BroilerMaxDays = 70;

        public int LayStartWeek { get { return PreLayEndWk + 1; } }
        public int LayStartAgeDays { get { return LayStartWeek * 7; } }

        public string StageOf(int ageDays)
        {
            int wk = ageDays / 7;
            if (wk <= ChickEndWk) return "Chick";
            if (wk <= GrowerEndWk) return "Grower";
            if (wk <= PreLayEndWk) return "Pre-Lay";
            return "Layer";
        }

        /// <summary>Broilers (meat birds) have a single stage; layers follow the Chick / Grower / Pre-Lay / Layer cycle.</summary>
        public string StageOf(int ageDays, bool broiler) { return broiler ? "Broiler" : StageOf(ageDays); }

        public bool IsLaying(int ageDays) { return ageDays / 7 > PreLayEndWk; }
        public bool IsLaying(int ageDays, bool broiler) { return !broiler && IsLaying(ageDays); }

        public int SaleableEggs(int table, int cracked, int misshapen, int doubleYolk)
        {
            return table + misshapen + doubleYolk + (CrackedIsSaleable ? cracked : 0);
        }
    }

    /// <summary>Per-request context: who is logged in, which company is active, shed scope.</summary>
    public static class Ctx
    {
        const string ItemUser = "__ctx_user", ItemCompany = "__ctx_company";

        public static UserInfo User
        {
            get
            {
                var http = HttpContext.Current;
                if (http == null || http.User == null || !http.User.Identity.IsAuthenticated) return null;
                var cached = http.Items[ItemUser] as UserInfo;
                if (cached != null) return cached;
                var fi = http.User.Identity as FormsIdentity;
                if (fi == null) return null;
                var u = UserInfo.Parse(fi.Name, fi.Ticket.UserData);
                http.Items[ItemUser] = u;
                return u;
            }
        }

        public static int UserId { get { var u = User; return u == null ? 0 : u.UserId; } }
        public static string Role { get { var u = User; return u == null ? "" : u.Role; } }
        public static bool IsSuperAdmin { get { return Role == Roles.SuperAdmin; } }
        public static bool IsManager { get { return Role == Roles.SuperAdmin || Role == Roles.FarmManager; } }

        /// <summary>Supervisors only see the sheds assigned to them.</summary>
        public static bool ShedRestricted { get { return Role == Roles.Supervisor; } }

        public static bool InRole(params string[] roles)
        {
            if (roles == null || roles.Length == 0) return User != null;
            var r = Role;
            foreach (var x in roles) if (x == r) return true;
            return false;
        }

        /// <summary>Active company. Super admins choose it from the top bar; others are fixed to theirs.</summary>
        public static int CompanyId
        {
            get
            {
                var u = User;
                if (u == null) return 0;
                if (u.Role != Roles.SuperAdmin) return u.CompanyId ?? 0;
                var s = HttpContext.Current.Session;
                if (s != null && s["ActiveCompanyId"] is int) return (int)s["ActiveCompanyId"];
                var first = Db.Scalar("SELECT TOP (1) CompanyId FROM dbo.Company WHERE IsActive = 1 ORDER BY CompanyId");
                int id = Db.Int(first);
                if (s != null && id > 0) s["ActiveCompanyId"] = id;
                return id;
            }
            set
            {
                if (IsSuperAdmin && HttpContext.Current.Session != null)
                {
                    HttpContext.Current.Session["ActiveCompanyId"] = value;
                    HttpContext.Current.Items.Remove(ItemCompany);
                }
            }
        }

        public static CompanySettings Company
        {
            get
            {
                var http = HttpContext.Current;
                var cached = http.Items[ItemCompany] as CompanySettings;
                if (cached != null) return cached;
                var cs = LoadCompany(CompanyId);
                http.Items[ItemCompany] = cs;
                return cs;
            }
        }

        public static CompanySettings LoadCompany(int companyId)
        {
            var cs = new CompanySettings { CompanyId = companyId, Name = "(no company)" };
            var r = Db.Row("SELECT * FROM dbo.Company WHERE CompanyId = @id", Db.P("@id", companyId));
            if (r != null)
            {
                cs.Code = Db.Str(r["Code"]);
                cs.Name = Db.Str(r["Name"]);
                cs.ChickEndWk = Db.Int(r["StageChickEndWk"]);
                cs.GrowerEndWk = Db.Int(r["StageGrowerEndWk"]);
                cs.PreLayEndWk = Db.Int(r["StagePreLayEndWk"]);
                cs.EggsPerTray = Db.Int(r["EggsPerTray"]);
                cs.EditWindowDays = Db.Int(r["EditWindowDays"]);
                cs.CrackedIsSaleable = Convert.ToBoolean(r["CrackedIsSaleable"]);
                if (r.Table.Columns.Contains("BroilerMaxDays")) cs.BroilerMaxDays = Db.Int(r["BroilerMaxDays"]);
            }
            return cs;
        }

        /// <summary>Standard scope parameters: @CompanyId, @UserId, @Restricted (1 = supervisor shed filter).</summary>
        public static SqlParameter[] Scope(params SqlParameter[] extra)
        {
            var list = new List<SqlParameter>();
            list.Add(Db.P("@CompanyId", CompanyId));
            list.Add(Db.P("@UserId", UserId));
            list.Add(Db.P("@Restricted", ShedRestricted ? 1 : 0));
            if (extra != null) list.AddRange(extra);
            return list.ToArray();
        }

        /// <summary>SQL predicate for supervisor shed restriction; use with Scope() parameters.</summary>
        public static string ShedFilter(string shedIdColumn)
        {
            return " AND (@Restricted = 0 OR " + shedIdColumn + " IN (SELECT us.ShedId FROM dbo.UserShed us WHERE us.UserId = @UserId)) ";
        }

        public static bool CanAccessShed(int shedId)
        {
            var o = Db.Scalar("SELECT COUNT(*) FROM dbo.Shed s WHERE s.ShedId = @ShedId AND s.CompanyId = @CompanyId" + ShedFilter("s.ShedId"),
                Scope(Db.P("@ShedId", shedId)));
            return Db.Int(o) > 0;
        }

        public static string ClientIp
        {
            get
            {
                var r = HttpContext.Current == null ? null : HttpContext.Current.Request;
                return r == null ? null : r.UserHostAddress;
            }
        }
    }

    public static class Audit
    {
        public static void Log(string action, string entity, object entityId, string details)
        {
            Log(null, null, action, entity, entityId, details);
        }

        public static void Log(SqlConnection c, SqlTransaction t, string action, string entity, object entityId, string details)
        {
            var u = Ctx.User;
            const string sql = "INSERT INTO dbo.AuditLog (CompanyId, UserId, UserName, Action, EntityName, EntityId, Details, IpAddress) " +
                               "VALUES (@c, @u, @n, @a, @e, @id, @d, @ip)";
            var ps = new[]
            {
                Db.P("@c", u == null ? (object)null : (object)Ctx.CompanyId),
                Db.P("@u", u == null ? (object)null : u.UserId),
                Db.P("@n", u == null ? null : u.UserName),
                Db.P("@a", action), Db.P("@e", entity),
                Db.P("@id", entityId == null ? null : entityId.ToString()),
                Db.P("@d", details), Db.P("@ip", Ctx.ClientIp)
            };
            try
            {
                if (c != null) Db.Exec(c, t, sql, ps); else Db.Exec(sql, ps);
            }
            catch
            {
                if (c != null) throw; // inside a transaction audit failure must abort the change
            }
        }

        public static string Describe(IDictionary<string, object> values)
        {
            var sb = new StringBuilder();
            foreach (var kv in values)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(kv.Key).Append('=').Append(kv.Value == null || kv.Value == DBNull.Value ? "NULL" : Convert.ToString(kv.Value));
            }
            return sb.ToString();
        }
    }

    public static class AuthCookie
    {
        public static void SignIn(UserInfo info, HttpResponse response)
        {
            var ticket = new FormsAuthenticationTicket(1, info.UserName, DateTime.Now, DateTime.Now.Add(FormsAuthentication.Timeout),
                false, info.Serialize(), FormsAuthentication.FormsCookiePath);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket));
            cookie.HttpOnly = true;
            cookie.Secure = FormsAuthentication.RequireSSL;
            cookie.Path = FormsAuthentication.FormsCookiePath;
            response.Cookies.Add(cookie);
            if (HttpContext.Current != null) HttpContext.Current.Items.Remove("__ctx_user");
        }
    }

    public static class Age
    {
        /// <summary>Age in the same "weeks-days" form as the old MIS reports (e.g. 23-4).</summary>
        public static string Text(DateTime hatch, DateTime onDate)
        {
            int d = (int)(onDate.Date - hatch.Date).TotalDays;
            if (d < 0) return "-";
            return (d / 7) + "-" + (d % 7);
        }

        public static int Days(DateTime hatch, DateTime onDate) { return (int)(onDate.Date - hatch.Date).TotalDays; }
    }
}
