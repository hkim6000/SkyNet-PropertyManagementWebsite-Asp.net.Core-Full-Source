var ApplyJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Apply/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('ap-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3600);
    }

    function openNav() {
        el('ap-nav').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeNav() {
        el('ap-nav').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
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
        var box = el('ap-slides');
        return box ? box.querySelectorAll('.ap-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#ap-slides .ap-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('ap-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('ap-on', on);
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
        var box = el('ap-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.ap-sctl');
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

    var fields = ['first', 'last', 'email', 'phone', 'birth', 'employer', 'income'];

    function count() {
        return parseInt(val('ap-count'), 10) || 1;
    }

    function shown(n) {
        for (var i = 1; i < 4; i++) {
            el('ap-p' + i).hidden = i >= n;
        }
        el('ap-count').value = String(n);
        el('ap-add').hidden = n >= 4;
        var occ = el('ap-occ');
        if ((parseInt(occ.value, 10) || 1) < n) {
            occ.value = String(n);
        }
    }

    function add() {
        var n = count();
        if (n >= 4) {
            return;
        }
        shown(n + 1);
        el('ap-a' + n + '-first').focus();
    }

    function drop(i) {
        var n = count();
        for (var k = i; k < n - 1; k++) {
            fields.forEach(function (f) {
                el('ap-a' + k + '-' + f).value = el('ap-a' + (k + 1) + '-' + f).value;
            });
        }
        fields.forEach(function (f) {
            el('ap-a' + (n - 1) + '-' + f).value = '';
            el('ap-e-a' + (n - 1) + '-' + f).textContent = '';
        });
        shown(n - 1);
    }

    function send() {
        el('ap-send').disabled = true;
        var list = [
            { key: 'unit', vlu: val('ap-unit') },
            { key: 'move', vlu: val('ap-move') },
            { key: 'months', vlu: el('ap-months').value },
            { key: 'occ', vlu: el('ap-occ').value },
            { key: 'pets', vlu: val('ap-pets') },
            { key: 'count', vlu: String(count()) },
            { key: 'addr', vlu: val('ap-addr') },
            { key: 'lord', vlu: val('ap-lord') },
            { key: 'lordph', vlu: val('ap-lordph') },
            { key: 'years', vlu: el('ap-years').value },
            { key: 'msg', vlu: val('ap-msg') },
            { key: 'agree', vlu: el('ap-agree').checked ? '1' : '' },
            { key: 'web', vlu: val('ap-web') }
        ];
        for (var i = 0; i < count(); i++) {
            fields.forEach(function (f) {
                list.push({ key: 'a' + i + f, vlu: val('ap-a' + i + '-' + f) });
            });
        }
        call('Send', list);
        return false;
    }

    function failed() {
        el('ap-send').disabled = false;
        var bad = document.querySelector('.ap-err:not(:empty)');
        if (bad) {
            var f = bad.parentNode.querySelector('input,textarea,select');
            if (!f && bad.previousElementSibling) {
                f = bad.previousElementSibling.querySelector('input');
            }
            if (f) {
                f.focus();
                f.scrollIntoView({ behavior: 'auto', block: 'center' });
            }
        }
    }

    function done() {
        var o = el('ap-out');
        if (o) {
            o.scrollIntoView({ behavior: 'auto', block: 'start' });
        }
    }

    function start() {
    }

    var own = { add: add, drop: drop, send: send, failed: failed, done: done };

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
    document.addEventListener('DOMContentLoaded', ApplyJs.setup);
} else {
    ApplyJs.setup();
}
