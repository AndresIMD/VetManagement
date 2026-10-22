// Measures a rendered exam report (ExamReportSheet) so its lines are spread over pages by their real height instead of an
// estimate. Everything is returned in mm: the room for lines in the results box of the first page (and of a later page, when
// there is one), the patient box, the patient box of later pages (bar and one line), the table header, the closing blocks and each line
// (marked data-row="index"). Returns null while the sheet isn't laid out (e.g. hidden).
window.appReportSheet = {
    measure(id, pageHeightMm) {
        const doc = document.getElementById(id);
        const pages = doc ? [...doc.querySelectorAll('.xr-page')] : [];
        const pagePx = pages.length ? pages[0].getBoundingClientRect().height : 0;
        if (!pagePx) return null;
        const mm = pageHeightMm / pagePx;
        const height = el => el ? el.getBoundingClientRect().height * mm : 0;
        const withMargins = el => {
            const style = getComputedStyle(el);
            return height(el) + (parseFloat(style.marginTop) + parseFloat(style.marginBottom)) * mm;
        };
        // Room inside the results area, below the bar: its height without its padding.
        const room = page => {
            const results = page.querySelector('.xr-results');
            const style = getComputedStyle(results);
            return (results.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom)) * mm;
        };
        // The closing blocks as tall as their content (on the page they stretch to the bottom of the box).
        const closingHeight = () => {
            const closing = pages[pages.length - 1].querySelector('.xr-closing');
            if (!closing || !closing.children.length) return 0;
            const holder = closing.cloneNode(true);
            holder.style.cssText = `position:absolute;visibility:hidden;left:0;top:0;flex:none;display:block;width:${closing.clientWidth}px`;
            closing.parentElement.appendChild(holder);
            const value = height(holder);
            holder.remove();
            return value;
        };
        const rows = [];
        doc.querySelectorAll('[data-row]').forEach(el => { rows[+el.dataset.row] = withMargins(el); });
        return {
            firstRoom: room(pages[0]),
            nextRoom: pages.length > 1 ? room(pages[1]) : null,
            patientBox: height(pages[0].querySelector('.xr-patient-box')),
            continued: pages.length > 1 ? height(pages[1].querySelector('.xr-continued-box')) : null,
            header: Math.max(0, ...[...doc.querySelectorAll('.xr-table thead')].map(height)),
            closing: closingHeight(),
            rows: [...rows].map(r => r ?? 0)
        };
    }
};
