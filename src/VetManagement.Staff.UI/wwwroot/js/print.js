// Prints one element on its own (no app bar or menus) through a hidden frame. The document title becomes the
// suggested file name when the user picks "Save as PDF". pageSize (e.g. "215.9mm 279.4mm") fixes the paper in the
// frame's head, so the dialog can't fall back to another default paper (e.g. legal/"oficio") and split each sheet.
window.appPrint = {
    element(id, title, pageSize) {
        const source = document.getElementById(id);
        if (!source) return;
        const frame = document.createElement('iframe');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(frame);
        const doc = frame.contentDocument;
        const page = pageSize ? `@page{size:${pageSize};margin:0}` : '';
        doc.open();
        doc.write(`<!doctype html><html><head><meta charset="utf-8"><style>${page}html,body{margin:0;padding:0}</style></head><body></body></html>`);
        doc.close();
        doc.title = title;
        doc.body.innerHTML = source.outerHTML;
        const pending = [...doc.images].map(img => img.complete ? Promise.resolve() : new Promise(done => { img.onload = img.onerror = done; }));
        Promise.all(pending).then(() => {
            // Chrome names the PDF after the top page, not the frame: borrow its title while the dialog is open.
            const pageTitle = document.title;
            document.title = title;
            const restore = () => { document.title = pageTitle; frame.remove(); };
            frame.contentWindow.addEventListener('afterprint', () => setTimeout(restore, 0), { once: true });
            frame.contentWindow.focus();
            frame.contentWindow.print();
            setTimeout(() => { if (frame.isConnected) restore(); }, 60000);
        });
    }
};
