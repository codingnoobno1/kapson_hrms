// hr-orgchart.js — Interactive organisation chart for the Kapson HR portal.
// Ported from hr1 OrgChartSystem.Blazor (wwwroot/Scripts/js/orgchart.js v2) without jQuery,
// SweetAlert or a JSON endpoint: Blazor hands the flattened hierarchy straight to render().
(function () {
    'use strict';

    // Department palette (blue / yellow / teal theme from hr1). Light headers get dark text.
    var DEPT_COLORS = {
        'HR': '#1565C0',
        'FINOPS': '#FFD600',
        'DIGITAL': '#0288D1',
        'RETAIL': '#0097A7',
        'BRAND': '#6A4FC8'
    };
    var LIGHT_HEADERS = { 'FINOPS': true };
    var FALLBACK_COLORS = ['#1976D2', '#00897B', '#5E35B1', '#EF6C00', '#2E7D32', '#AD1457'];

    function deptColor(code) {
        var key = (code || '').toUpperCase();
        if (DEPT_COLORS[key]) return DEPT_COLORS[key];
        var hash = 0;
        for (var i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
        return FALLBACK_COLORS[Math.abs(hash) % FALLBACK_COLORS.length];
    }

    function esc(str) {
        if (str === null || str === undefined) return '';
        return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function initials(name) {
        var parts = String(name || '?').trim().split(/\s+/);
        return ((parts[0] || '?').charAt(0) + (parts.length > 1 ? parts[parts.length - 1].charAt(0) : '')).toUpperCase();
    }

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            if (document.querySelector('script[data-src="' + src + '"]')) { resolve(); return; }
            var s = document.createElement('script');
            s.src = src;
            s.async = true;
            s.dataset.src = src;
            s.onload = function () { resolve(); };
            s.onerror = function () { reject(new Error('Could not load ' + src)); };
            document.head.appendChild(s);
        });
    }

    function OrgChart(rootId, nodes, options) {
        this.root = document.getElementById(rootId);
        this.viewport = this.root.querySelector('.ocx-viewport');
        this.inner = this.root.querySelector('.ocx-inner');
        this.options = options || {};
        this.chartData = nodes || [];
        this.filteredData = this.chartData.slice();
        this.collapsed = new Set();
        this.selected = null;
        this.nodeEls = new Map();
        this.zoom = 1;
        this.listeners = [];
        this.bindToolbar();
        this.bindPan();
        this.buildContextMenu();
        this.render();
    }

    OrgChart.prototype.on = function (target, type, handler, opts) {
        if (!target) return;
        target.addEventListener(type, handler, opts);
        this.listeners.push(function () { target.removeEventListener(type, handler, opts); });
    };

    OrgChart.prototype.q = function (sel) { return this.root.querySelector(sel); };

    // ─── Rendering ───────────────────────────────────────────────
    OrgChart.prototype.render = function () {
        var self = this;
        this.inner.innerHTML = '';
        this.nodeEls.clear();

        var data = this.filteredData;
        var ids = new Set(data.map(function (n) { return n.id; }));
        var roots = data.filter(function (n) { return !n.pid || !ids.has(n.pid); });

        var tree = document.createElement('div');
        tree.className = 'ocx-tree';
        roots.forEach(function (r) { tree.appendChild(self.buildSubtree(r, data)); });

        if (roots.length === 0) {
            tree.innerHTML = '<div class="ocx-empty"><strong>No employees found</strong><span>Clear the filters or add reporting managers.</span></div>';
        }

        this.inner.appendChild(tree);
        requestAnimationFrame(function () { self.drawConnectors(); });
    };

    OrgChart.prototype.buildSubtree = function (node, data) {
        var self = this;
        var wrap = document.createElement('div');
        wrap.className = 'ocx-node-wrap';
        var card = this.buildCard(node);
        wrap.appendChild(card);
        this.nodeEls.set(node.id, card);

        var children = data.filter(function (n) { return n.pid === node.id; });
        if (children.length > 0 && !this.collapsed.has(node.id)) {
            var row = document.createElement('div');
            row.className = 'ocx-children';
            children.forEach(function (c) { row.appendChild(self.buildSubtree(c, data)); });
            wrap.appendChild(row);
        }
        return wrap;
    };

    OrgChart.prototype.buildCard = function (node) {
        var self = this;
        var hasChildren = this.chartData.some(function (n) { return n.pid === node.id; });
        var isCollapsed = this.collapsed.has(node.id);
        var code = (node.departmentCode || '').toUpperCase();
        var color = deptColor(code);
        var text = LIGHT_HEADERS[code] ? '#1a1a1a' : '#ffffff';

        var card = document.createElement('div');
        card.className = 'ocx-card' + (node.isActive === false ? ' inactive' : '') + (this.selected && this.selected.id === node.id ? ' selected' : '');
        card.dataset.nodeId = node.id;
        card.dataset.collapsed = isCollapsed ? 'true' : 'false';

        var photo = node.photoUrl
            ? '<img class="ocx-photo" src="' + esc(node.photoUrl) + '" alt="' + esc(node.name) + '" />'
            : '<span class="ocx-photo ocx-initials" style="background:' + color + ';color:' + text + '">' + esc(initials(node.name)) + '</span>';

        card.innerHTML =
            '<div class="ocx-head" style="background:' + color + ';color:' + text + '">' +
                '<span class="ocx-name" title="' + esc(node.name) + '">' + esc(node.name) + '</span>' +
                '<span class="ocx-code">' + esc(node.empCode) + '</span>' +
            '</div>' +
            '<div class="ocx-body">' +
                photo +
                '<div class="ocx-info">' +
                    '<div class="ocx-dept" title="' + esc(node.department) + '">' + esc(node.department) + '</div>' +
                    '<div class="ocx-title" title="' + esc(node.designation) + '">' + esc(node.designation || 'No designation') + '</div>' +
                    (node.division ? '<div class="ocx-meta">' + esc(node.division) + (node.grade ? ' · ' + esc(node.grade) : '') + '</div>' : '') +
                    (node.directReportCount > 0
                        ? '<div class="ocx-reports">👥 ' + node.directReportCount + ' report' + (node.directReportCount > 1 ? 's' : '') +
                          (node.teamSize > node.directReportCount ? ' · team ' + node.teamSize : '') + '</div>'
                        : '') +
                '</div>' +
            '</div>' +
            (hasChildren
                ? '<button type="button" class="ocx-toggle" title="' + (isCollapsed ? 'Expand' : 'Collapse') + '">' + (isCollapsed ? '▾' : '▴') + '</button>'
                : '');

        card.addEventListener('click', function (e) {
            if (e.target.closest('.ocx-toggle')) return;
            self.select(node, card);
        });
        card.addEventListener('dblclick', function () {
            if (node.editUrl) window.location.href = node.editUrl;
        });
        var toggle = card.querySelector('.ocx-toggle');
        if (toggle) {
            toggle.addEventListener('click', function (e) {
                e.stopPropagation();
                if (self.collapsed.has(node.id)) self.collapsed.delete(node.id); else self.collapsed.add(node.id);
                self.render();
            });
        }
        card.addEventListener('contextmenu', function (e) {
            e.preventDefault();
            self.showMenu(e, node);
        });
        return card;
    };

    OrgChart.prototype.drawConnectors = function () {
        var self = this;
        var old = this.viewport.querySelector('.ocx-connectors');
        if (old) old.remove();
        if (this.nodeEls.size === 0) return;

        var width = Math.max(this.viewport.scrollWidth, this.viewport.clientWidth);
        var height = Math.max(this.viewport.scrollHeight, this.viewport.clientHeight);
        var ratio = Math.max(1, window.devicePixelRatio || 1);
        var layer = document.createElement('canvas');
        layer.className = 'ocx-connectors';
        layer.setAttribute('aria-hidden', 'true');
        layer.width = Math.ceil(width * ratio);
        layer.height = Math.ceil(height * ratio);
        layer.style.width = width + 'px';
        layer.style.height = height + 'px';
        this.viewport.prepend(layer);
        var origin = layer.getBoundingClientRect();

        var ctx = layer.getContext('2d');
        if (!ctx) return;
        ctx.scale(ratio, ratio);
        ctx.strokeStyle = '#8eadd8';
        ctx.lineWidth = 1.5;
        ctx.lineCap = 'square';

        var pos = function (el) {
            var r = el.getBoundingClientRect();
            return { x: r.left - origin.left + r.width / 2, top: r.top - origin.top, bottom: r.bottom - origin.top };
        };

        var byParent = new Map();
        this.filteredData.forEach(function (n) {
            if (!n.pid || !self.nodeEls.has(n.id) || !self.nodeEls.has(n.pid)) return;
            if (!byParent.has(n.pid)) byParent.set(n.pid, []);
            byParent.get(n.pid).push(self.nodeEls.get(n.id));
        });

        byParent.forEach(function (childEls, pid) {
            var p = pos(self.nodeEls.get(pid));
            var pts = childEls.map(pos);
            var firstTop = Math.min.apply(null, pts.map(function (c) { return c.top; }));
            var mid = p.bottom + Math.max(12, (firstTop - p.bottom) / 2);
            var minX = Math.min.apply(null, pts.map(function (c) { return c.x; }));
            var maxX = Math.max.apply(null, pts.map(function (c) { return c.x; }));
            ctx.beginPath();
            ctx.moveTo(p.x, p.bottom);
            ctx.lineTo(p.x, mid);
            if (maxX - minX > 1) { ctx.moveTo(minX, mid); ctx.lineTo(maxX, mid); }
            pts.forEach(function (c) { ctx.moveTo(c.x, mid); ctx.lineTo(c.x, c.top); });
            ctx.stroke();
        });
    };

    // ─── Selection, breadcrumb, subtree ──────────────────────────
    OrgChart.prototype.select = function (node, card) {
        this.root.querySelectorAll('.ocx-card.selected').forEach(function (el) { el.classList.remove('selected'); });
        card.classList.add('selected');
        this.selected = node;
        this.updateBreadcrumb(node.breadcrumb);
        card.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
    };

    OrgChart.prototype.updateBreadcrumb = function (path) {
        var bar = this.q('.ocx-breadcrumb');
        if (!bar) return;
        if (!path) { bar.hidden = true; return; }
        var parts = path.split(' > ');
        bar.innerHTML = '<span class="ocx-bc-label">Reporting line</span>' + parts.map(function (p, i) {
            return '<span class="' + (i === parts.length - 1 ? 'ocx-bc-current' : 'ocx-bc-item') + '">' + esc(p.trim()) + '</span>';
        }).join('<span class="ocx-bc-sep">›</span>');
        bar.hidden = false;
    };

    OrgChart.prototype.subtree = function (rootId) {
        var self = this;
        var result = [];
        var root = this.chartData.find(function (n) { return n.id === rootId; });
        if (!root) return result;
        result.push(Object.assign({}, root, { pid: null }));
        var collect = function (pid) {
            self.chartData.filter(function (n) { return n.pid === pid; }).forEach(function (c) { result.push(c); collect(c.id); });
        };
        collect(rootId);
        return result;
    };

    // ─── Toolbar: search, focus, reset, zoom, export ─────────────
    OrgChart.prototype.bindToolbar = function () {
        var self = this;
        var search = this.q('.ocx-search input');
        var debounce;
        this.on(search, 'input', function () {
            clearTimeout(debounce);
            debounce = setTimeout(function () { self.highlight(search.value.trim().toLowerCase()); }, 250);
        });
        this.on(this.q('[data-oc="clear"]'), 'click', function () { search.value = ''; self.highlight(''); });

        this.on(this.q('[data-oc="focus"]'), 'click', function () {
            if (!self.selected) { self.toast('Click an employee card first, then press Focus.'); return; }
            self.filteredData = self.subtree(self.selected.id);
            self.collapsed.clear();
            self.render();
        });
        this.on(this.q('[data-oc="reset"]'), 'click', function () { self.reset(); });
        this.on(this.q('[data-oc="expand"]'), 'click', function () { self.collapsed.clear(); self.render(); });
        this.on(this.q('[data-oc="collapse"]'), 'click', function () {
            self.collapsed.clear();
            self.filteredData.forEach(function (n) {
                if (n.pid && self.filteredData.some(function (c) { return c.pid === n.id; })) self.collapsed.add(n.id);
            });
            self.render();
        });

        var slider = this.q('.ocx-zoom input');
        var zoomVal = this.q('.ocx-zoom span');
        var applyZoom = function () {
            var oldZoom = self.zoom;
            var newZoom = parseInt(slider.value, 10) / 100;
            var cx = self.viewport.scrollLeft + self.viewport.clientWidth / 2;
            var cy = self.viewport.scrollTop + self.viewport.clientHeight / 2;
            self.inner.style.zoom = String(newZoom);
            self.zoom = newZoom;
            if (zoomVal) zoomVal.textContent = slider.value + '%';
            self.viewport.scrollLeft = cx * (newZoom / oldZoom) - self.viewport.clientWidth / 2;
            self.viewport.scrollTop = cy * (newZoom / oldZoom) - self.viewport.clientHeight / 2;
            requestAnimationFrame(function () { self.drawConnectors(); });
        };
        this.on(slider, 'input', applyZoom);
        this.on(this.viewport, 'wheel', function (e) {
            if (!e.ctrlKey) return;
            e.preventDefault();
            var v = Math.max(15, Math.min(150, parseInt(slider.value, 10) + (e.deltaY < 0 ? 5 : -5)));
            slider.value = String(v);
            applyZoom();
        }, { passive: false });
        this.on(this.q('[data-oc="fit"]'), 'click', function () {
            var tree = self.inner.querySelector('.ocx-tree');
            if (!tree) return;
            var natural = Math.max(1, tree.getBoundingClientRect().width / (self.zoom || 1));
            var fit = Math.floor(Math.max(15, Math.min(100, (self.viewport.clientWidth - 56) / natural * 100)));
            slider.value = String(fit);
            applyZoom();
            self.viewport.scrollLeft = 0;
            self.viewport.scrollTop = 0;
        });

        this.on(window, 'resize', function () { requestAnimationFrame(function () { self.drawConnectors(); }); });
        this.on(this.q('[data-oc="png"]'), 'click', function () { self.exportPng(); });
        this.on(this.q('[data-oc="pdf"]'), 'click', function () { self.exportPdf(); });
        this.on(this.q('[data-oc="print"]'), 'click', function () { window.print(); });
    };

    OrgChart.prototype.reset = function () {
        this.filteredData = this.chartData.slice();
        this.collapsed.clear();
        this.selected = null;
        this.updateBreadcrumb('');
        this.render();
    };

    OrgChart.prototype.highlight = function (term) {
        var self = this;
        this.root.querySelectorAll('.ocx-card.highlight').forEach(function (el) { el.classList.remove('highlight'); });
        if (!term) return;
        var first = null;
        this.chartData.forEach(function (n) {
            var hit = (n.name || '').toLowerCase().indexOf(term) >= 0 ||
                      (n.empCode || '').toLowerCase().indexOf(term) >= 0 ||
                      (n.designation || '').toLowerCase().indexOf(term) >= 0;
            var el = self.nodeEls.get(n.id);
            if (el && hit) { el.classList.add('highlight'); if (!first) first = el; }
        });
        if (first) first.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
    };

    OrgChart.prototype.bindPan = function () {
        var self = this;
        var vp = this.viewport;
        var active = false, moved = false, sx = 0, sy = 0, sl = 0, st = 0;
        this.on(vp, 'pointerdown', function (e) {
            if (e.button !== 0 || e.target.closest('button,input,select,a,.ocx-toggle')) return;
            active = true; moved = false;
            sx = e.clientX; sy = e.clientY; sl = vp.scrollLeft; st = vp.scrollTop;
        });
        this.on(vp, 'pointermove', function (e) {
            if (!active) return;
            var dx = e.clientX - sx, dy = e.clientY - sy;
            if (!moved && Math.abs(dx) + Math.abs(dy) < 5) return;
            if (!moved) {
                try { vp.setPointerCapture(e.pointerId); } catch (_) { }
                document.body.classList.add('ocx-noselect');
                var sel = window.getSelection && window.getSelection();
                if (sel) sel.removeAllRanges();
            }
            moved = true;
            e.preventDefault();
            vp.classList.add('is-panning');
            vp.scrollLeft = sl - dx;
            vp.scrollTop = st - dy;
        });
        var stop = function (e) {
            if (!active) return;
            active = false;
            vp.classList.remove('is-panning');
            document.body.classList.remove('ocx-noselect');
            try { if (vp.hasPointerCapture(e.pointerId)) vp.releasePointerCapture(e.pointerId); } catch (_) { }
            if (moved) setTimeout(function () { moved = false; }, 0);
        };
        this.on(vp, 'pointerup', stop);
        this.on(vp, 'pointercancel', stop);
        this.on(vp, 'click', function (e) { if (moved) { e.preventDefault(); e.stopPropagation(); } }, true);
        this.on(vp, 'scroll', function () { self.hideMenu(); });
    };

    // ─── Export (html2canvas / jsPDF are loaded only when asked for) ─
    OrgChart.prototype.capture = function (scale) {
        var self = this;
        return loadScript('https://cdn.jsdelivr.net/npm/html2canvas@1.4.1/dist/html2canvas.min.js').then(function () {
            var tree = self.inner.querySelector('.ocx-tree');
            return window.html2canvas(tree, { scale: scale, backgroundColor: '#f8fafc' });
        });
    };

    OrgChart.prototype.exportPng = function () {
        var self = this;
        this.capture(2).then(function (canvas) {
            var link = document.createElement('a');
            link.download = 'organisation-chart_' + new Date().toISOString().slice(0, 10) + '.png';
            link.href = canvas.toDataURL('image/png');
            link.click();
        }).catch(function () { self.toast('PNG export needs internet access to load html2canvas. Use Print instead.'); });
    };

    OrgChart.prototype.exportPdf = function () {
        var self = this;
        Promise.all([this.capture(1.5), loadScript('https://cdn.jsdelivr.net/npm/jspdf@2.5.1/dist/jspdf.umd.min.js')])
            .then(function (res) {
                var canvas = res[0];
                var pdf = new window.jspdf.jsPDF('landscape', 'mm', 'a3');
                var pageW = pdf.internal.pageSize.getWidth();
                var pageH = pdf.internal.pageSize.getHeight();
                var w = pageW - 20;
                var h = (canvas.height / canvas.width) * w;
                if (h > pageH - 30) { h = pageH - 30; w = (canvas.width / canvas.height) * h; }
                pdf.setFontSize(14); pdf.setTextColor(21, 101, 192);
                pdf.text((self.options.title || 'Organisation Chart'), 10, 12);
                pdf.setFontSize(9); pdf.setTextColor(120, 120, 120);
                pdf.text('Generated: ' + new Date().toLocaleString(), 10, 18);
                pdf.addImage(canvas.toDataURL('image/png'), 'PNG', 10, 24, w, h);
                pdf.save('organisation-chart_' + new Date().toISOString().slice(0, 10) + '.pdf');
            })
            .catch(function () { self.toast('PDF export needs internet access to load jsPDF. Use Print instead.'); });
    };

    // ─── Context menu ────────────────────────────────────────────
    OrgChart.prototype.buildContextMenu = function () {
        var self = this;
        var menu = document.createElement('div');
        menu.className = 'ocx-menu';
        menu.hidden = true;
        menu.innerHTML =
            '<div class="ocx-menu-title"></div>' +
            '<button type="button" data-act="profile">👤 View / Edit Employee</button>' +
            '<button type="button" data-act="add">➕ Add Direct Report</button>' +
            '<button type="button" data-act="team">👥 View Team</button>' +
            '<button type="button" data-act="path">📋 Copy Reporting Path</button>';
        document.body.appendChild(menu);
        this.menu = menu;
        this.listeners.push(function () { menu.remove(); });

        menu.addEventListener('click', function (e) {
            var btn = e.target.closest('button');
            var node = self.menuNode;
            self.hideMenu();
            if (!btn || !node) return;
            switch (btn.dataset.act) {
                case 'profile': if (node.editUrl) window.location.href = node.editUrl; break;
                case 'add': if (self.options.addUrl) window.location.href = self.options.addUrl + '?managerId=' + encodeURIComponent(node.id); break;
                case 'team':
                    self.selected = node;
                    self.filteredData = self.subtree(node.id);
                    self.collapsed.clear();
                    self.render();
                    self.updateBreadcrumb(node.breadcrumb);
                    break;
                case 'path':
                    if (navigator.clipboard && node.breadcrumb) {
                        navigator.clipboard.writeText(node.breadcrumb).then(function () { self.toast('Reporting path copied.'); });
                    }
                    break;
            }
        });
        this.on(document, 'click', function (e) { if (!menu.contains(e.target)) self.hideMenu(); });
        this.on(document, 'keydown', function (e) { if (e.key === 'Escape') self.hideMenu(); });
    };

    OrgChart.prototype.showMenu = function (e, node) {
        this.menuNode = node;
        this.menu.querySelector('.ocx-menu-title').textContent = node.name + ' · ' + (node.empCode || '');
        this.menu.hidden = false;
        this.menu.style.left = e.clientX + 'px';
        this.menu.style.top = e.clientY + 'px';
        var r = this.menu.getBoundingClientRect();
        if (r.right > window.innerWidth) this.menu.style.left = (e.clientX - r.width) + 'px';
        if (r.bottom > window.innerHeight) this.menu.style.top = (e.clientY - r.height) + 'px';
    };

    OrgChart.prototype.hideMenu = function () { if (this.menu) this.menu.hidden = true; };

    OrgChart.prototype.toast = function (message) {
        var t = this.q('.ocx-toast');
        if (!t) return;
        t.textContent = message;
        t.hidden = false;
        clearTimeout(this.toastTimer);
        this.toastTimer = setTimeout(function () { t.hidden = true; }, 2600);
    };

    OrgChart.prototype.setData = function (nodes) {
        this.chartData = nodes || [];
        this.filteredData = this.chartData.slice();
        this.collapsed.clear();
        this.selected = null;
        this.updateBreadcrumb('');
        this.render();
    };

    OrgChart.prototype.dispose = function () {
        this.listeners.forEach(function (off) { try { off(); } catch (_) { } });
        this.listeners = [];
    };

    var instances = {};

    window.hrOrgChart = {
        render: function (rootId, nodes, options) {
            var root = document.getElementById(rootId);
            if (!root) return;
            if (instances[rootId] && instances[rootId].root === root) {
                instances[rootId].options = options || instances[rootId].options;
                instances[rootId].setData(nodes);
                return;
            }
            if (instances[rootId]) instances[rootId].dispose();
            instances[rootId] = new OrgChart(rootId, nodes, options);
        },
        dispose: function (rootId) {
            if (instances[rootId]) { instances[rootId].dispose(); delete instances[rootId]; }
        }
    };
})();
