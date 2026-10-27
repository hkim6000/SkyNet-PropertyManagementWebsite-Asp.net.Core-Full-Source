var AdminLeasesJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
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
        list.push({ key: 'tok', vlu: getCookie('mr_tok') });
        $ApiRequest('AdminLeases/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'mr_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        var t = getCookie('mr_tok');
        document.cookie = 'mr_tok=; path=' + base() + '; max-age=0';
        setTimeout(function () {
            location.href = 'AdminLogin';
        }, 1500);
        $ApiRequest('AdminLogin/SignOut', JSON.stringify([{ key: 'tok', vlu: t }]));
    }

    function toast(msg) {
        var t = el('ls-toast');
        t.textContent = msg;
        t.classList.add('ls-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ls-open');
        }, 3400);
    }

    function openMenu() {
        el('ls-rail').classList.add('ls-open');
        el('ls-scrim').classList.add('ls-open');
    }

    function closeMenu() {
        el('ls-rail').classList.remove('ls-open');
        el('ls-scrim').classList.remove('ls-open');
    }

    function openModal(title) {
        var m = el('ls-modal');
        el('ls-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ls-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ls-modal');
        if (m) {
            m.classList.remove('ls-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ls-modal');
        if (!m || !m.classList.contains('ls-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ls-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ls-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ls-ask';
        askBox.innerHTML = '<div class="ls-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ls-err" role="alert"></em><div class="ls-askacts"><button type="button" class="ls-btn ls-btn-ghost"></button><button type="button" class="ls-btn"></button></div></div>';
        document.getElementById('ls-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        on(window, 'keydown', function (e) {
            if (!askBox.classList.contains('ls-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ls-btn' + (o.danger ? ' ls-askdel' : '');
        askBox.firstChild.classList.toggle('ls-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ls-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.hasAttribute('data-src')) {
                list.push({ key: n.getAttribute('data-k'), vlu: n.getAttribute('data-src') || '' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }


    var pkDone = null;
    var pkTimer = null;

    var pkPg = 1;

    function pkList(n) {
        pkPg = typeof n === 'number' ? n : 1;
        api('MediaPick', [{ key: 'ppg', vlu: String(pkPg) }, { key: 'pkind', vlu: 'photo' }, { key: 'pterm', vlu: val('ls-pkq') }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList();
        }, 300);
    }

    function pkOpen() {
        var d = el('ls-pk');
        return d && d.classList.contains('ls-open');
    }

    function pkClose() {
        var d = el('ls-pk');
        if (d) {
            d.classList.remove('ls-open');
            d.setAttribute('aria-hidden', 'true');
        }
        el('ls-pkfile').value = '';
        pkDone = null;
    }

    function pickMedia(done) {
        pkDone = done;
        el('ls-pkq').value = '';
        el('ls-pkmsg').textContent = '';
        var d = el('ls-pk');
        d.classList.add('ls-open');
        d.setAttribute('aria-hidden', 'false');
        pkList();
        setTimeout(function () {
            el('ls-pkq').focus();
        }, 30);
    }

    function pkPick(b) {
        var f = pkDone;
        var it = { src: b.getAttribute('data-src'), thumb: b.getAttribute('data-thumb') };
        pkClose();
        if (f) {
            f(it);
        }
    }

    function pkUpload() {
        el('ls-pkmsg').textContent = 'Uploading.';
        api('MediaUp');
    }

    function pkUploaded(ok, msg) {
        el('ls-pkfile').value = '';
        el('ls-pkmsg').textContent = msg;
        if (ok) {
            el('ls-pkq').value = '';
        }
    }

    function paintPic(box, it) {
        var b = box.querySelector('.ls-picb');
        box.setAttribute('data-src', it ? it.src : '');
        b.innerHTML = it ? '<img src="' + esc(it.thumb) + '" alt="">' : '<span>' + esc(box.getAttribute('data-ph') || 'Choose a picture') + '</span>';
        var x = box.querySelector('.ls-picx');
        if (x) {
            x.hidden = !it;
        }
    }

    function pic(btn) {
        var box = btn.closest('.ls-pic');
        pickMedia(function (it) {
            paintPic(box, it);
            markDirty();
        });
    }

    function picClear(btn) {
        paintPic(btn.closest('.ls-pic'), null);
        markDirty();
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }



    function denied() {
        location.href = 'Admin';
    }

    var tabNow = 'Active';
    var unitNow = '';

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function extra() {
        return [{ key: 'tab', vlu: tabNow }, { key: 'prop', vlu: el('ls-fp') ? el('ls-fp').value : '' }, { key: 'unit', vlu: unitNow }];
    }

    function first() {
        return [{ key: 'lcode', vlu: qs('code') }, { key: 'unit', vlu: qs('u') }];
    }

    function tab(t) {
        tabNow = t;
        list(true);
    }

    function setTab(t, u) {
        tabNow = t;
        unitNow = u || '';
    }

    function allUnits() {
        unitNow = '';
        var c = el('ls-uchip');
        if (c) {
            c.remove();
        }
        list(true);
    }

    function add() {
        api('Edit', [{ key: 'id', vlu: '' }, { key: 'unit', vlu: unitNow }]);
    }

    function unitPick(s) {
        var o = s.options[s.selectedIndex];
        if (!o || !o.value) {
            return;
        }
        if (!val('ls-f-crent')) {
            el('ls-f-crent').value = o.getAttribute('data-rent');
        }
        if (!val('ls-f-cdep')) {
            el('ls-f-cdep').value = o.getAttribute('data-dep');
        }
        var from = o.getAttribute('data-from');
        if (from && val('ls-f-cstart') < from) {
            el('ls-f-cstart').value = from;
            var p = from.split('-');
            var e = new Date(+p[0] + 1, +p[1] - 1, +p[2] - 1);
            el('ls-f-cend').value = e.getFullYear() + '-' + String(e.getMonth() + 1).padStart(2, '0') + '-' + String(e.getDate()).padStart(2, '0');
        }
    }

    function create() {
        api('Create', collect('ls-new', []).concat(filters()));
        return false;
    }

    function save() {
        api('Save', collect('ls-edit', [{ key: 'id', vlu: val('ls-f-id') }]).concat(filters()));
        return false;
    }

    function step(act) {
        api('StepForm', [{ key: 'id', vlu: val('ls-f-id') }, { key: 'act', vlu: act }]);
    }

    function stepShown() {
        var b = el('ls-stepbox');
        b.scrollIntoView({ behavior: 'auto', block: 'nearest' });
        var i = b.querySelector('input');
        if (i) {
            i.focus();
        }
    }

    function stepClose() {
        el('ls-stepbox').innerHTML = '';
    }

    function stepGo(act) {
        api('Step', [{ key: 'id', vlu: val('ls-f-id') }, { key: 'act', vlu: act }, { key: 'adate', vlu: val('ls-f-adate') }, { key: 'aend', vlu: val('ls-f-aend') }, { key: 'arent', vlu: val('ls-f-arent') }].concat(filters()));
    }

    function deposit() {
        api('DepositForm', [{ key: 'id', vlu: val('ls-f-id') }]);
    }

    function depSum() {
        var f = el('ls-depform');
        if (!f) {
            return;
        }
        var held = parseFloat(f.getAttribute('data-held')) || 0, bal = parseFloat(f.getAttribute('data-bal')) || 0, ded = 0;
        f.querySelectorAll('[data-k^="da"]').forEach(function (i) {
            var v = parseFloat((i.value || '').replace(/[$,]/g, ''));
            if (v > 0) {
                ded += v;
            }
        });
        var r = held - Math.max(0, bal) - ded + Math.max(0, -bal);
        el('ls-depsum').textContent = (r >= 0 ? 'Refund to the resident: $' : 'The resident still owes: $') + Math.abs(r).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function depositGo() {
        ask({ title: 'Finish the deposit statement?', text: 'The deductions and the deposit are added to the account and the statement is final. ' + el('ls-depsum').textContent, ok: 'Finish', no: 'Cancel' }, function () {
            api('DepositSave', collect('ls-depform', [{ key: 'id', vlu: val('ls-f-id') }]).concat(filters()));
        });
        return false;
    }

    function printDep() {
        document.body.classList.add('ls-printdep');
        window.print();
        setTimeout(function () {
            document.body.classList.remove('ls-printdep');
        }, 500);
    }

    function del(id, code) {
        ask({ title: 'Delete ' + code + '?', text: 'The lease and its charges are removed for good. An application linked to it goes back to approved.', ok: 'Delete lease', no: 'Keep it', danger: true }, function () {
            api('Delete', [{ key: 'id', vlu: id }].concat(filters()));
        });
    }

    var own = { deposit: deposit, depSum: depSum, depositGo: depositGo, printDep: printDep, tab: tab, setTab: setTab, allUnits: allUnits, add: add, unitPick: unitPick, create: create, save: save, step: step, stepShown: stepShown, stepClose: stepClose, stepGo: stepGo, del: del };

    var pg = 1;
    var mtimer = null;

    function filters() {
        return [{ key: 'term', vlu: val('ls-q') }, { key: 'pg', vlu: String(pg) }].concat(extra());
    }

    function list(reset) {
        if (reset) {
            pg = 1;
        }
        api('List', filters());
    }

    function type() {
        clearTimeout(mtimer);
        mtimer = setTimeout(function () {
            list(true);
        }, 250);
    }

    function go(n) {
        pg = n;
        list(false);
    }

    function opened(title) {
        openModal(title);
    }

    function saved() {
        clean();
        shut();
        toast('Saved');
    }

    function add() {
        api('Edit', [{ key: 'id', vlu: '' }]);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function deleted() {
        clean();
        shut();
        toast('Deleted');
    }

    var offs = [];
    var here = '';
    var navBusy = false;

    function on(t, type, fn, opt) {
        t.addEventListener(type, fn, opt);
        offs.push([t, type, fn, opt]);
    }

    function leave() {
        offs.forEach(function (x) {
            x[0].removeEventListener(x[1], x[2], x[3]);
        });
        offs = [];
        clearTimeout(ttimer);
        if (typeof mtimer !== 'undefined') {
            clearTimeout(mtimer);
        }
        if (typeof pkTimer !== 'undefined') {
            clearTimeout(pkTimer);
        }
    }

    function leaveOk(go) {
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Stay', danger: true }, function () {
                dirty = false;
                go();
            });
            return;
        }
        go();
    }

    function abs(u) {
        return new URL(u, location.href).href;
    }

    function swapClass(root, np) {
        [].forEach.call(root.querySelectorAll('[class]'), function (n) {
            n.className = n.className.split(/\s+/).map(function (c) {
                return c.indexOf('ls-') === 0 ? np + '-' + c.slice(3) : c;
            }).join(' ');
        });
    }

    function carry(id, doc, np, page) {
        var from = el('ls-' + id), to = doc.getElementById(np + '-' + id);
        if (!from || !to || !from.innerHTML) {
            return;
        }
        to.innerHTML = from.innerHTML;
        swapClass(to, np);
        if (page) {
            [].forEach.call(to.querySelectorAll('a'), function (a) {
                a.classList.toggle(np + '-on', a.getAttribute('href') === page);
            });
        }
    }

    function navTo(href, push) {
        if (navBusy) {
            return;
        }
        navBusy = true;
        var page = href.split('?')[0];
        fetch(href, { credentials: 'same-origin' }).then(function (r) {
            if (!r.ok || /AdminLogin/.test(r.url)) {
                throw new Error('page');
            }
            return r.text();
        }).then(function (html) {
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var root = doc.querySelector('[id$="-site-root"]');
            if (!root) {
                throw new Error('page');
            }
            var np = root.id.replace(/-site-root$/, '');
            var have = [].map.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                return abs(l.getAttribute('href'));
            });
            var want = [].map.call(doc.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                return l.getAttribute('href');
            });
            var loads = want.filter(function (h) {
                return have.indexOf(abs(h)) < 0;
            }).map(function (h) {
                return new Promise(function (ok) {
                    var n = document.createElement('link');
                    n.rel = 'stylesheet';
                    n.href = h;
                    n.onload = n.onerror = function () {
                        ok();
                    };
                    setTimeout(ok, 4000);
                    document.head.appendChild(n);
                });
            });
            return Promise.all(loads).then(function () {
                return { doc: doc, np: np, want: want.map(abs) };
            });
        }).then(function (x) {
            var doc = x.doc, np = x.np;
            var srcs = [].map.call(doc.querySelectorAll('script[src]'), function (n) {
                return n.getAttribute('src');
            });
            var rail = el('ls-rail');
            var top = rail ? rail.scrollTop : 0;
            carry('menu', doc, np, page);
            carry('who', doc, np, '');
            carry('brand', doc, np, '');
            leave();
            closeMenu();
            if (push) {
                history.pushState({ nav: 1 }, '', href);
            }
            document.title = doc.title;
            document.body.className = doc.body.className;
            [].forEach.call(doc.body.querySelectorAll('script'), function (s) {
                s.parentNode.removeChild(s);
            });
            document.body.innerHTML = doc.body.innerHTML;
            var r2 = document.getElementById(np + '-rail');
            if (r2) {
                r2.scrollTop = top;
            }
            window.scrollTo(0, 0);
            [].forEach.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                if (x.want.indexOf(abs(l.getAttribute('href'))) < 0) {
                    l.parentNode.removeChild(l);
                }
            });
            var loaded = [].map.call(document.querySelectorAll('script[src]'), function (s) {
                return abs(s.getAttribute('src'));
            });
            srcs.forEach(function (src) {
                var mine = /(^|\/)scripts\/Admin[A-Za-z]*\.js/.test(src);
                if (!mine && loaded.indexOf(abs(src)) >= 0) {
                    return;
                }
                if (mine) {
                    [].forEach.call(document.querySelectorAll('script[src]'), function (o) {
                        if (/(^|\/)scripts\/Admin[A-Za-z]*\.js/.test(o.getAttribute('src'))) {
                            o.parentNode.removeChild(o);
                        }
                    });
                }
                var n = document.createElement('script');
                n.src = src;
                document.head.appendChild(n);
            });
        }).catch(function () {
            location.href = href;
        });
    }

    var openKey = 'pm-menu-open';

    function openList() {
        try {
            var v = JSON.parse(localStorage.getItem(openKey) || '[]');
            return Array.isArray(v) ? v : [];
        } catch (err) {
            return [];
        }
    }

    function openSave(list) {
        try {
            localStorage.setItem(openKey, JSON.stringify(list));
        } catch (err) {
        }
    }

    function groupSet(b, closed) {
        var g = b.nextElementSibling;
        b.setAttribute('aria-expanded', closed ? 'false' : 'true');
        b.classList.toggle('ls-shut', closed);
        if (g) {
            g.hidden = closed;
        }
    }

    function menuApply() {
        var m = el('ls-menu');
        if (!m) {
            return;
        }
        var open = openList();
        [].forEach.call(m.querySelectorAll('.ls-mg'), function (b) {
            var g = b.nextElementSibling;
            groupSet(b, open.indexOf(b.getAttribute('data-g')) < 0 && !(g && g.querySelector('.ls-on')));
        });
    }

    function groupToggle(b) {
        var closed = b.getAttribute('aria-expanded') === 'true';
        var k = b.getAttribute('data-g') || '';
        var list = openList().filter(function (v) {
            return v !== k;
        });
        if (!closed) {
            list.push(k);
        }
        groupSet(b, closed);
        openSave(list);
    }

    function menuClick(e) {
        var b = e.target.closest ? e.target.closest('.ls-mg') : null;
        if (b) {
            groupToggle(b);
            return;
        }
        var a = e.target.closest ? e.target.closest('a') : null;
        if (!a || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || a.target) {
            return;
        }
        var href = a.getAttribute('href') || '';
        if (!/^Admin[A-Za-z]*$/.test(href) || !window.fetch || !window.DOMParser || !history.pushState) {
            return;
        }
        e.preventDefault();
        leaveOk(function () {
            [].forEach.call(el('ls-menu').querySelectorAll('a'), function (n) {
                n.classList.toggle('ls-on', n === a);
            });
            navTo(href, true);
        });
    }

    function navInit() {
        var m = el('ls-menu');
        if (m) {
            m.addEventListener('click', menuClick);
            if (window.MutationObserver) {
                new MutationObserver(menuApply).observe(m, { childList: true });
            }
            menuApply();
        }
        here = location.pathname + location.search;
        if (!history.state || !history.state.nav) {
            history.replaceState({ nav: 1 }, '', location.href);
        }
        on(window, 'popstate', function () {
            if (location.pathname + location.search === here) {
                return;
            }
            navTo(location.pathname.replace(/^.*\//, '') + location.search, false);
        });
    }

    function reveal() {
        navInit();
        on(document, 'keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ls-open')) {
                    return;
                }
                if (pkOpen()) {
                    pkClose();
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ls-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var d = el('ls-pk');
        if (d) {
            d.addEventListener('mousedown', function (e) {
                if (e.target === d) {
                    pkClose();
                }
            });
        }
        api('Load', first());
    }

    var base0 = {
        reveal: reveal,
        expired: expired,
        signout: signout,
        denied: denied,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        pic: pic,
        picClear: picClear,
        pkList: pkList,
        pkSearch: pkSearch,
        pkPick: pkPick,
        pkClose: pkClose,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded,
        list: list,
        type: type,
        go: go,
        opened: opened,
        saved: saved,
        add: add,
        edit: edit,
        deleted: deleted
    };
    Object.keys(own).forEach(function (k) {
        base0[k] = own[k];
    });
    return base0;
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLeasesJs.reveal);
} else {
    AdminLeasesJs.reveal();
}
