(() => {
    const root = document.documentElement;
    let messages = JSON.parse(document.getElementById('ui-messages').textContent);
    const catalogues = [Object.fromEntries(Object.keys(messages).map(key => [key, key])), messages];
    window.mediClinicText = key => messages[key] || key;
    const t = key => window.mediClinicText(key);
    const themeButton = document.querySelector('[data-theme-toggle]');
    const syncTheme = () => {
        const dark = root.dataset.theme === 'dark';
        themeButton.setAttribute('aria-pressed', String(dark));
        themeButton.setAttribute('aria-label', t(dark ? 'Açık temaya geç' : 'Koyu temaya geç'));
    };
    syncTheme();
    themeButton.addEventListener('click', () => {
        root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem('mediclinic-theme', root.dataset.theme); } catch { /* The control still works for this page. */ }
        syncTheme();
    });
    window.addEventListener('storage', event => {
        if (event.key === 'mediclinic-theme') { root.dataset.theme = event.newValue === 'dark' ? 'dark' : 'light'; syncTheme(); }
    });

    // Only catalogue messages are translated. Free-text data and form values are excluded.
    const translate = (value, previous, next) => {
        const trimmed = value.trim();
        for (const key of Object.keys(previous)) {
            if (previous[key] === trimmed) return value.replace(trimmed, next[key] || key);
        }
        // Resource format strings retain their variable portions (counts and accessible names).
        for (const key of Object.keys(previous).filter(key => /\{\d+\}/.test(key))) {
            const pattern = previous[key].split(/(\{\d+\})/).map(part => /\{\d+\}/.test(part) ? '(.+?)' : part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('');
            const match = trimmed.match(new RegExp('^' + pattern + '$'));
            if (match) return value.replace(trimmed, (next[key] || key).replace(/\{(\d+)\}/g, (_, index) => match[Number(index) + 1]));
        }
        // An icon and an action label may share one text node.
        for (const key of Object.keys(previous)) {
            if (trimmed.startsWith('✎ ') || trimmed.startsWith('▦ ')) {
                if (trimmed.slice(2) === previous[key]) return value.replace(previous[key], next[key] || key);
            }
        }
        return value;
    };
    const refreshDatesAndMoney = () => {
        const culture = root.dataset.culture;
        const money = value => new Intl.NumberFormat(culture, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value)) + (culture === 'tr-TR' ? ' TL' : ' TRY');
        document.querySelectorAll('[data-service-name]').forEach(option => {
            option.textContent = t('{0} - {1} dakika - {2}').replace('{0}', option.dataset.serviceName).replace('{1}', option.dataset.serviceMinutes).replace('{2}', money(option.dataset.servicePrice));
        });
        document.querySelectorAll('[data-money]').forEach(element => {
            element.textContent = money(element.dataset.money);
        });
        document.querySelectorAll('time[datetime]').forEach(element => {
            const date = new Date(element.getAttribute('datetime'));
            if (Number.isNaN(date.valueOf())) return;
            const options = { timeZone: 'Europe/Istanbul', day: '2-digit', month: 'long', year: 'numeric' };
            if (element.dataset.dateOnly !== 'true') Object.assign(options, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
            element.textContent = new Intl.DateTimeFormat(culture, options).format(date);
        });
    };
    window.mediClinicRefresh = refreshDatesAndMoney;
    window.mediClinicLocalize = value => {
        for (const catalogue of catalogues) {
            const translated = translate(value, catalogue, messages);
            if (translated !== value) return translated;
        }
        return value;
    };
    const form = document.querySelector('[data-language-form]');
    const status = document.querySelector('[data-preferences-status]');
    let changing = false;
    form.addEventListener('submit', async event => {
        if (!window.fetch || !event.submitter) return; // Native secure POST remains available.
        event.preventDefault();
        if (changing) return;
        changing = true;
        form.setAttribute('aria-busy', 'true');
        const data = new FormData(form);
        data.set('culture', event.submitter.value);
        try {
            const response = await fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (!response.ok || response.redirected) throw new Error('language');
            const result = await response.json();
            if (!['tr-TR', 'en-GB'].includes(result.culture) || !result.messages) throw new Error('culture');
            const previous = messages;
            messages = result.messages;
            catalogues.push(messages);
            const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
            const nodes = [];
            while (walker.nextNode()) nodes.push(walker.currentNode);
            nodes.forEach(node => {
                if (!node.parentElement.closest('script, style, input, textarea, [data-user-content]')) node.nodeValue = translate(node.nodeValue, previous, messages);
            });
            document.querySelectorAll('[data-hours-summary]').forEach(summary => {
                const dayKeys = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];
                summary.textContent = summary.textContent.split(' · ').map(part => {
                    for (const key of dayKeys) if (part.startsWith(previous[key] + ':')) {
                        part = (messages[key] || key) + part.slice(previous[key].length);
                        const closed = previous['Kapalı'];
                        if (part.endsWith(': ' + closed)) part = part.slice(0, -closed.length) + messages['Kapalı'];
                        break;
                    }
                    return part;
                }).join(' · ');
            });
            document.querySelectorAll('*').forEach(element => {
                if (element.closest('[data-user-content]')) return;
                ['aria-label', 'title', 'placeholder', 'data-val-required', 'data-val-email', 'data-val-length', 'data-val-range', 'data-day-name'].forEach(name => {
                    if (element.hasAttribute(name)) element.setAttribute(name, translate(element.getAttribute(name), previous, messages));
                });
            });
            document.title = translate(document.title.replace(/ - MediClinic$/, ''), previous, messages) + ' - MediClinic';
            root.lang = result.culture === 'tr-TR' ? 'tr' : 'en';
            root.dataset.culture = result.culture;
            form.querySelectorAll('button[name="culture"]').forEach(button => button.setAttribute('aria-pressed', String(button.value === result.culture)));
            refreshDatesAndMoney(); syncTheme();
            document.querySelectorAll('input, select, textarea').forEach(element => element.setCustomValidity(''));
            document.dispatchEvent(new CustomEvent('mediclinic:language'));
            status.textContent = t('Dil değiştirildi.');
        } catch {
            status.textContent = t('Dil değiştirilemedi. Lütfen tekrar deneyiniz.');
        } finally { changing = false; form.removeAttribute('aria-busy'); }
    });
})();
