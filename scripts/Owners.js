var OwnersJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Owners/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('ow-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ow-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ow-open');
        }, 3600);
    }

    function openNav() {
        el('ow-nav').classList.add('ow-open');
        el('ow-scrim').classList.add('ow-open');
    }

    function closeNav() {
        el('ow-nav').classList.remove('ow-open');
        el('ow-scrim').classList.remove('ow-open');
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
        var box = el('ow-slides');
        return box ? box.querySelectorAll('.ow-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#ow-slides .ow-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('ow-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('ow-on', on);
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
        var box = el('ow-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.ow-sctl');
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

    function send() {
        el('ow-send').disabled = true;
        call('Ask', [
            { key: 'name', vlu: val('ow-name') },
            { key: 'email', vlu: val('ow-email') },
            { key: 'phone', vlu: val('ow-phone') },
            { key: 'addr', vlu: val('ow-addr') },
            { key: 'kind', vlu: el('ow-kind').value },
            { key: 'homes', vlu: val('ow-homes') },
            { key: 'state', vlu: el('ow-state').value },
            { key: 'msg', vlu: val('ow-msg') },
            { key: 'web', vlu: val('ow-web') }
        ]);
        return false;
    }

    function failed() {
        el('ow-send').disabled = false;
        var bad = document.querySelector('.ow-err:not(:empty)');
        if (bad) {
            var f = bad.parentNode.querySelector('input,textarea,select');
            if (f) {
                f.focus();
            }
        }
    }

    function sent() {
        el('ow-send').disabled = false;
        el('ow-form').reset();
        popOpen();
    }

    var popBack = null;

    function popOpen() {
        var p = el('ow-pop');
        if (!p) {
            return;
        }
        popBack = document.activeElement;
        p.classList.add('ow-open');
        p.setAttribute('aria-hidden', 'false');
        el('ow-pop-ok').focus();
    }

    function popClose() {
        var p = el('ow-pop');
        if (!p || !p.classList.contains('ow-open')) {
            return;
        }
        p.classList.remove('ow-open');
        p.setAttribute('aria-hidden', 'true');
        if (popBack && popBack.focus && document.body.contains(popBack)) {
            popBack.focus();
        }
        popBack = null;
    }

    function start() {
        var p = el('ow-pop');
        if (!p) {
            return;
        }
        p.addEventListener('mousedown', function (e) {
            if (e.target === p) {
                popClose();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (!p.classList.contains('ow-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                popClose();
            } else if (e.key === 'Tab') {
                e.preventDefault();
                el('ow-pop-ok').focus();
            }
        });
    }

    var own = { send: send, failed: failed, sent: sent, popClose: popClose };

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
    document.addEventListener('DOMContentLoaded', OwnersJs.setup);
} else {
    OwnersJs.setup();
}
