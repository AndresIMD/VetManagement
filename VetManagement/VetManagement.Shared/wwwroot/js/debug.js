window.debug = {
    log: function (message) {
        // console.log suppressed in production builds; safe in dev
        if (window.location.hostname === 'localhost') console.log(message);
    }
};
