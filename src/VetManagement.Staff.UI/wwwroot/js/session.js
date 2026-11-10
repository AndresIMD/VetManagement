// The staff app keeps one session per browser, in localStorage, so every tab uses it (see AccountApiService). A session
// kept only until the browser closes also gets this cookie: having no expiry date, the cookie goes when the browser
// closes, while localStorage stays. Clinics serve the app over plain http on their network, so no Secure flag.
window.staffSession = {
    markBrowserOpen() {
        document.cookie = 'staffBrowserOpen=1; path=/; SameSite=Strict';
    },
    // True only when the browser keeps cookies and the mark is gone: one that refuses cookies keeps the session.
    browserWasClosed() {
        return navigator.cookieEnabled && !document.cookie.split('; ').includes('staffBrowserOpen=1');
    },
    clearBrowserMark() {
        document.cookie = 'staffBrowserOpen=; path=/; max-age=0; SameSite=Strict';
    },
    // Another tab signed in or out, or refreshed the session. A tab coming back into view checks too: a sleeping tab
    // can hear about a change late.
    watch(dotnet) {
        const check = () => dotnet.invokeMethodAsync('OnSessionChangedElsewhereAsync');
        window.addEventListener('storage', e => {
            if (e.key === 'authToken' || e.key === null)
                check();
        });
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible')
                check();
        });
    }
};
