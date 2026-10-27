var UnitJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Unit/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('un-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('un-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('un-open');
        }, 3600);
    }

    function openNav() {
        el('un-nav').classList.add('un-open');
        el('un-scrim').classList.add('un-open');
    }

    function closeNav() {
        el('un-nav').classList.remove('un-open');
        el('un-scrim').classList.remove('un-open');
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
        var box = el('un-slides');
        return box ? box.querySelectorAll('.un-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#un-slides .un-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('un-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('un-on', on);
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
        var box = el('un-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.un-sctl');
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

    var galIdx = 0;

    function galItems() {
        var g = el('un-gal');
        if (!g) {
            return [];
        }
        try {
            var v = JSON.parse(g.getAttribute('data-items') || '[]');
            return Array.isArray(v) ? v : [];
        } catch (err) {
            return [];
        }
    }

    function media(it, big) {
        if (it.k === 'video') {
            return '<video src="' + esc(it.s) + '" controls playsinline preload="metadata"' + (big ? ' autoplay' : '') + ' aria-label="' + esc(it.a) + '"></video>';
        }
        return '<img src="' + esc(it.s) + '" alt="' + esc(it.a) + '"' + (big ? '' : ' decoding="async"') + '>';
    }

    function pick(btn) {
        var main = el('un-galmain');
        var items = galItems();
        var i = parseInt(btn.getAttribute('data-i'), 10) || 0;
        if (!main || !items[i]) {
            return;
        }
        galIdx = i;
        var it = items[i];
        main.innerHTML = it.k === 'video' ? media(it, false) : '<button type="button" class="un-galzoom" onclick="UnitJs.show()" aria-label="Open the picture larger">' + media(it, false) + '</button>';
        if (it.k === 'video') {
            var v = main.querySelector('video');
            if (v && v.play) {
                var p = v.play();
                if (p && p.catch) {
                    p.catch(function () { });
                }
            }
        }
        [].forEach.call(document.querySelectorAll('.un-thumb'), function (t) {
            var on = t === btn;
            t.classList.toggle('un-on', on);
            t.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    var shIdx = 0, shPlay = true, shTimer = null, shBack = null, shX = null, shList = [];

    function shEl() {
        return el('un-show');
    }

    function shIsOpen() {
        var s = shEl();
        return !!(s && s.classList.contains('un-open'));
    }

    function shStop() {
        clearTimeout(shTimer);
        shTimer = null;
    }

    function shArm() {
        shStop();
        if (!shPlay || shList.length < 2 || !shIsOpen()) {
            return;
        }
        if (shList[shIdx].k === 'video') {
            return;
        }
        shTimer = setTimeout(function () {
            showGo(1);
        }, 5000);
    }

    function shPaint() {
        var b = el('un-show-pp');
        if (!b) {
            return;
        }
        b.setAttribute('aria-label', shPlay ? 'Pause the slide show' : 'Play the slide show');
        b.innerHTML = shPlay ? '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M7 5h4v14H7zM13 5h4v14h-4z"/></svg>' : '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z"/></svg>';
    }

    function shRender() {
        var it = shList[shIdx];
        var st = el('un-show-stage');
        if (!it || !st) {
            return;
        }
        st.innerHTML = media(it, true);
        el('un-show-n').textContent = shList.length > 1 ? (shIdx + 1) + ' / ' + shList.length : '';
        el('un-show-t').textContent = it.a || '';
        var v = st.querySelector('video');
        if (v) {
            v.addEventListener('ended', function () {
                if (shPlay && shList.length > 1) {
                    showGo(1);
                }
            });
            var p = v.play ? v.play() : null;
            if (p && p.catch) {
                p.catch(function () { });
            }
        }
        var nx = shList[(shIdx + 1) % shList.length];
        if (nx && nx.k !== 'video') {
            var pre = new Image();
            pre.src = nx.s;
        }
        shArm();
    }

    function show(i) {
        var s = shEl();
        shList = galItems();
        if (!s || !shList.length) {
            return;
        }
        shIdx = typeof i === 'number' ? i : galIdx;
        if (shIdx < 0 || shIdx >= shList.length) {
            shIdx = 0;
        }
        var mv = el('un-galmain') ? el('un-galmain').querySelector('video') : null;
        if (mv && mv.pause) {
            mv.pause();
        }
        shPlay = shList.length > 1;
        shBack = document.activeElement;
        s.classList.toggle('un-single', shList.length < 2);
        s.classList.add('un-open');
        s.setAttribute('aria-hidden', 'false');
        document.documentElement.style.overflow = 'hidden';
        shPaint();
        shRender();
        el('un-showbox').focus();
    }

    function showGo(step) {
        if (!shList.length) {
            return;
        }
        shIdx = (shIdx + step + shList.length) % shList.length;
        shRender();
    }

    function showPlay() {
        shPlay = !shPlay;
        shPaint();
        if (shPlay) {
            var v = el('un-show-stage').querySelector('video');
            if (v && v.ended) {
                showGo(1);
                return;
            }
        }
        shArm();
    }

    function showClose() {
        var s = shEl();
        if (!shIsOpen()) {
            return;
        }
        shStop();
        el('un-show-stage').innerHTML = '';
        s.classList.remove('un-open');
        s.setAttribute('aria-hidden', 'true');
        document.documentElement.style.overflow = '';
        if (shBack && shBack.focus && document.body.contains(shBack)) {
            shBack.focus();
        }
        shBack = null;
    }

    function showSetup() {
        var s = shEl();
        if (!s) {
            return;
        }
        s.addEventListener('mousedown', function (e) {
            if (e.target === s || e.target.id === 'un-show-stage' || e.target.classList.contains('un-showbox')) {
                showClose();
            }
        });
        var st = el('un-show-stage');
        st.addEventListener('touchstart', function (e) {
            shX = e.touches && e.touches.length === 1 ? e.touches[0].clientX : null;
        }, { passive: true });
        st.addEventListener('touchend', function (e) {
            if (shX === null || !e.changedTouches || !e.changedTouches.length) {
                return;
            }
            var dx = e.changedTouches[0].clientX - shX;
            shX = null;
            if (Math.abs(dx) > 50 && shList.length > 1) {
                showGo(dx < 0 ? 1 : -1);
            }
        });
        document.addEventListener('keydown', function (e) {
            if (!shIsOpen()) {
                return;
            }
            var tag = e.target && e.target.tagName;
            if (e.key === 'Escape') {
                e.preventDefault();
                showClose();
            } else if (e.key === 'ArrowRight' && shList.length > 1) {
                e.preventDefault();
                showGo(1);
            } else if (e.key === 'ArrowLeft' && shList.length > 1) {
                e.preventDefault();
                showGo(-1);
            } else if (e.key === ' ' && tag !== 'BUTTON' && tag !== 'VIDEO' && shList.length > 1) {
                e.preventDefault();
                showPlay();
            } else if (e.key === 'Tab') {
                var f = [].filter.call(s.querySelectorAll('button, video'), function (n) {
                    return n.offsetParent !== null;
                });
                if (!f.length) {
                    return;
                }
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? -1 : 1) + f.length) % f.length].focus();
            }
        });
    }

    function label(h) {
        return (h > 12 ? h - 12 : h) + ':00 ' + (h >= 12 ? 'PM' : 'AM');
    }

    function days() {
        var v = val('un-day'), s = el('un-slot');
        var keep = s.value;
        var hours = [10, 11, 13, 14, 15, 16, 17];
        if (v) {
            var p = v.split('-');
            var wd = new Date(+p[0], +p[1] - 1, +p[2]).getDay();
            hours = wd === 6 ? [10, 11, 12] : wd === 0 ? [] : hours;
        }
        s.innerHTML = hours.length ? hours.map(function (h) {
            return '<option value="' + h + '"' + (String(h) === keep ? ' selected' : '') + '>' + label(h) + '</option>';
        }).join('') : '<option value="">No tours on Sunday</option>';
    }

    function send() {
        el('un-send').disabled = true;
        call('Tour', [
            { key: 'unit', vlu: val('un-unit') },
            { key: 'name', vlu: val('un-name') },
            { key: 'email', vlu: val('un-email') },
            { key: 'phone', vlu: val('un-phone') },
            { key: 'day', vlu: val('un-day') },
            { key: 'slot', vlu: el('un-slot').value },
            { key: 'note', vlu: val('un-note') },
            { key: 'web', vlu: val('un-web') }
        ]);
        return false;
    }

    function failed() {
        el('un-send').disabled = false;
        var bad = document.querySelector('.un-err:not(:empty)');
        if (bad) {
            var f = bad.parentNode.querySelector('input,textarea,select');
            if (f) {
                f.focus();
            }
        }
    }

    function sent() {
        el('un-send').disabled = false;
        el('tour').reset();
        days();
        popOpen();
    }

    var popBack = null;

    function popOpen() {
        var p = el('un-pop');
        if (!p) {
            return;
        }
        popBack = document.activeElement;
        p.classList.add('un-open');
        p.setAttribute('aria-hidden', 'false');
        el('un-pop-ok').focus();
    }

    function popClose() {
        var p = el('un-pop');
        if (!p || !p.classList.contains('un-open')) {
            return;
        }
        p.classList.remove('un-open');
        p.setAttribute('aria-hidden', 'true');
        if (popBack && popBack.focus && document.body.contains(popBack)) {
            popBack.focus();
        }
        popBack = null;
    }

    function start() {
        var p = el('un-pop');
        if (!p) {
            return;
        }
        p.addEventListener('mousedown', function (e) {
            if (e.target === p) {
                popClose();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (!p.classList.contains('un-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                popClose();
            } else if (e.key === 'Tab') {
                e.preventDefault();
                el('un-pop-ok').focus();
            }
        });
    }

    var viewBack = null;

    function view(btn) {
        var v = el('un-view'), img = el('un-view-img');
        if (!v || !btn) {
            return;
        }
        var small = btn.querySelector('img');
        img.src = btn.getAttribute('data-src') || (small ? small.src : '');
        img.alt = small ? small.alt : '';
        el('un-view-t').textContent = btn.getAttribute('data-cap') || '';
        viewBack = btn;
        v.classList.add('un-open');
        v.setAttribute('aria-hidden', 'false');
        document.documentElement.style.overflow = 'hidden';
        el('un-view-x').focus();
    }

    function viewClose() {
        var v = el('un-view');
        if (!v || !v.classList.contains('un-open')) {
            return;
        }
        v.classList.remove('un-open');
        v.setAttribute('aria-hidden', 'true');
        document.documentElement.style.overflow = '';
        if (viewBack && viewBack.focus && document.body.contains(viewBack)) {
            viewBack.focus();
        }
        viewBack = null;
    }

    function viewSetup() {
        var v = el('un-view');
        if (!v) {
            return;
        }
        v.addEventListener('mousedown', function (e) {
            if (e.target === v || e.target.classList.contains('un-viewbox')) {
                viewClose();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (!v.classList.contains('un-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                viewClose();
            } else if (e.key === 'Tab') {
                e.preventDefault();
                el('un-view-x').focus();
            }
        });
    }

    var own = { pick: pick, days: days, send: send, failed: failed, sent: sent, popClose: popClose, view: view, viewClose: viewClose, show: show, showGo: showGo, showPlay: showPlay, showClose: showClose };

    function setup() {
        slideSetup();
        start();
        viewSetup();
        showSetup();
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
    document.addEventListener('DOMContentLoaded', UnitJs.setup);
} else {
    UnitJs.setup();
}
