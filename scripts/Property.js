var PropertyJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Property/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('pd-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pd-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pd-open');
        }, 3600);
    }

    function openNav() {
        el('pd-nav').classList.add('pd-open');
        el('pd-scrim').classList.add('pd-open');
    }

    function closeNav() {
        el('pd-nav').classList.remove('pd-open');
        el('pd-scrim').classList.remove('pd-open');
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
        var box = el('pd-slides');
        return box ? box.querySelectorAll('.pd-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#pd-slides .pd-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('pd-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('pd-on', on);
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
        var box = el('pd-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.pd-sctl');
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
        var g = el('pd-gal');
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
        var main = el('pd-galmain');
        var items = galItems();
        var i = parseInt(btn.getAttribute('data-i'), 10) || 0;
        if (!main || !items[i]) {
            return;
        }
        galIdx = i;
        var it = items[i];
        main.innerHTML = it.k === 'video' ? media(it, false) : '<button type="button" class="pd-galzoom" onclick="PropertyJs.show()" aria-label="Open the picture larger">' + media(it, false) + '</button>';
        if (it.k === 'video') {
            var v = main.querySelector('video');
            if (v && v.play) {
                var p = v.play();
                if (p && p.catch) {
                    p.catch(function () { });
                }
            }
        }
        [].forEach.call(document.querySelectorAll('.pd-thumb'), function (t) {
            var on = t === btn;
            t.classList.toggle('pd-on', on);
            t.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    var shIdx = 0, shPlay = true, shTimer = null, shBack = null, shX = null, shList = [];

    function shEl() {
        return el('pd-show');
    }

    function shIsOpen() {
        var s = shEl();
        return !!(s && s.classList.contains('pd-open'));
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
        var b = el('pd-show-pp');
        if (!b) {
            return;
        }
        b.setAttribute('aria-label', shPlay ? 'Pause the slide show' : 'Play the slide show');
        b.innerHTML = shPlay ? '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M7 5h4v14H7zM13 5h4v14h-4z"/></svg>' : '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z"/></svg>';
    }

    function shRender() {
        var it = shList[shIdx];
        var st = el('pd-show-stage');
        if (!it || !st) {
            return;
        }
        st.innerHTML = media(it, true);
        el('pd-show-n').textContent = shList.length > 1 ? (shIdx + 1) + ' / ' + shList.length : '';
        el('pd-show-t').textContent = it.a || '';
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
        var mv = el('pd-galmain') ? el('pd-galmain').querySelector('video') : null;
        if (mv && mv.pause) {
            mv.pause();
        }
        shPlay = shList.length > 1;
        shBack = document.activeElement;
        s.classList.toggle('pd-single', shList.length < 2);
        s.classList.add('pd-open');
        s.setAttribute('aria-hidden', 'false');
        document.documentElement.style.overflow = 'hidden';
        shPaint();
        shRender();
        el('pd-showbox').focus();
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
            var v = el('pd-show-stage').querySelector('video');
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
        el('pd-show-stage').innerHTML = '';
        s.classList.remove('pd-open');
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
            if (e.target === s || e.target.id === 'pd-show-stage' || e.target.classList.contains('pd-showbox')) {
                showClose();
            }
        });
        var st = el('pd-show-stage');
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

    function start() {
    }

    var own = { pick: pick, show: show, showGo: showGo, showPlay: showPlay, showClose: showClose };

    function setup() {
        slideSetup();
        start();
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
    document.addEventListener('DOMContentLoaded', PropertyJs.setup);
} else {
    PropertyJs.setup();
}
