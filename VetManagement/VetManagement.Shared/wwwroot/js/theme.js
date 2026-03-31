window.appTheme = {
    applyVariables(cssVars) {
        const root = document.documentElement;
        for (const [key, value] of Object.entries(cssVars)) {
            root.style.setProperty(key, value);
        }
    }
};
