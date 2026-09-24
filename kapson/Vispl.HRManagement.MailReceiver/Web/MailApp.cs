namespace Vispl.HRManagement.MailReceiver.Web;

/// <summary>
/// The mail client, served as one page.
///
/// A list and a reading pane that talk to the JSON API, rather than a server-rendered page per
/// message. Selecting a message in a mail client does not reload the window, and a viewer that
/// loses your scroll position and your search every time you open something is a debug list
/// wearing a mail client's clothes.
///
/// Held as strings in one file so the receiver stays a single self-contained project: adding a
/// build step and a node_modules to a development mail catcher would cost more than the thing
/// is worth.
/// </summary>
public static class MailApp
{
    public static string Page() => $"""
        <!doctype html>
        <html lang="en">
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width,initial-scale=1" />
        <title>Mail — Vispl HR</title>
        <style>{Css}</style>
        <body>
          <div class="app">
            <header class="top">
              <button class="icon menu" id="back" title="Back to the list">‹</button>
              <span class="brand">📬 Mail</span>
              <div class="search">
                <input id="q" type="search" placeholder="Search mail" autocomplete="off" />
              </div>
              <button class="compose" id="compose">✏️ Compose</button>
            </header>

            <aside class="list" id="list">
              <div class="list-head">
                <span id="count">—</span>
                <span class="spacer"></span>
                <button class="icon" id="refresh" title="Refresh">⟳</button>
                <button class="icon danger" id="empty" title="Delete everything">🗑</button>
              </div>
              <div class="rows" id="rows"></div>
            </aside>

            <main class="pane" id="pane">
              <div class="blank">
                <div class="blank-mark">📭</div>
                <p id="blank-text">Select a message</p>
                <p class="muted">
                  Anything relayed here appears immediately. Nothing leaves this machine.
                </p>
                <details class="connect">
                  <summary>Connect a mail client</summary>
                  <div class="settings" id="endpoints"></div>
                  <p class="muted" id="endpoint-note"></p>
                </details>
              </div>
            </main>
          </div>

          <div class="scrim" id="scrim" hidden></div>
          <section class="composer" id="composer" hidden>
            <header>
              <strong id="composer-title">New message</strong>
              <button class="icon" id="composer-close" title="Close">✕</button>
            </header>
            <div class="composer-body">
              <label><span>To</span><input id="c-to" type="email" placeholder="someone@example.com" /></label>
              <label><span>Subject</span><input id="c-subject" type="text" /></label>
              <label><span>Message</span><textarea id="c-text" rows="8"></textarea></label>
              <details>
                <summary>HTML body (optional)</summary>
                <textarea id="c-html" rows="6"></textarea>
              </details>
              <p class="composer-note" id="c-note"></p>
            </div>
            <footer>
              <button class="send" id="c-send">Send</button>
              <span class="muted" id="c-route"></span>
            </footer>
          </section>

          <script>{Script}</script>
        </body>
        </html>
        """;

