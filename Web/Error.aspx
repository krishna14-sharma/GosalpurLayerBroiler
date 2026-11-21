<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Something went wrong</title><link rel="stylesheet" href="<%= ResolveUrl("~/lib/bootstrap/css/bootstrap.min.css") %>" /></head>
<body class="bg-light"><div class="container py-5" style="max-width:560px">
<div class="card shadow-sm"><div class="card-body p-4">
<h4 class="fw-bold">Something went wrong</h4>
<p class="text-secondary">The error has been recorded in the audit log. Please try again, or contact your administrator if it keeps happening.</p>
<a class="btn btn-success" href="<%= ResolveUrl("~/Default.aspx") %>">Go to dashboard</a>
</div></div></div></body></html>
