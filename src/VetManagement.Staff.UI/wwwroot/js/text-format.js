// Bold for the report texts that use **markup** (see ReportText): toggles ** around the selected text of a field and
// tells Blazor the value changed. With nothing selected it takes the whole line; a selection over several lines marks
// each line. Ctrl+B (Cmd+B) does the same in any field inside an element with data-bold-markup.
window.appText = {
    bold(containerId) {
        const field = document.querySelector(`#${containerId} textarea, #${containerId} input`);
        if (field) appText.toggle(field, '**');
    },

    toggle(field, marker) {
        let text = field.value, start = field.selectionStart, end = field.selectionEnd;
        if (start === end) {
            start = text.lastIndexOf('\n', start - 1) + 1;
            const lineEnd = text.indexOf('\n', end);
            end = lineEnd < 0 ? text.length : lineEnd;
        }
        const m = marker.length;
        // Already bold from outside the selection, e.g. the word between ** ** was selected: unwrap it.
        if (!text.slice(start, end).includes('\n') && text.slice(start - m, start) === marker && text.slice(end, end + m) === marker) {
            field.value = text.slice(0, start - m) + text.slice(start, end) + text.slice(end + m);
            appText.changed(field, start - m, end - m);
            return;
        }
        const lines = text.slice(start, end).split('\n');
        const isBold = l => l.trim().length > 2 * m && l.trim().startsWith(marker) && l.trim().endsWith(marker);
        const unwrap = lines.filter(l => l.trim()).every(isBold);
        const changed = lines.map(l => {
            const core = l.trim();
            if (!core) return l;
            const lead = l.slice(0, l.indexOf(core)), trail = l.slice(l.indexOf(core) + core.length);
            if (unwrap) return lead + core.slice(m, -m) + trail;
            return isBold(l) ? l : lead + marker + core + marker + trail;
        }).join('\n');
        field.value = text.slice(0, start) + changed + text.slice(end);
        appText.changed(field, start, start + changed.length);
    },

    changed(field, start, end) {
        field.focus();
        field.setSelectionRange(start, end);
        field.dispatchEvent(new Event('input', { bubbles: true }));
        field.dispatchEvent(new Event('change', { bubbles: true }));
    }
};

document.addEventListener('keydown', e => {
    if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 'b' && e.target.closest?.('[data-bold-markup]')
        && (e.target.tagName === 'TEXTAREA' || e.target.tagName === 'INPUT')) {
        e.preventDefault();
        appText.toggle(e.target, '**');
    }
});