    private const string Css = """
        :root {
          color-scheme: light;
          --line:#e3e9f2; --ink:#16202f; --dim:#64748b; --accent:#2563eb; --bg:#f6f8fc;
        }
        * { box-sizing:border-box; }
        html, body { height:100%; }
        body { margin:0; background:var(--bg); color:var(--ink);
               font:14px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif; }

        .app { display:grid; height:100vh;
               grid-template-columns:minmax(300px,380px) 1fr;
               grid-template-rows:auto 1fr;
               grid-template-areas:"top top" "list pane"; }

        .top { grid-area:top; display:flex; align-items:center; gap:14px;
               padding:10px 16px; background:var(--accent); color:#fff; }
        .brand { font-weight:700; letter-spacing:.01em; white-space:nowrap; }
        .search { flex:1; max-width:520px; }
        .search input { width:100%; padding:8px 13px; border:none; border-radius:999px;
                        background:rgba(255,255,255,.16); color:#fff; font:inherit; }
        .search input::placeholder { color:rgba(255,255,255,.72); }
        .search input:focus { outline:2px solid rgba(255,255,255,.5); background:rgba(255,255,255,.22); }
        .compose { padding:8px 16px; border:none; border-radius:999px; background:#fff;
                   color:var(--accent); font:inherit; font-weight:700; cursor:pointer; white-space:nowrap; }
        .icon { border:none; background:none; color:inherit; font-size:16px; cursor:pointer;
                padding:4px 8px; border-radius:7px; line-height:1; }
        .icon:hover { background:rgba(0,0,0,.06); }
        .top .icon:hover { background:rgba(255,255,255,.18); }
        .icon.danger:hover { background:#fff2f3; color:#b91c1c; }
        .menu { display:none; }

        .list { grid-area:list; border-right:1px solid var(--line); background:#fff;
                display:flex; flex-direction:column; min-height:0; }
        .list-head { display:flex; align-items:center; gap:4px; padding:9px 12px;
                     border-bottom:1px solid var(--line); color:var(--dim); font-size:12.5px; }
        .spacer { flex:1; }
        .rows { overflow-y:auto; flex:1; }

        .row { display:flex; gap:11px; padding:11px 13px; border-bottom:1px solid var(--line);
               cursor:pointer; }
        .row:hover { background:#f8fafd; }
        .row.on { background:#eaf1ff; box-shadow:inset 3px 0 0 var(--accent); }
        .avatar { flex:none; width:34px; height:34px; border-radius:50%; display:grid;
                  place-items:center; color:#fff; font-weight:700; font-size:13px; }
        .row-main { min-width:0; flex:1; }
        .row-top { display:flex; align-items:baseline; gap:8px; }
        .row-from { font-weight:600; font-size:13.5px; flex:1; overflow:hidden;
                    text-overflow:ellipsis; white-space:nowrap; }
        .row-when { color:var(--dim); font-size:11.5px; white-space:nowrap; }
        .row-subject, .row-peek { overflow:hidden; text-overflow:ellipsis; white-space:nowrap;
                                  font-size:13px; }
        .row-peek { color:var(--dim); font-size:12.5px; }
        .row.unread .row-from, .row.unread .row-subject { font-weight:800; }
        .row.unread .row-subject::after { content:"●"; color:var(--accent); font-size:9px;
                                          vertical-align:middle; margin-left:6px; }

        .pane { grid-area:pane; overflow-y:auto; min-height:0; }
        .blank { height:100%; display:flex; flex-direction:column; align-items:center;
                 justify-content:center; gap:6px; color:var(--dim); text-align:center; padding:24px; }
        .blank-mark { font-size:40px; }
        .muted { color:var(--dim); font-size:12.5px; margin:0; }

        .connect { margin-top:18px; font-size:13px; }
        .connect summary { cursor:pointer; }
        .settings { display:flex; gap:14px; flex-wrap:wrap; justify-content:center; margin:12px 0 6px; }
        .settings div { background:#fff; border:1px solid var(--line); border-radius:9px;
                        padding:9px 13px; text-align:left; }
        .settings strong { display:block; font-size:11px; text-transform:uppercase;
                           letter-spacing:.04em; color:var(--dim); }
        .settings code { font:13px ui-monospace,Consolas,monospace; }

        .reading { padding:22px 26px 60px; max-width:900px; }
        .reading h1 { font-size:20px; margin:0 0 14px; line-height:1.3; }
        .who { display:flex; align-items:center; gap:11px; padding-bottom:15px;
               border-bottom:1px solid var(--line); margin-bottom:18px; }
        .who .avatar { width:40px; height:40px; font-size:15px; }
        .who-main { flex:1; min-width:0; }
        .who-from { font-weight:700; }
        .who-to { color:var(--dim); font-size:12.5px; }
        .actions { display:flex; gap:6px; }
        .chip { display:inline-block; padding:3px 10px; border-radius:999px; background:#eef4ff;
                color:var(--accent); font-size:12px; font-weight:600; }
        .banner { display:flex; align-items:center; gap:10px; flex-wrap:wrap; padding:11px 14px;
                  border:1px solid #d8e3fb; background:#f4f8ff; border-radius:10px; margin-bottom:16px; }
        .banner a { color:var(--accent); font-size:12.5px; word-break:break-all; }
        iframe { width:100%; min-height:420px; border:1px solid var(--line); border-radius:10px;
                 background:#fff; }
        pre { white-space:pre-wrap; word-break:break-word; background:#fff; border:1px solid var(--line);
              border-radius:10px; padding:14px; font:12.5px/1.6 ui-monospace,Consolas,monospace;
              margin:0; }
        .tabs { display:flex; gap:4px; margin:18px 0 10px; }
        .tab { padding:5px 12px; border:1px solid var(--line); border-radius:999px; background:#fff;
               font:inherit; font-size:12.5px; cursor:pointer; color:var(--dim); }
        .tab.on { background:var(--accent); border-color:var(--accent); color:#fff; font-weight:600; }

        /* Both carry `display`, which outranks the browser's own [hidden] rule — without this
           the compose window is open the moment the page loads. */
        [hidden] { display:none !important; }

        .scrim { position:fixed; inset:0; background:rgba(15,23,42,.35); z-index:8; }
        .composer { position:fixed; right:24px; bottom:0; width:min(560px,calc(100vw - 32px));
                    background:#fff; border-radius:12px 12px 0 0; box-shadow:0 -6px 28px rgba(15,23,42,.28);
                    z-index:9; display:flex; flex-direction:column; max-height:min(86vh,720px); }
        .composer header { display:flex; align-items:center; gap:10px; padding:11px 15px;
                           background:#0f766e; color:#fff; border-radius:12px 12px 0 0; }
        .composer header strong { flex:1; font-size:14px; }
        .composer-body { padding:14px 16px; overflow-y:auto; }
        .composer label { display:block; margin-bottom:11px; }
        .composer label span { display:block; font-size:12px; font-weight:600; color:#334155;
                               margin-bottom:4px; }
        .composer input, .composer textarea { width:100%; padding:9px 11px; border:1px solid var(--line);
                                              border-radius:8px; font:inherit; }
        .composer textarea { font-family:ui-monospace,Consolas,monospace; font-size:13px; }
        .composer details summary { cursor:pointer; font-size:12.5px; color:var(--dim);
                                    margin-bottom:8px; }
        .composer footer { display:flex; align-items:center; gap:12px; padding:12px 16px;
                           border-top:1px solid var(--line); }
        .send { padding:9px 20px; border:none; border-radius:9px; background:#0f766e; color:#fff;
                font:inherit; font-weight:700; cursor:pointer; }
        .send:disabled { opacity:.55; cursor:default; }
        .composer-note { margin:4px 0 0; font-size:12.5px; }
        .composer-note.bad { color:#b91c1c; }
        .composer-note.ok { color:#15803d; }

        /* One pane at a time on a phone, which is what every mail app does. */
        @media (max-width: 820px) {
          .app { grid-template-columns:1fr; grid-template-areas:"top" "list"; }
          .app.reading-open { grid-template-areas:"top" "pane"; }
          .app.reading-open .list { display:none; }
          .app:not(.reading-open) .pane { display:none; }
          .app.reading-open .menu { display:block; }
          .compose { padding:8px 12px; }
        }
        """;

