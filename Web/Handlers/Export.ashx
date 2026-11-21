<%@ WebHandler Language="C#" Class="PoultryERP.ExportHandler" %>
using System;
using System.Web;
using System.Web.SessionState;
using PoultryERP.Reports;

namespace PoultryERP
{
    /// <summary>Streams a report as PDF, Excel (.xlsx) or CSV. Same filters and role checks as the screen.</summary>
    public class ExportHandler : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var u = Ctx.User;
            if (u == null) { ctx.Response.StatusCode = 401; return; }
            if (u.MustChangePassword) { ctx.Response.Redirect("~/ChangePassword.aspx"); return; }
            var def = ReportCatalog.Get(ctx.Request.QueryString["r"] ?? "");
            if (def == null) { ctx.Response.StatusCode = 404; return; }
            if (!Ctx.InRole(def.Roles)) { ctx.Response.StatusCode = 403; ctx.Response.Write("Access denied"); return; }

            var p = ReportParams.Parse(ctx.Request.QueryString);
            var fmt = (ctx.Request.QueryString["fmt"] ?? "pdf").ToLowerInvariant();
            var model = def.Build(p);
            byte[] data; string type, ext;
            switch (fmt)
            {
                case "xlsx": data = XlsxWriter.Write(model); type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; ext = "xlsx"; break;
                case "csv": data = CsvWriter.Write(model); type = "text/csv"; ext = "csv"; break;
                default: data = PdfWriter.Write(model); type = "application/pdf"; ext = "pdf"; break;
            }
            Audit.Log("EXPORT", "Report", def.Key, fmt + " " + p.ToQuery());

            var file = def.Key + "_" + (def.Has("date") ? p.Date.ToString("yyyyMMdd") : p.From.ToString("yyyyMMdd") + "-" + p.To.ToString("yyyyMMdd")) + "." + ext;
            ctx.Response.Clear();
            ctx.Response.ContentType = type;
            ctx.Response.AddHeader("Content-Disposition", (ext == "pdf" ? "inline" : "attachment") + "; filename=\"" + file + "\"");
            ctx.Response.AddHeader("Cache-Control", "private, no-store");
            ctx.Response.BinaryWrite(data);
        }
    }
}
