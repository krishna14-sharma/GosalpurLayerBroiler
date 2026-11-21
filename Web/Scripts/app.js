/* Phoenix Poultry ERP - shared client script (works with ASP.NET AJAX UpdatePanels) */
(function (w, $) {
    'use strict';
    var App = w.App = w.App || {};

    App.toast = function (msg, type) {
        type = type || 'info';
        var icons = { success: 'check-circle-fill', danger: 'exclamation-octagon-fill', warning: 'exclamation-triangle-fill', info: 'info-circle-fill' };
        var $c = $('#toastHost');
        if (!$c.length) { $c = $('<div id="toastHost" class="toast-container position-fixed top-0 end-0 p-3" style="z-index:2100"></div>').appendTo('body'); }
        var $t = $('<div class="toast align-items-center border-0 text-bg-' + type + '" role="alert" aria-live="assertive" aria-atomic="true">' +
            '<div class="d-flex"><div class="toast-body"><i class="bi bi-' + (icons[type] || icons.info) + ' me-2"></i><span></span></div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div></div>');
        $t.find('.toast-body span').text(msg);
        $c.append($t);
        var t = new bootstrap.Toast($t[0], { delay: type === 'danger' ? 7000 : 3500 });
        $t.on('hidden.bs.toast', function () { $t.remove(); });
        t.show();
    };

    App.fmt = function (n, d) {
        if (n === null || n === undefined || isNaN(n)) return '-';
        return Number(n).toLocaleString('en-IN', { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 });
    };

    App.num = function (sel) {
        var v = parseFloat(String($(sel).val() || '').replace(/,/g, ''));
        return isNaN(v) ? 0 : v;
    };

    /* Calls a [WebMethod] on the current page (PageMethods) and returns a promise. */
    App.pageMethod = function (method, data) {
        var url = w.location.pathname + '/' + method;
        return $.ajax({ type: 'POST', url: url, data: JSON.stringify(data || {}), contentType: 'application/json; charset=utf-8', dataType: 'json' })
            .then(function (r) { return r && r.d !== undefined ? r.d : r; });
    };

    App.init = function () {
        // select all on focus for quick numeric entry on tablets
        $('input[type=number], input.num').off('focus.erp').on('focus.erp', function () { var el = this; setTimeout(function () { try { el.select(); } catch (e) { } }, 0); });
        // block mouse-wheel changing number inputs
        $('input[type=number]').off('wheel.erp').on('wheel.erp', function () { $(this).blur(); });
        $('[data-bs-toggle="tooltip"]').each(function () { if (!bootstrap.Tooltip.getInstance(this)) new bootstrap.Tooltip(this); });
        $(document).trigger('app:init');
    };

    $(function () {
        $(document).on('click', '.sidebar-toggle', function () { $('body').toggleClass('sidebar-open'); });
        $(document).on('click', '.sidebar-backdrop', function () { $('body').removeClass('sidebar-open'); });
        App.init();
        if (w.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            var prm = Sys.WebForms.PageRequestManager.getInstance();
            prm.add_beginRequest(function () { $('body').addClass('busy'); });
            prm.add_endRequest(function (s, args) {
                $('body').removeClass('busy');
                if (args.get_error()) {
                    App.toast('Request failed: ' + args.get_error().message.replace('Sys.WebForms.PageRequestManagerServerErrorException: ', ''), 'danger');
                    args.set_errorHandled(true);
                }
                App.init();
            });
        }
    });
})(window, jQuery);
