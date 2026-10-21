// Guided tours (lib/driverjs, MIT): each step highlights the element marked data-tour="anchor" and explains it.
// Steps whose element isn't on screen (e.g. a field shown only for registered pets) are skipped. See TourService.
window.appTour = {
    start(steps, labels) {
        const find = anchor => document.querySelector(`[data-tour="${anchor}"]`);
        const shown = steps.filter(s => !s.anchor || find(s.anchor));
        if (shown.length === 0) return;
        const text = value => { const span = document.createElement('span'); span.textContent = value; return span.innerHTML; };
        window.driver.js.driver({
            showProgress: shown.length > 1,
            progressText: labels.progress,
            nextBtnText: labels.next,
            prevBtnText: labels.previous,
            doneBtnText: labels.done,
            popoverClass: 'vm-tour',
            smoothScroll: true,
            steps: shown.map(s => ({
                element: s.anchor ? find(s.anchor) : undefined,
                popover: { title: text(s.title), description: text(s.text) }
            }))
        }).drive();
    }
};
