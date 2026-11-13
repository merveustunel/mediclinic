// Progressive enhancement of the existing, read-only GET workflow.
(() => {
    const form = document.querySelector('[data-booking-selection]');
    if (!form || !window.fetch || !window.AbortController || !window.DOMParser) return;
    const selects = Array.from(form.querySelectorAll('select'));
    const manual = form.querySelector('[data-booking-update]');
    const next = form.querySelector('[data-booking-continue]');
    const status = form.querySelector('[data-booking-status]');
    const help = document.getElementById('selection-help');
    const originalHelp = help.textContent;
    const empty = '00000000-0000-0000-0000-000000000000';
    let active = true, loading = false, submitting = false, revision = 0, request;
    const ready = () => selects.every(select => !select.disabled && select.value !== empty && select.value !== '');
    const updateState = () => {
        form.setAttribute('aria-busy', String(loading));
        next.disabled = loading || submitting || !ready();
    };

    form.addEventListener('change', async event => {
        const index = selects.indexOf(event.target);
        if (!active || submitting || index < 0) return;
        const current = ++revision;
        request?.abort();
        // Retain ancestors and the changed control. Never choose a descendant for the patient.
        selects.slice(index + 1).forEach(select => {
            select.replaceChildren(new Option((globalThis.mediClinicText?.('Önce üst seçimi tamamlayın') || 'Önce üst seçimi tamamlayın'), empty));
            select.disabled = true;
        });
        if (index === selects.length - 1) {
            loading = false;
            status.textContent = ready()
                ? (globalThis.mediClinicText?.('Hizmet seçiminiz kaydedildi. Tarih ve saat seçimine devam edebilirsiniz.') || 'Hizmet seçiminiz kaydedildi. Tarih ve saat seçimine devam edebilirsiniz.')
                : (globalThis.mediClinicText?.('Lütfen bir hizmet seçin.') || 'Lütfen bir hizmet seçin.');
            updateState();
            return;
        }
        loading = true;
        status.textContent = (globalThis.mediClinicText?.('Seçenekler yükleniyor…') || 'Seçenekler yükleniyor…');
        updateState();
        request = new AbortController();
        const url = new URL(form.action, window.location.href);
        url.search = new URLSearchParams(new FormData(form)).toString();
        url.searchParams.set('Stage', '1');
        try {
            const response = await fetch(url, { signal: request.signal, credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Selection unavailable');
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (current !== revision) return;
            const returned = html.querySelector('[data-booking-selection]');
            const refreshed = selects.map(select => returned?.querySelector('select[name="' + select.name + '"]'));
            if (refreshed.some(select => !select) || html.querySelector('.validation-summary-errors'))
                throw new Error('Selection changed');
            // Keep the same select nodes so keyboard focus stays on the changed control.
            selects.forEach((select, position) => {
                // Clone first: moving selected options can make the donor select
                // implicitly select its next option while nodes are being removed.
                select.replaceChildren(...Array.from(refreshed[position].options, option => option.cloneNode(true)));
                select.disabled = refreshed[position].disabled;
                const placeholder = select.options[0];
                if (placeholder?.value === empty && globalThis.mediClinicText)
                    placeholder.textContent = globalThis.mediClinicText(select.disabled ? 'Önce üst seçimi tamamlayın' : 'Seçin');
            });
            globalThis.mediClinicRefresh?.();
            status.textContent = (globalThis.mediClinicText?.('Seçenekler güncellendi. Sıradaki seçiminizi yapabilirsiniz.') || 'Seçenekler güncellendi. Sıradaki seçiminizi yapabilirsiniz.');
        } catch (error) {
            if (current !== revision) return;
            // Restore the fully functional native form if enhancement cannot continue.
            active = false;
            manual.hidden = false;
            help.textContent = globalThis.mediClinicLocalize?.(originalHelp) || originalHelp;
            status.textContent = (globalThis.mediClinicText?.('Seçenekler otomatik yüklenemedi. Seçenekleri güncelle düğmesiyle devam edin.') || 'Seçenekler otomatik yüklenemedi. Seçenekleri güncelle düğmesiyle devam edin.');
        } finally {
            if (current === revision) { loading = false; updateState(); }
        }
    });
    form.addEventListener('submit', event => {
        if (loading || submitting || (active && !ready())) { event.preventDefault(); return; }
        submitting = true;
        // Disabling the submitter here would remove its Stage value from the native GET.
    });
    window.addEventListener('pageshow', () => { submitting = false; updateState(); });
    help.textContent = (globalThis.mediClinicText?.('Klinik, şube, bölüm, doktor ve hizmeti sırayla seçin. Seçenekler otomatik güncellenir.') || 'Klinik, şube, bölüm, doktor ve hizmeti sırayla seçin. Seçenekler otomatik güncellenir.');
    manual.hidden = true;
    updateState();
})();
