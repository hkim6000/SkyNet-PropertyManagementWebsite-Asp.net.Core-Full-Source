var PortalJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Portal/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('po-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('po-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('po-open');
        }, 3600);
    }

    function openNav() {
        el('po-nav').classList.add('po-open');
        el('po-scrim').classList.add('po-open');
    }

    function closeNav() {
        el('po-nav').classList.remove('po-open');
        el('po-scrim').classList.remove('po-open');
    }

    function find(form) {
        [].forEach.call(form.querySelectorAll('select, input'), function (f) {
            if (!f.value || (f.type === 'checkbox' && !f.checked)) {
                f.disabled = true;
            }
        });
        setTimeout(function () {
            [].forEach.call(form.querySelectorAll('select, input'), function (f) {
                f.disabled = false;
            });
        }, 50);
        return true;
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeNav();
        }
    });

    var sIdx = 0, sTimer = null, sHold = false, sX = null;

    function slides() {
        var box = el('po-slides');
        return box ? box.querySelectorAll('.po-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#po-slides .po-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('po-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('po-on', on);
                dots[k].setAttribute('aria-pressed', on ? 'true' : 'false');
            }
        }
        plan();
    }

    function slide(step) {
        slideTo(sIdx + step);
    }

    function still() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function plan() {
        clearTimeout(sTimer);
        sTimer = null;
        if (sHold || document.hidden || still() || slides().length < 2) {
            return;
        }
        sTimer = setTimeout(function () { slideTo(sIdx + 1); }, 7000);
    }

    function hold(on) {
        sHold = on;
        plan();
    }

    function slideSetup() {
        var box = el('po-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.po-sctl');
        ctl.addEventListener('mouseenter', function () { hold(true); });
        ctl.addEventListener('mouseleave', function () { hold(false); });
        ctl.addEventListener('focusin', function () { hold(true); });
        ctl.addEventListener('focusout', function () { hold(false); });
        box.addEventListener('touchstart', function (e) {
            sX = e.touches[0].clientX;
            hold(true);
        }, { passive: true });
        box.addEventListener('touchend', function (e) {
            var dx = sX === null ? 0 : e.changedTouches[0].clientX - sX;
            sX = null;
            sHold = false;
            if (Math.abs(dx) > 40) {
                slide(dx < 0 ? 1 : -1);
            } else {
                plan();
            }
        });
        document.addEventListener('visibilitychange', plan);
        plan();
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('mr_res') });
        call(method, list);
    }

    function clear() {
        document.cookie = 'mr_res=; path=' + base() + '; max-age=0; samesite=lax';
    }

    function tab(k) {
        ['in', 'act', 'fgt'].forEach(function (x) {
            var on = x === k, b = el('po-tb-' + x), p = el('po-t-' + x);
            if (b) {
                b.classList.toggle('po-on', on);
                b.setAttribute('aria-selected', on ? 'true' : 'false');
            }
            if (p) {
                p.hidden = !on;
            }
        });
        var f = el('po-t-' + k);
        var i = f ? f.querySelector('input') : null;
        if (i) {
            i.focus();
        }
    }

    function signIn() {
        el('po-e-in').textContent = '';
        call('SignIn', [{ key: 'email', vlu: val('po-in-email') }, { key: 'pass', vlu: el('po-in-pass').value }]);
        return false;
    }

    function activate() {
        el('po-e-ac').textContent = '';
        call('Activate', [{ key: 'email', vlu: val('po-ac-email') }, { key: 'code', vlu: val('po-ac-code') }, { key: 'pass', vlu: el('po-ac-pass').value }, { key: 'again', vlu: el('po-ac-again').value }]);
        return false;
    }

    function forgot() {
        el('po-fg-ok').innerHTML = '';
        call('Forgot', [{ key: 'email', vlu: val('po-fg-email') }]);
        return false;
    }

    function reset() {
        el('po-e-rs').textContent = '';
        call('Reset', [{ key: 'rtoken', vlu: val('po-rs-tok') }, { key: 'pass', vlu: el('po-rs-pass').value }, { key: 'again', vlu: el('po-rs-again').value }]);
        return false;
    }

    function signed(token, fresh) {
        document.cookie = 'mr_res=' + token + '; path=' + base() + '; max-age=43200; samesite=lax';
        if (fresh) {
            history.replaceState(null, '', 'Portal');
        }
        api('Load', []);
    }

    function signOut() {
        api('SignOut', []);
    }

    function profile() {
        el('po-e-pf').textContent = '';
        api('SaveProfile', [{ key: 'phone', vlu: val('po-pf-phone') }, { key: 'emerg', vlu: val('po-pf-emerg') }, { key: 'car', vlu: val('po-pf-car') }]);
        return false;
    }

    function password() {
        el('po-e-pw').textContent = '';
        api('Password', [{ key: 'cur', vlu: el('po-pw-cur').value }, { key: 'next', vlu: el('po-pw-new').value }, { key: 'again', vlu: el('po-pw-again').value }]);
        return false;
    }

    function payForm(show) {
        var f = el('po-payform'), b = el('po-pay-open');
        if (!f) {
            return;
        }
        f.hidden = !show;
        b.hidden = show;
        el('po-e-pay').textContent = '';
        if (show) {
            el('po-pay-amt').focus();
        }
    }

    function pay() {
        el('po-e-pay').textContent = '';
        el('po-pay-go').disabled = true;
        api('PayStart', [{ key: 'pamt', vlu: val('po-pay-amt').replace(/[$,]/g, '') }]);
        return false;
    }

    function payFailed() {
        var b = el('po-pay-go');
        if (b) {
            b.disabled = false;
        }
    }

    function go(url) {
        location.href = url;
    }

    function reqForm(show) {
        var f = el('po-rqform'), b = el('po-rq-open');
        if (!f) {
            return;
        }
        f.hidden = !show;
        b.hidden = show;
        if (show) {
            el('po-rq-title').focus();
        } else {
            f.reset();
            el('po-e-rq').textContent = '';
        }
    }

    function request() {
        el('po-e-rq').textContent = '';
        var files = el('po-rq-photos').files;
        if (files && files.length > 3) {
            el('po-e-rq').textContent = 'Choose up to 3 photos.';
            return false;
        }
        el('po-rq-send').disabled = true;
        api('SendRequest', [
            { key: 'rcat', vlu: el('po-rq-cat').value },
            { key: 'rtitle', vlu: val('po-rq-title') },
            { key: 'rdetail', vlu: val('po-rq-detail') },
            { key: 'rpets', vlu: val('po-rq-pets') },
            { key: 'rentry', vlu: el('po-rq-entry').checked ? '1' : '' },
            { key: 'rurgent', vlu: el('po-rq-urgent').checked ? '1' : '' }
        ]);
        return false;
    }

    function reqFailed() {
        var b = el('po-rq-send');
        if (b) {
            b.disabled = false;
        }
    }

    function ready(saved) {
        var n = document.querySelector('#po-portal .po-sent');
        if (saved && n) {
            n.scrollIntoView({ behavior: 'auto', block: 'center' });
            toast(n.textContent);
        }
    }

    function start() {
        window.addEventListener('hashchange', function () {
            if (location.hash === '#activate' && el('po-t-act')) {
                tab('act');
            }
        });
        var box = el('po-portal');
        if (!box || box.getAttribute('data-reset')) {
            return;
        }
        var paid = new URLSearchParams(location.search).get('paid') || '';
        if (paid) {
            history.replaceState(null, '', 'Portal');
        }
        api('Load', [{ key: 'tab', vlu: location.hash === '#activate' ? 'act' : 'in' }, { key: 'psess', vlu: paid }]);
    }

    var own = { payForm: payForm, pay: pay, payFailed: payFailed, go: go, reqForm: reqForm, request: request, reqFailed: reqFailed, tab: tab, signIn: signIn, activate: activate, forgot: forgot, reset: reset, signed: signed, signOut: signOut, profile: profile, password: password, ready: ready, clear: clear };

    function setup() {
        slideSetup();
        start();
    }

    var base0 = {
        setup: setup,
        openNav: openNav,
        closeNav: closeNav,
        toast: toast,
        find: find,
        slide: slide,
        slideTo: slideTo
    };
    Object.keys(own).forEach(function (k) {
        base0[k] = own[k];
    });
    return base0;

})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', PortalJs.setup);
} else {
    PortalJs.setup();
}
