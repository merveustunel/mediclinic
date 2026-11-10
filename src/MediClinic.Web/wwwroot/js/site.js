// Native details keeps navigation usable with JavaScript disabled.
(() => {
    const navigation = document.querySelector('.navigation');
    if (!navigation) return;
    const mobile = matchMedia('(max-width: 760px)');
    const sync = () => { navigation.open = !mobile.matches; };
    sync();
    mobile.addEventListener('change', sync);
    navigation.addEventListener('keydown', event => {
        if (event.key === 'Escape' && mobile.matches && navigation.open) {
            navigation.open = false;
            navigation.querySelector('summary').focus();
        }
    });
})();
