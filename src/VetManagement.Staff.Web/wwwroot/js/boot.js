// Starts Blazor in the clinic's culture (config.json "Culture"), so the runtime loads the part of the globalization
// data that covers it, whatever language the browser is in. Without a culture it starts as the browser is.
fetch('config.json', { cache: 'no-store' })
    .then(response => response.ok ? response.json() : {})
    .catch(() => ({}))
    .then(config => Blazor.start(config.Culture ? { applicationCulture: config.Culture } : {}));
