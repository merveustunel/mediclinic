// Native details keeps navigation usable with JavaScript disabled.
document.addEventListener('invalid', event => {
    const input = event.target;
    if (!input.validity || input.validity.customError) return;
    const text = key => globalThis.mediClinicText?.(key) || key;
    let message = text('Bu alana geçerli bir değer giriniz.');
    if (input.validity.valueMissing) message = input.getAttribute('data-val-required') || text('Bu alan zorunludur.');
    else if (input.validity.typeMismatch && input.type === 'email') message = text('Geçerli bir e-posta adresi giriniz.');
    input.setCustomValidity(message);
}, true);
document.addEventListener('input', event => event.target.setCustomValidity?.(''));
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

// Reveal only on explicit request; never read or copy the password value.
document.querySelectorAll('[data-password-toggle]').forEach(button => {
    const input = document.getElementById(button.dataset.passwordToggle);
    if (!input || input.type !== 'password') return;
    button.hidden = false;
    const mask = () => {
        input.type = 'password';
        button.setAttribute('aria-pressed', 'false');
        button.textContent = (globalThis.mediClinicText?.('Parolayı göster') || 'Parolayı göster');
    };
    button.addEventListener('click', () => {
        const reveal = input.type === 'password';
        input.type = reveal ? 'text' : 'password';
        button.setAttribute('aria-pressed', String(reveal));
        button.textContent = reveal ? (globalThis.mediClinicText?.('Parolayı gizle') || 'Parolayı gizle') : (globalThis.mediClinicText?.('Parolayı göster') || 'Parolayı göster');
    });
    input.form?.addEventListener('submit', mask);
    window.addEventListener('pagehide', mask);
});
