// "Back" on pages reached from several screens: the previous screen, or the fallback page when this one was opened
// directly (a new tab or a typed address) and has nothing to go back to.
window.appNavigation = {
    back(fallback) {
        if (window.history.length > 1) {
            window.history.back();
        } else {
            window.location.assign(fallback);
        }
    }
};
