(() => {
    document.querySelectorAll('.weekly-hours').forEach(form => {
        const rows = [...form.querySelectorAll('[data-day]')];
        const status = form.querySelector('[data-status]');
        const controls = [...form.querySelectorAll('button, select, input')];
        const fields = row => [...row.querySelectorAll('input[type="time"]')];
        const closed = row => row.querySelector('[data-closed]').value === 'true';
        const sync = row => fields(row).forEach(input => {
            input.disabled = closed(row);
            input.required = !closed(row);
            input.setCustomValidity('');
        });
        rows.forEach(row => {
            row.querySelector('[data-closed]').addEventListener('change', () => sync(row));
            fields(row).forEach(input => input.addEventListener('input', () => fields(row).forEach(f => f.setCustomValidity(''))));
            sync(row);
        });
        form.querySelector('[data-weekdays]').addEventListener('click', () => {
            rows.filter(row => +row.dataset.day >= 1 && +row.dataset.day <= 5).forEach(row => {
                row.querySelector('[data-closed]').value = 'false';
                fields(row).forEach((input, i) => input.value = i ? '17:00' : '09:00');
                sync(row);
            });
            status.textContent = (globalThis.mediClinicText?.('Hafta içi dolduruldu. Değişiklikleri kaydediniz.') || 'Hafta içi dolduruldu. Değişiklikleri kaydediniz.');
        });
        form.querySelector('[data-copy]').addEventListener('click', () => {
            const source = rows.find(row => row.dataset.day === form.querySelector('[data-source]').value);
            const times = fields(source).map(input => input.value);
            if (closed(source) || !times[0] || !times[1] || times[0] >= times[1]) {
                status.textContent = (globalThis.mediClinicText?.('Geçerli saatleri olan açık bir kaynak gün seçiniz.') || 'Geçerli saatleri olan açık bir kaynak gün seçiniz.');
                return;
            }
            rows.filter(row => !closed(row)).forEach(row => {
                fields(row).forEach((input, i) => input.value = times[i]);
                sync(row);
            });
            status.textContent = (globalThis.mediClinicText?.('Saatler açık günlere uygulandı. Değişiklikleri kaydediniz.') || 'Saatler açık günlere uygulandı. Değişiklikleri kaydediniz.');
        });
        form.addEventListener('submit', async event => {
            event.preventDefault();
            rows.forEach(row => {
                const [opens, closes] = fields(row);
                closes.setCustomValidity(!closed(row) && opens.value >= closes.value ? (globalThis.mediClinicText?.('Kapanış saati açılıştan sonra olmalıdır.') || 'Kapanış saati açılıştan sonra olmalıdır.') : '');
            });
            if (!form.reportValidity()) return;
            const body = new FormData(form);
            const savedSummary = rows.map(row => `${row.dataset.dayName}: ${closed(row) ? (globalThis.mediClinicText?.('Kapalı') || 'Kapalı') : fields(row).map(input => input.value).join('–')}`).join(' · ');
            controls.forEach(control => control.disabled = true);
            status.textContent = (globalThis.mediClinicText?.('Kaydediliyor…') || 'Kaydediliyor…');
            let failureMessage;
            try {
                const response = await fetch(form.action, { method: 'POST', body, headers: { Accept: 'application/json' } });
                const result = (response.headers.get('content-type') || '').includes('application/json') ? await response.json() : null;
                if (!response.ok || !result) {
                    failureMessage = result?.message || (globalThis.mediClinicText?.('Kayıt yapılamadı. Oturumunuzu ve bağlantınızı kontrol edip tekrar deneyiniz.') || 'Kayıt yapılamadı. Oturumunuzu ve bağlantınızı kontrol edip tekrar deneyiniz.');
                    throw new Error('save');
                }
                status.textContent = globalThis.mediClinicLocalize?.(result.message) || result.message;
                const summary = form.closest('.branch-hours')?.querySelector('[data-hours-summary]');
                if (summary) summary.textContent = savedSummary;
            } catch (error) {
                status.textContent = (failureMessage && (globalThis.mediClinicLocalize?.(failureMessage) || failureMessage)) || (globalThis.mediClinicText?.('Bağlantı hatası. Değerleriniz korunuyor; tekrar deneyiniz.') || 'Bağlantı hatası. Değerleriniz korunuyor; tekrar deneyiniz.');
            } finally {
                controls.forEach(control => control.disabled = false);
                rows.forEach(sync);
            }
        });
    });
})();
