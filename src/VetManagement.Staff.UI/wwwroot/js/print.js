// Prints one element on its own (no app bar or menus) through a hidden frame. The document title becomes the
// suggested file name when the user picks "Save as PDF".
window.appPrint = {
    element(id, title) {
        const source = document.getElementById(id);
        if (!source) return;
        const frame = document.createElement('iframe');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(frame);
        const doc = frame.contentDocument;
        doc.open();
        doc.write('<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0"></body></html>');
        doc.close();
        doc.title = title;
        doc.body.innerHTML = source.outerHTML;
        const pending = [...doc.images].map(img => img.complete ? Promise.resolve() : new Promise(done => { img.onload = img.onerror = done; }));
        Promise.all(pending).then(() => {
            frame.contentWindow.focus();
            frame.contentWindow.print();
            setTimeout(() => frame.remove(), 1000);
        });
    }
};
