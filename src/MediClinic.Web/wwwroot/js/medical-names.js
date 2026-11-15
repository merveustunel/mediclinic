// Refresh only explicitly marked medical data after the existing secure culture POST.
// Never replace forms, personal names, free text, or selected identifiers.
(() => {
    let revision = 0;
    document.addEventListener('mediclinic:language', async () => {
        const current = ++revision;
        const booking = document.querySelector('[data-booking-selection]');
        if (!booking && !document.querySelector('[data-medical-name], [data-medical-option]')) return;
        try {
            const url = new URL(location.href);
            const bookingValues = booking ? Array.from(new FormData(booking)) : [];
            if (booking) {
                for (const [key, value] of bookingValues) url.searchParams.set(key, value);
                url.searchParams.set('Stage', '1');
            }
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Medical labels unavailable');
            const fresh = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (current !== revision) return;
            if (booking && JSON.stringify(Array.from(new FormData(booking))) !== JSON.stringify(bookingValues)) return;
            const names = new Map(Array.from(fresh.querySelectorAll('[data-medical-name]'), el => [el.dataset.medicalName, el.textContent]));
            document.querySelectorAll('[data-medical-name]').forEach(el => {
                if (names.has(el.dataset.medicalName)) el.textContent = names.get(el.dataset.medicalName);
            });
            fresh.querySelectorAll('select').forEach(select => {
                const target = document.getElementById(select.id);
                if (!target || target.tagName !== 'SELECT') return;
                if (booking?.contains(target)) {
                    target.replaceChildren(...Array.from(select.options, option => option.cloneNode(true)));
                    target.disabled = select.disabled;
                    return;
                }
                const options = new Map(Array.from(select.options, option => [option.value, option]));
                Array.from(target.options).filter(option => option.hasAttribute('data-medical-option')).forEach(option => {
                    const replacement = options.get(option.value);
                    if (!replacement) return;
                    option.textContent = replacement.textContent;
                    if (replacement.dataset.serviceName) option.dataset.serviceName = replacement.dataset.serviceName;
                });
            });
            document.dispatchEvent(new CustomEvent('mediclinic:medical'));
        } catch {
            if (current === revision) document.dispatchEvent(new CustomEvent('mediclinic:medical-error'));
        }
    });
})();