    private const string Script = """
        const $ = (id) => document.getElementById(id);
        const rows = $('rows'), pane = $('pane'), app = document.querySelector('.app');

        let messages = [];
        let openId = null;
        let bodyTab = 'html';
        let lastState = null;

        // Filled by /api/inbox/v1/settings. Null until it answers, and a null here means
        // "not known yet" rather than "not configured" — the difference decides whether Send
        // is disabled or merely untested.
        let settings = null;

        const senderReady = () => !settings || settings.sender.configured;

        // A stable colour per sender, so the same correspondent always looks the same. Hashing
        // the address beats a random palette that reshuffles on every refresh.
        const hues = [211, 160, 275, 20, 340, 130, 45];
        const colourOf = (text) => {
          let hash = 0;
          for (const character of text) hash = (hash * 31 + character.charCodeAt(0)) | 0;
          return `hsl(${hues[Math.abs(hash) % hues.length]} 62% 46%)`;
        };

        const escape = (value) => (value ?? '').replace(/[&<>"']/g, (c) =>
          ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

        // "Vispl HR" <no-reply@x> -> Vispl HR. A raw header is not a name.
        function displayName(address) {
          const quoted = /"([^"]+)"/.exec(address || '');
          if (quoted) return quoted[1];
          const bare = (address || '').replace(/[<>]/g, '').trim();
          return bare.split('@')[0] || bare || 'Unknown';
        }

        function initials(name) {
          const parts = name.split(/[\s._-]+/).filter(Boolean);
          return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase();
        }

        // Time for today, weekday for this week, date beyond. What every mail client does,
        // because "5 Sep 23:34:19" is unreadable at a glance and the year is never the point.
        function when(iso) {
          const at = new Date(iso), now = new Date();
          const sameDay = at.toDateString() === now.toDateString();
          if (sameDay) return at.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
          const days = (now - at) / 86400000;
          if (days < 1) return 'Yesterday';
          if (days < 7) return at.toLocaleDateString([], { weekday: 'short' });
          return at.toLocaleDateString([], { day: 'numeric', month: 'short' });
        }

        async function load(keepOpen = true) {
          const query = $('q').value.trim();
          const url = '/api/inbox/v1/messages' + (query ? '?q=' + encodeURIComponent(query) : '');
          messages = await (await fetch(url)).json();

          const unread = messages.filter((m) => !m.seen).length;
          $('count').textContent = messages.length
            ? `${messages.length} message${messages.length === 1 ? '' : 's'}` +
              (unread ? ` · ${unread} unread` : '')
            : (query ? 'Nothing matches' : 'Empty');

          document.title = unread ? `Mail (${unread}) — Vispl HR` : 'Mail — Vispl HR';

          rows.innerHTML = messages.map((message) => {
            const name = displayName(message.from);
            return `
              <div class="row ${message.seen ? '' : 'unread'} ${message.id === openId ? 'on' : ''}"
                   data-id="${message.id}">
                <span class="avatar" style="background:${colourOf(message.from)}">${escape(initials(name))}</span>
                <span class="row-main">
                  <span class="row-top">
                    <span class="row-from">${escape(name)}</span>
                    <span class="row-when">${escape(when(message.receivedUtc))}</span>
                  </span>
                  <div class="row-subject">${escape(message.subject)}</div>
                  <div class="row-peek">to ${escape(message.to.join(', '))}</div>
                </span>
              </div>`;
          }).join('');

          if (!keepOpen || !messages.some((m) => m.id === openId)) {
            openId = null;
            showBlank();
          }
        }

        // Kept as a template rather than as the markup the page happened to start with: once a
        // message has been opened that markup is gone, and rebuilding it by reloading the
        // window would throw away the search, the scroll position and the point of a
        // single-page client.
        const blankHtml = pane.innerHTML;

        function showBlank() {
          app.classList.remove('reading-open');
          pane.innerHTML = blankHtml;

          // The template is the blank markup as it was before anything filled it in, so the
          // connection panel has to be painted again every time it comes back.
          paintEndpoints();
        }

        async function open(id) {
          openId = id;
          app.classList.add('reading-open');
          [...rows.children].forEach((row) =>
            row.classList.toggle('on', row.dataset.id === id));

          const message = await (await fetch('/api/inbox/v1/messages/' + id)).json();
          await fetch('/api/inbox/v1/messages/' + id + '/read', { method: 'POST' });

          const name = displayName(message.from);
          const hasHtml = !!message.htmlBody;
          if (!hasHtml) bodyTab = 'text';

          pane.innerHTML = `
            <div class="reading">
              <h1>${escape(message.subject)}</h1>
              <div class="who">
                <span class="avatar" style="background:${colourOf(message.from)}">${escape(initials(name))}</span>
                <span class="who-main">
                  <div class="who-from">${escape(name)} <span class="who-to">&lt;${escape(message.from.replace(/.*<|>.*/g, ''))}&gt;</span></div>
                  <div class="who-to">to ${escape(message.to.join(', '))} · ${new Date(message.receivedUtc).toLocaleString()}</div>
                </span>
                <span class="actions">
                  <button class="icon" data-act="unread" title="Mark unread">✉︎</button>
                  <button class="icon danger" data-act="delete" title="Delete">🗑</button>
                </span>
              </div>

              ${message.links.length ? `
                <div class="banner">
                  <span class="chip">${message.links.length} link${message.links.length === 1 ? '' : 's'}</span>
                  ${message.links.map((l) => `<a href="${escape(l)}" target="_blank" rel="noreferrer">${escape(l)}</a>`).join('')}
                </div>` : ''}

              <div class="tabs">
                ${hasHtml ? `<button class="tab ${bodyTab === 'html' ? 'on' : ''}" data-tab="html">HTML</button>` : ''}
                <button class="tab ${bodyTab === 'text' ? 'on' : ''}" data-tab="text">Text</button>
                <button class="tab ${bodyTab === 'raw' ? 'on' : ''}" data-tab="raw">Source</button>
              </div>
              <div id="body"></div>
            </div>`;

          renderBody(message);

          pane.querySelectorAll('.tab').forEach((tab) => tab.onclick = () => {
            bodyTab = tab.dataset.tab;
            pane.querySelectorAll('.tab').forEach((t) => t.classList.toggle('on', t === tab));
            renderBody(message);
          });

          pane.querySelector('[data-act=unread]').onclick = async () => {
            await fetch('/api/inbox/v1/messages/' + id + '/unread', { method: 'POST' });
            openId = null;
            await load(false);
          };

          pane.querySelector('[data-act=delete]').onclick = async () => {
            await fetch('/api/inbox/v1/messages/' + id, { method: 'DELETE' });
            openId = null;
            await load(false);
          };

          load();
        }

        function renderBody(message) {
          const target = $('body');
          if (bodyTab === 'html' && message.htmlBody) {
            // Sandboxed: captured mail is untrusted content and this client must not run it.
            const frame = document.createElement('iframe');
            frame.setAttribute('sandbox', '');
            frame.srcdoc = message.htmlBody;
            target.innerHTML = '';
            target.appendChild(frame);
            return;
          }
          target.innerHTML =
            `<pre>${escape(bodyTab === 'raw' ? message.raw : (message.textBody || '(no text part)'))}</pre>`;
        }

        // ---- Wiring ----------------------------------------------------------

        rows.onclick = (event) => {
          const row = event.target.closest('.row');
          if (row) open(row.dataset.id);
        };

        $('back').onclick = () => { openId = null; app.classList.remove('reading-open'); load(false); };
        $('refresh').onclick = () => load();

        let typing;
        $('q').oninput = () => { clearTimeout(typing); typing = setTimeout(() => load(false), 250); };

        $('empty').onclick = async () => {
          if (!confirm('Delete every captured message?')) return;
          await fetch('/api/inbox/v1/messages', { method: 'DELETE' });
          openId = null;
          await load(false);
        };

        // ---- Compose ---------------------------------------------------------

        const composer = $('composer'), scrim = $('scrim');
        const openComposer = () => { composer.hidden = false; scrim.hidden = false; $('c-to').focus(); };
        const closeComposer = () => { composer.hidden = true; scrim.hidden = true; $('c-note').textContent = ''; };

        $('compose').onclick = openComposer;
        $('composer-close').onclick = closeComposer;
        scrim.onclick = closeComposer;

        $('c-send').onclick = async () => {
          const note = $('c-note');
          const body = {
            to: $('c-to').value.trim(),
            subject: $('c-subject').value.trim(),
            text: $('c-text').value,
            html: $('c-html').value.trim() || null
          };

          if (!body.to || !body.subject || !body.text.trim()) {
            note.className = 'composer-note bad';
            note.textContent = 'To, subject and a message are all needed.';
            return;
          }

          $('c-send').disabled = true;
          note.className = 'composer-note';
          note.textContent = 'Sending…';

          try {
            const response = await fetch('/api/inbox/v1/compose', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body)
            });

            if (!response.ok) {
              const problem = await response.json().catch(() => ({}));
              note.className = 'composer-note bad';
              note.textContent = problem.failure || problem.title || 'The sender refused it.';
              return;
            }

            ['c-to', 'c-subject', 'c-text', 'c-html'].forEach((id) => $(id).value = '');
            closeComposer();

            // Keeps whatever was open. Sending a message is no reason to close the one
            // somebody was reading.
            await load();
          } catch (error) {
            note.className = 'composer-note bad';
            note.textContent = settings?.sender
              ? `Could not reach the sender at ${settings.sender.authority}. Is it running?`
              : 'Could not reach the sender. Is it running?';
          } finally {
            $('c-send').disabled = !senderReady();
          }
        };

        // ---- Live ------------------------------------------------------------
        // Polls a tiny endpoint and refreshes the list only when something changed, so a
        // message somebody is reading is never pulled out from under them.

        setInterval(async () => {
          try {
            const state = await (await fetch('/api/inbox/v1/state')).json();
            const key = state.count + ':' + state.unseen;
            if (lastState !== null && key !== lastState) await load();
            lastState = key;
          } catch (error) { /* the server is restarting; try again next tick */ }
        }, 4000);

        // ---- Where this server can be reached --------------------------------
        // Asked for rather than assumed. Nothing about a port or a hostname is written into
        // this page; the server binds them and the server is the one that answers.

        async function loadSettings() {
          try {
            settings = await (await fetch('/api/inbox/v1/settings')).json();
          } catch (error) {
            return;
          }

          $('c-route').textContent = settings.sender.configured
            ? `Relayed by the sender at ${settings.sender.authority}, delivered straight back here.`
            : 'No sender is configured, so nothing can be sent from here.';

          $('c-send').disabled = !senderReady();

          paintEndpoints();
        }

        /// Fills the connection panel, when there is one on screen and there is something to
        /// put in it. Called from both ends because the panel lives in the blank pane, which
        /// comes and goes, and the settings arrive once.
        function paintEndpoints() {
          const list = $('endpoints');
          if (!list || !settings) return;

          list.innerHTML = settings.protocols.map((endpoint) => `
            <div title="${escape(endpoint.purpose)}">
              <strong>${escape(endpoint.name)}</strong><code>${escape(endpoint.address)}</code>
            </div>`).join('');

          $('endpoint-note').textContent =
            `${settings.encryption}. ${settings.credentials}. One folder, ${settings.folder}.`;
        }

        loadSettings();
        load(false);
        """;
}
