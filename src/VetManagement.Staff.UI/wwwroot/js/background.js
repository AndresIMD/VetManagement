window.appBg = {
  setMode(mode) {
    const body = document.body;
    body.classList.remove('bg-none', 'bg-soft', 'bg-grid', 'bg-image');
    body.style.removeProperty('--app-bg-image');

    if (mode && mode !== 'none') {
      body.classList.add(`bg-${mode}`);
    }
  },

  setImage(url) {
    const body = document.body;
    body.classList.remove('bg-none', 'bg-soft', 'bg-grid');
    body.classList.add('bg-image');
    body.style.setProperty('--app-bg-image', `url('${url}')`);
  }
};