const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const init = fs.readFileSync('src/MediClinic.Web/wwwroot/js/theme-init.js', 'utf8');
const source = fs.readFileSync('src/MediClinic.Web/wwwroot/js/preferences.js', 'utf8');
const tr = { 'Giriş yap': 'Giriş yap', 'Dil': 'Dil', 'Koyu temaya geç': 'Koyu temaya geç', 'Açık temaya geç': 'Açık temaya geç', 'Dil değiştirildi.': 'Dil değiştirildi.', 'Dil değiştirilemedi. Lütfen tekrar deneyiniz.': 'Dil değiştirilemedi. Lütfen tekrar deneyiniz.', '{0} randevu': '{0} randevu' };
const en = { 'Giriş yap': 'Sign in', 'Dil': 'Language', 'Koyu temaya geç': 'Switch to dark theme', 'Açık temaya geç': 'Switch to light theme', 'Dil değiştirildi.': 'Language changed.', 'Dil değiştirilemedi. Lütfen tekrar deneyiniz.': 'Could not change language.', '{0} randevu': '{0} appointments' };
function setup(saved, brokenStorage = false) {
    const listeners = {};
    const element = (attributes = {}) => ({ attributes, dataset: {}, value: '', textContent: '', hasAttribute: key => key in attributes, getAttribute: key => attributes[key], setAttribute: (key, value) => attributes[key] = value, removeAttribute: key => delete attributes[key], addEventListener(name, fn) { this[name] = fn; }, closest() { return null; } });
    const root = { dataset: { culture: 'tr-TR', theme: 'light' }, lang: 'tr' };
    const theme = element(), form = element({ 'aria-label': 'Dil' }), status = element();
    form.action = '/Preferences/Language';
    const buttons = ['tr-TR', 'en-GB'].map(value => ({ ...element(), value }));
    form.querySelectorAll = () => buttons;
    const field = { value: 'unsaved-password', setCustomValidity() {} };
    const user = element(); user.closest = () => user;
    const nodes = [{ nodeValue: ' Giriş yap ', parentElement: element() }, { nodeValue: 'Giriş yap', parentElement: user }, { nodeValue: '2 randevu', parentElement: element() }];
    const document = { documentElement: root, title: 'Giriş yap - MediClinic', body: {}, getElementById: () => ({ textContent: JSON.stringify(tr) }),
        querySelector: selector => ({ '[data-theme-toggle]': theme, '[data-language-form]': form, '[data-preferences-status]': status })[selector],
        querySelectorAll: selector => selector === '*' ? [form, theme] : selector === 'input, select, textarea' ? [field] : [],
        createTreeWalker: () => { let i = -1; return { nextNode() { return ++i < nodes.length; }, get currentNode() { return nodes[i]; } }; }, dispatchEvent() {} };
    const storage = { value: saved, getItem() { if (brokenStorage) throw Error('denied'); return this.value; }, setItem(key, value) { if (brokenStorage) throw Error('denied'); this.value = value; } };
    const requests = [];
    const window = { fetch: true, addEventListener: (name, fn) => listeners[name] = fn };
    const context = { document, window, localStorage: storage, Intl, NodeFilter: { SHOW_TEXT: 4 }, CustomEvent: class {},
        FormData: class { constructor() { this.values = { __RequestVerificationToken: 'csrf' }; } set(key, value) { this.values[key] = value; } },
        fetch: async (url, options) => { requests.push({url, options}); if (requests.fail) throw Error('network'); return { ok: true, json: async () => ({ culture: options.body.values.culture, messages: options.body.values.culture === 'en-GB' ? en : tr }) }; } };
    vm.runInNewContext(init, context); vm.runInNewContext(source, context);
    return {root, theme, form, status, field, nodes, storage, requests, listeners, document};
}
test('light is default; saved dark is applied before render and survives a new page', () => {
    assert.equal(setup().root.dataset.theme, 'light');
    const app = setup('dark'); assert.equal(app.root.dataset.theme, 'dark');
    assert.equal(app.theme.getAttribute('aria-pressed'), 'true');
    app.theme.click(); assert.equal(app.storage.value, 'light');
    app.theme.click(); assert.equal(setup(app.storage.value).root.dataset.theme, 'dark');
});
test('unavailable storage and invalid saved theme keep controls usable', () => {
    assert.equal(setup('invalid').root.dataset.theme, 'light');
    const app = setup('dark', true); app.theme.click(); assert.equal(app.root.dataset.theme, 'dark');
    app.listeners.storage({key:'mediclinic-theme',newValue:'light'}); assert.equal(app.root.dataset.theme, 'light');
});
test('live language switch preserves password, user text and form nodes while translating labels and counts', async () => {
    const app = setup(); const field = app.field;
    await app.form.submit({ preventDefault(){}, submitter:{value:'en-GB'} });
    assert.equal(app.root.lang, 'en'); assert.equal(app.root.dataset.culture, 'en-GB');
    assert.equal(app.nodes[0].nodeValue, ' Sign in '); assert.equal(app.nodes[1].nodeValue, 'Giriş yap');
    assert.equal(app.nodes[2].nodeValue, '2 appointments');
    assert.equal(app.field, field); assert.equal(app.field.value, 'unsaved-password');
    assert.equal(app.form.getAttribute('aria-label'), 'Language'); assert.equal(app.document.title,'Sign in - MediClinic');
    assert.equal(app.requests[0].options.body.values.__RequestVerificationToken, 'csrf');
    assert.equal(app.requests[0].options.credentials, 'same-origin');
    assert.equal(JSON.stringify(app.requests).includes('unsaved-password'), false);
    await app.form.submit({ preventDefault(){}, submitter:{value:'tr-TR'} });
    assert.equal(app.nodes[0].nodeValue, ' Giriş yap '); assert.equal(app.nodes[2].nodeValue, '2 randevu');
});
test('language network failure keeps the current language, form data and usable control', async () => {
    const app = setup(); app.requests.fail = true;
    await app.form.submit({preventDefault(){},submitter:{value:'en-GB'}});
    assert.equal(app.root.lang, 'tr'); assert.equal(app.field.value,'unsaved-password');
    assert.equal(app.form.hasAttribute('aria-busy'),false); assert.match(app.status.textContent,/tekrar/);
});
