// Small browser helpers called from Blazor through IJSRuntime.
window.pm = (() => {
    const store = {
        get(key) { try { return localStorage.getItem(key); } catch { return null; } },
        set(key, value) { try { localStorage.setItem(key, value); } catch { /* private mode */ } },
    };

    function setTheme(theme) {
        document.documentElement.dataset.theme = theme;
        store.set('pm.theme', theme);
    }

    async function copy(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Fallback for http:// (clipboard API needs a secure context).
            const ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            const ok = document.execCommand('copy');
            ta.remove();
            return ok;
        }
    }

    // "/" focuses the header search unless the user is typing somewhere.
    document.addEventListener('keydown', e => {
        if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
        const t = e.target;
        if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
        const input = document.querySelector('[data-search-input]');
        if (input && input.offsetParent !== null) { e.preventDefault(); input.focus(); input.select(); }
    });

    function isOverflowing(el) { return !!el && el.scrollHeight > el.clientHeight + 4; }

    // ---------- Uploads ----------
    // File objects stay in JS so multi-hundred-MB files never pass through .NET memory.
    const files = new Map();
    const xhrs = new Map();
    let nextId = 1;

    function registerDropZone(zone, input, dotnet) {
        const add = list => {
            const added = [];
            for (const f of list) {
                const id = nextId++;
                files.set(id, f);
                added.push({ id, name: f.name, size: f.size });
            }
            if (added.length) dotnet.invokeMethodAsync('OnFilesAdded', added);
        };
        input.addEventListener('change', () => { add(input.files); input.value = ''; });
        ['dragenter', 'dragover'].forEach(ev => zone.addEventListener(ev, e => { e.preventDefault(); zone.classList.add('drag'); }));
        ['dragleave', 'drop'].forEach(ev => zone.addEventListener(ev, e => { e.preventDefault(); zone.classList.remove('drag'); }));
        zone.addEventListener('drop', e => add(e.dataTransfer.files));
    }

    function upload(id, url, releaseNotes, dotnet) {
        const file = files.get(id);
        if (!file) return Promise.resolve({ status: 0, body: 'Không còn file trong trình duyệt — chọn lại file.' });
        return new Promise(resolve => {
            const xhr = new XMLHttpRequest();
            xhrs.set(id, xhr);
            let last = 0;
            xhr.upload.onprogress = e => {
                const now = performance.now();
                if (now - last < 250 && e.loaded < e.total) return;
                last = now;
                dotnet.invokeMethodAsync('OnProgress', id, e.loaded, e.total);
            };
            const done = status => { xhrs.delete(id); resolve({ status, body: xhr.responseText || '' }); };
            xhr.onload = () => done(xhr.status);
            xhr.onerror = () => done(0);
            xhr.onabort = () => done(-1);
            xhr.open('PUT', url);
            xhr.setRequestHeader('Content-Type', 'application/octet-stream');
            xhr.setRequestHeader('Accept-Language', 'vi'); // server error messages in Vietnamese, as in Program.cs
            if (releaseNotes) xhr.setRequestHeader('X-Release-Notes', encodeURIComponent(releaseNotes));
            xhr.send(file);
        });
    }

    function removeFile(id) {
        const xhr = xhrs.get(id);
        if (xhr) xhr.abort();
        files.delete(id);
    }

    function onBeforeUnload(e) { e.preventDefault(); e.returnValue = ''; }
    function guardUnload(on) {
        if (on) window.addEventListener('beforeunload', onBeforeUnload);
        else window.removeEventListener('beforeunload', onBeforeUnload);
    }

    return { store, setTheme, copy, isOverflowing, registerDropZone, upload, removeFile, guardUnload };
})();
