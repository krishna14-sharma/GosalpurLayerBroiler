<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Login.aspx.cs" Inherits="PoultryERP.LoginPage" %>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Sign in | Phoenix Poultry ERP</title>
    <link rel="stylesheet" href="lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="lib/bootstrap-icons/bootstrap-icons.min.css" />
    <link rel="stylesheet" href="Content/site.css?v=1" />
</head>
<body>
    <div class="login-wrap">
        <section class="login-art">
            <div class="d-flex align-items-center gap-2">
                <span class="brand-mark">P</span>
                <span class="fw-bold fs-5">Phoenix Poultry ERP</span>
            </div>
            <div>
                <h1 class="mb-4">One place for every shed, flock and egg.</h1>
                <div class="feat"><i class="bi bi-check2-circle"></i><span>Daily shed logs with automatic opening/closing birds, HD% and feed per bird</span></div>
                <div class="feat"><i class="bi bi-check2-circle"></i><span>Flock shifting with age continuity and transit-loss reconciliation</span></div>
                <div class="feat"><i class="bi bi-check2-circle"></i><span>Hen-house, daily transaction and cost-per-egg reports in PDF, Excel and CSV</span></div>
            </div>
            <small class="opacity-75">Chicks &rarr; Grower &rarr; Pre-Lay &rarr; Laying</small>
        </section>
        <section class="login-panel">
            <form id="form1" runat="server" class="login-card" autocomplete="on">
                <div class="d-lg-none d-flex align-items-center gap-2 mb-4">
                    <span class="brand-mark">P</span><span class="fw-bold fs-5">Phoenix Poultry ERP</span>
                </div>
                <h2 class="fw-bold mb-1">Sign in</h2>
                <p class="text-muted-2 mb-4">Use the account given by your administrator.</p>

                <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger py-2 small">
                    <i class="bi bi-exclamation-triangle me-1"></i><asp:Literal ID="litError" runat="server" />
                </asp:Panel>

                <div class="mb-3">
                    <label class="form-label" for="txtUser">User name</label>
                    <asp:TextBox ID="txtUser" runat="server" CssClass="form-control form-control-lg" MaxLength="50" autocomplete="username" ClientIDMode="Static" />
                </div>
                <div class="mb-4">
                    <label class="form-label" for="txtPassword">Password</label>
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control form-control-lg" TextMode="Password" MaxLength="100" autocomplete="current-password" ClientIDMode="Static" />
                </div>
                <asp:Button ID="btnLogin" runat="server" Text="Sign in" CssClass="btn btn-primary btn-lg w-100" OnClick="btnLogin_Click" />
                <p class="small text-muted-2 mt-4 mb-0"><i class="bi bi-shield-lock me-1"></i>Accounts lock for 15 minutes after repeated failed attempts.</p>
            </form>
        </section>
    </div>
</body>
</html>
