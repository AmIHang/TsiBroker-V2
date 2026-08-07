namespace TsiBroker.Im.Mock.Ui;

/// <summary>
/// Single self-contained HTML page for manually inspecting/driving the mock during local testing.
/// Deliberately not a separate frontend project - this is a dev tool, not a shipped UI. Styling
/// mirrors TsiBroker.Ui's design tokens/components (src/assets/styles/tokens.less, card.less,
/// buttons.less, forms.less, list.less) so it reads as part of the same product, not a
/// throwaway debug page - just inlined instead of pulled from the Vue app's build.
/// </summary>
public static class IndexPage
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8" />
        <title>TsiBroker.Im.Mock</title>
        <style>
          :root {
            --color-primary: #6f4295;
            --color-primary-hover: color-mix(in srgb, var(--color-primary) 85%, black);
            --color-primary-soft: color-mix(in srgb, var(--color-primary) 10%, white);
            --color-on-primary: #ffffff;

            --color-background: #ffffff;
            --color-content-bg: #eaeaea;
            --color-border: rgba(60, 60, 60, 0.12);
            --color-border-hover: rgba(60, 60, 60, 0.29);
            --color-heading: #2c3e50;
            --color-text: #2c3e50;

            --color-danger: #d33;
            --color-success: #2e9e5b;
            --color-warning: #b8860b;

            --color-topbar-bg:
              radial-gradient(circle at 15% 20%, color-mix(in srgb, var(--color-primary) 6%, transparent), transparent 40%),
              radial-gradient(circle at 85% 80%, color-mix(in srgb, var(--color-primary) 6%, transparent), transparent 40%),
              linear-gradient(135deg, #ece9f1 0%, #e7e4ee 100%);
            --color-topbar-border: rgba(60, 60, 60, 0.12);
          }

          * { box-sizing: border-box; }

          body {
            margin: 0;
            min-height: 100vh;
            background: var(--color-content-bg);
            color: var(--color-text);
            font-family: Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            font-size: 15px;
            line-height: 1.6;
          }

          .topbar {
            display: flex;
            align-items: baseline;
            gap: 0.75rem;
            min-height: 64px;
            padding: 0.5rem 1.75rem;
            background: var(--color-topbar-bg);
            border-bottom: 1px solid var(--color-topbar-border);
          }

          .topbar h1 { font-size: 1.1rem; font-weight: 600; color: var(--color-heading); margin: 0; }
          .topbar p { margin: 0; font-size: 0.85rem; opacity: 0.7; }

          main {
            max-width: 960px;
            margin: 0 auto;
            padding: 1.75rem;
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
          }

          .card {
            display: flex;
            flex-direction: column;
            gap: 1rem;
            padding: 1.5rem;
            border: 1px solid var(--color-border);
            border-radius: 14px;
            background: var(--color-background);
          }

          .card__title { font-size: 1rem; font-weight: 600; color: var(--color-heading); margin: 0; }

          .row { display: flex; gap: 1rem; }
          .row > .field { flex: 1; }

          .field { display: flex; flex-direction: column; gap: 0.35rem; }
          .field__label { font-size: 0.8rem; color: var(--color-text); opacity: 0.75; }

          input, select, textarea {
            padding: 0.6rem 0.75rem;
            border: 1px solid var(--color-border);
            border-radius: 8px;
            background: var(--color-background);
            color: var(--color-text);
            font-size: 0.9rem;
            font-family: inherit;
          }

          input:focus, select:focus, textarea:focus {
            outline: none;
            border-color: var(--color-primary);
          }

          textarea { min-height: 10rem; font-family: ui-monospace, monospace; font-size: 0.8rem; resize: vertical; }

          .toolbar { display: flex; gap: 0.6rem; align-items: center; }

          .btn {
            padding: 0.6rem 1.1rem;
            border: 1px solid var(--color-border);
            border-radius: 8px;
            background: var(--color-background);
            color: var(--color-text);
            font-size: 0.9rem;
            cursor: pointer;
            transition: background-color 0.15s, border-color 0.15s;
          }

          .btn:hover { border-color: var(--color-border-hover); }

          .btn--primary {
            border-color: var(--color-primary);
            background: var(--color-primary);
            color: var(--color-on-primary);
          }

          .btn--primary:hover { background: var(--color-primary-hover); }

          .btn--small { padding: 0.35rem 0.7rem; font-size: 0.82rem; }

          .status-text { font-size: 0.85rem; opacity: 0.75; }

          .data-table { width: 100%; border-collapse: collapse; }

          .data-table th, .data-table td {
            padding: 0.6rem 0.7rem;
            text-align: left;
            border-bottom: 1px solid var(--color-border);
            font-size: 0.85rem;
            vertical-align: top;
          }

          .data-table th { font-weight: 600; opacity: 0.75; }

          .badge {
            display: inline-block;
            padding: 0.2rem 0.6rem;
            border-radius: 999px;
            font-size: 0.78rem;
            font-weight: 600;
            white-space: nowrap;
          }

          .badge--primary { background: var(--color-primary-soft); color: var(--color-primary); }
          .badge--neutral { background: color-mix(in srgb, #999 15%, transparent); color: #777; }
          .badge--success { background: color-mix(in srgb, var(--color-success) 15%, transparent); color: var(--color-success); }
          .badge--danger { background: color-mix(in srgb, var(--color-danger) 15%, transparent); color: var(--color-danger); }

          .empty-state { text-align: center; opacity: 0.6; padding: 1rem 0; }

          details > summary { cursor: pointer; font-family: ui-monospace, monospace; font-size: 0.78rem; }

          pre {
            white-space: pre-wrap;
            word-break: break-all;
            max-height: 12rem;
            overflow: auto;
            background: var(--color-content-bg);
            color: var(--color-text);
            padding: 0.6rem 0.75rem;
            border-radius: 8px;
            font-size: 0.78rem;
            margin-top: 0.5rem;
          }
        </style>
        </head>
        <body>
        <header class="topbar">
          <h1>TsiBroker.Im.Mock</h1>
          <p>Test double for the ISB side of the Common Interface contract</p>
        </header>

        <main>
          <section class="card">
            <h2 class="card__title">Response behaviour (Broker &rarr; Mock, /ci)</h2>
            <div class="row">
              <label class="field">
                <span class="field__label">Mode</span>
                <select id="mode">
                  <option value="Ack">Ack</option>
                  <option value="Nack">Nack</option>
                  <option value="HttpError">HttpError</option>
                </select>
              </label>
              <label class="field">
                <span class="field__label">Delay (ms)</span>
                <input id="delay" type="number" min="0" value="0" />
              </label>
            </div>
            <div class="toolbar">
              <button class="btn btn--primary" id="saveConfig">Save</button>
              <span class="status-text" id="configStatus"></span>
            </div>
          </section>

          <section class="card">
            <h2 class="card__title">Send a message (Mock &rarr; Broker, /ci)</h2>
            <div class="row">
              <label class="field">
                <span class="field__label">Sender (RICS)</span>
                <input id="sender" />
              </label>
              <label class="field">
                <span class="field__label">Recipient (RICS)</span>
                <input id="recipient" />
              </label>
            </div>
            <label class="field">
              <span class="field__label">Target URL</span>
              <input id="targetUrl" />
            </label>
            <label class="field">
              <span class="field__label">Payload</span>
              <textarea id="payload" placeholder="Message XML"></textarea>
            </label>
            <div class="toolbar">
              <select id="messageType"></select>
              <button class="btn" id="loadTemplate">Load template</button>
              <button class="btn btn--primary" id="send">Send</button>
            </div>
            <div id="sendResult"></div>
          </section>

          <section class="card">
            <div class="toolbar" style="justify-content: space-between;">
              <h2 class="card__title">Messages</h2>
              <button class="btn btn--small" id="refresh">Refresh</button>
            </div>
            <table class="data-table">
              <thead>
                <tr><th>Time (UTC)</th><th>Direction</th><th>Id</th><th>Result</th><th>Content</th></tr>
              </thead>
              <tbody id="messages"></tbody>
            </table>
            <p class="empty-state" id="messagesEmpty" style="display:none;">No messages yet.</p>
          </section>
        </main>

        <script>
          async function loadConfig() {
            const res = await fetch('/api/response-config');
            const config = await res.json();
            document.getElementById('mode').value = config.mode;
            document.getElementById('delay').value = config.delayMs;
          }

          document.getElementById('saveConfig').addEventListener('click', async () => {
            const mode = document.getElementById('mode').value;
            const delayMs = Number(document.getElementById('delay').value) || 0;
            const res = await fetch('/api/response-config', {
              method: 'PUT',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ mode, delayMs })
            });
            document.getElementById('configStatus').textContent = res.ok ? 'saved' : 'error';
          });

          async function loadSendDefaults() {
            const res = await fetch('/api/send/defaults');
            const defaults = await res.json();
            document.getElementById('targetUrl').value = defaults.targetUrl;
            document.getElementById('sender').value = defaults.senderRics;
          }

          // Fills MessageHeader/Sender and /Recipient in the payload from the Sender/Recipient
          // fields, so those fields are the source of truth for the XML rather than something
          // you'd otherwise hand-edit inside the textarea.
          function applyRicsToPayload(xml) {
            const sender = document.getElementById('sender').value.trim();
            const recipient = document.getElementById('recipient').value.trim();
            if (sender) {
              xml = xml.replace(/(<Sender>)[^<]*(<\/Sender>)/, (_, open, close) => open + escapeHtml(sender) + close);
            }
            if (recipient) {
              xml = xml.replace(/(<Recipient>)[^<]*(<\/Recipient>)/, (_, open, close) => open + escapeHtml(recipient) + close);
            }
            return xml;
          }

          async function loadMessageTypes() {
            const res = await fetch('/api/send/templates');
            const messageTypes = await res.json();
            document.getElementById('messageType').innerHTML =
              messageTypes.map(t => `<option value="${t}">${t}</option>`).join('');
          }

          document.getElementById('loadTemplate').addEventListener('click', async () => {
            const messageType = document.getElementById('messageType').value;
            const res = await fetch('/api/send/templates/' + encodeURIComponent(messageType));
            document.getElementById('payload').value = applyRicsToPayload(await res.text());
          });

          function badgeClassForResult(result) {
            return result === 'ACK' ? 'badge--success' : 'badge--danger';
          }

          document.getElementById('send').addEventListener('click', async () => {
            const payload = applyRicsToPayload(document.getElementById('payload').value);
            document.getElementById('payload').value = payload;
            const targetUrl = document.getElementById('targetUrl').value || null;
            const res = await fetch('/api/send', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ payload, targetUrl })
            });
            const result = await res.json();
            document.getElementById('sendResult').innerHTML =
              '<span class="badge ' + badgeClassForResult(result.status) + '">' + result.status + '</span>' +
              (result.error ? ' <span class="status-text">' + escapeHtml(result.error) + '</span>' : '') +
              (result.responseXml ? '<pre>' + escapeHtml(result.responseXml) + '</pre>' : '');
            loadMessages();
          });

          document.getElementById('refresh').addEventListener('click', loadMessages);

          function escapeHtml(text) {
            return text.replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]);
          }

          async function loadMessages() {
            const res = await fetch('/api/messages');
            const messages = await res.json();
            const tbody = document.getElementById('messages');
            document.getElementById('messagesEmpty').style.display = messages.length === 0 ? 'block' : 'none';
            tbody.innerHTML = messages.map(m => `
              <tr>
                <td>${m.timestamp}</td>
                <td><span class="badge ${m.direction === 'Sent' ? 'badge--primary' : 'badge--neutral'}">${m.direction}</span></td>
                <td>${m.messageIdentifier ?? ''}</td>
                <td>${m.result ? '<span class="badge ' + badgeClassForResult(m.result) + '">' + m.result + '</span>' : ''}</td>
                <td><details><summary>${m.fileName}</summary><pre>${escapeHtml(m.content)}</pre></details></td>
              </tr>
            `).join('');
          }

          loadConfig();
          loadSendDefaults();
          loadMessageTypes();
          loadMessages();
          setInterval(loadMessages, 5000);
        </script>
        </body>
        </html>
        """;
}
