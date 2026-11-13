// Runs before the stylesheet: never paint the light palette before a saved dark preference.
(() => {
    let theme = 'light';
    try { if (localStorage.getItem('mediclinic-theme') === 'dark') theme = 'dark'; } catch { /* Storage can be unavailable in private browsing. */ }
    document.documentElement.dataset.theme = theme;
})();
