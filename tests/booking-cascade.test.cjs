// Run with node --test tests/booking-cascade.test.cjs; no packages required.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');
const source = readFileSync(join(__dirname, '../src/MediClinic.Web/wwwroot/js/booking.js'), 'utf8');
const empty = '00000000-0000-0000-0000-000000000000';
const names = ['ClinicId', 'BranchId', 'DepartmentId', 'DoctorId', 'ServiceId'];
function select(name, value = empty, disabled = false) {
    return { name, value, disabled, options: [{ value: empty }, { value }],
        replaceChildren(...options) {
            this.options = options;
            this.value = options.find(option => option.selected)?.value || options[0].value;
        } };
}
function setup({ supported = true } = {}) {
    const selects = names.map(name => select(name));
    const manual = { hidden: false }, next = {}, status = {}, help = { textContent: 'Manual instructions' };
    const listeners = {}, windowListeners = {}, requests = [];
    const controls = { '[data-booking-update]': manual, '[data-booking-continue]': next, '[data-booking-status]': status };
    const form = { action: 'https://localhost/Appointments/Book', attributes: {},
        querySelectorAll: () => selects, querySelector: selector => controls[selector],
        setAttribute(name, value) { this.attributes[name] = value; },
        addEventListener(name, listener) { listeners[name] = listener; } };
    const fetch = (url, options) => new Promise((resolve, reject) => requests.push({ url, options, resolve, reject }));
    const context = { document: { querySelector: () => form, getElementById: () => help },
        window: { fetch: supported && fetch, AbortController, DOMParser: true,
            location: { href: form.action }, addEventListener(name, listener) { windowListeners[name] = listener; } },
        fetch, AbortController, URL, URLSearchParams,
        Option: function (text, value) { this.text = text; this.value = value; },
        FormData: class { constructor() { this.values = selects.filter(s => !s.disabled).map(s => [s.name, s.value]); }
            [Symbol.iterator]() { return this.values[Symbol.iterator](); } },
        DOMParser: class { parseFromString(text) {
            const values = JSON.parse(text);
            return { querySelector: selector => selector === '[data-booking-selection]' ? {
                querySelector: selector => {
                    const name = /name="([^"]+)"/.exec(selector)[1];
                    const item = values[names.indexOf(name)];
                    return { disabled: item.disabled, options: item.options.map(option => ({
                        ...option, cloneNode() { return { ...this }; }
                    })) };
                }
            } : null };
        } } };
    vm.runInNewContext(source, context);
    const change = (index, value) => { selects[index].value = value; return listeners.change({ target: selects[index] }); };
    const respond = (index, chosen, available = []) => {
        const values = names.map((name, i) => ({ disabled: i > chosen.length,
            options: [{ value: empty, selected: !chosen[i] },
                ...(chosen[i] ? [{ value: chosen[i], selected: true }] : available.map(value => ({ value })))] }));
        requests[index].resolve({ ok: true, redirected: false, text: async () => JSON.stringify(values) });
    };
    const submit = () => { let prevented = false; listeners.submit({ preventDefault: () => { prevented = true; } }); return prevented; };
    return { selects, manual, next, status, help, requests, change, respond, submit, form, listeners, windowListeners };
}

test('automatic cascades retain ancestors, clear descendants and never auto-select the next option', async () => {
    const ui = setup();
    assert.equal(ui.manual.hidden, true);
    const chosen = [];
    for (let index = 0; index < 4; index++) {
        chosen.push('chosen-' + index);
        const pending = ui.change(index, chosen[index]);
        assert.equal(ui.form.attributes['aria-busy'], 'true');
        assert.equal(ui.next.disabled, true);
        assert.equal(ui.submit(), true);
        assert.match(ui.status.textContent, /yükleniyor/);
        const query = ui.requests[index].url.searchParams;
        assert.equal(query.get('Stage'), '1');
        for (let ancestor = 0; ancestor <= index; ancestor++) assert.equal(query.get(names[ancestor]), chosen[ancestor]);
        for (const descendant of ui.selects.slice(index + 1)) {
            assert.equal(descendant.value, empty); assert.equal(descendant.disabled, true);
        }
        ui.respond(index, chosen, ['available-but-unselected']);
        await pending;
        assert.equal(ui.selects[index + 1].value, empty);
        assert.equal(ui.selects[index + 1].disabled, false);
        assert.equal(ui.form.attributes['aria-busy'], 'false');
    }
    await ui.change(4, 'service');
    assert.equal(ui.requests.length, 4); // Service changes do not navigate or reserve anything.
    assert.equal(ui.selects[4].value, 'service');
    assert.equal(ui.next.disabled, false);
    assert.equal(ui.submit(), false);
    assert.equal(ui.submit(), true); // A second continue submission is suppressed.
});

test('rapid parent changes abort earlier requests and ignore late responses', async () => {
    const ui = setup();
    const first = ui.change(0, 'first-clinic');
    const second = ui.change(0, 'latest-clinic');
    assert.equal(ui.requests[0].options.signal.aborted, true);
    ui.respond(1, ['latest-clinic'], ['latest-branch']);
    await second;
    ui.respond(0, ['first-clinic'], ['stale-branch']);
    await first; // Simulate a response arriving even after cancellation.
    assert.equal(ui.selects[0].value, 'latest-clinic');
    assert.equal(ui.selects[1].options[1].value, 'latest-branch');
    assert.equal(ui.next.disabled, true);
});

test('parent changes clear previously completed selections without disturbing ancestors', async () => {
    const ui = setup();
    ui.selects.forEach((control, index) => { control.value = 'old-' + index; });
    const pending = ui.change(2, 'new-department');
    assert.equal(ui.selects[0].value, 'old-0');
    assert.equal(ui.selects[1].value, 'old-1');
    assert.equal(ui.selects[3].value, empty);
    assert.equal(ui.selects[4].value, empty);
    ui.respond(0, ['old-0', 'old-1', 'new-department'], []);
    await pending;
    assert.equal(ui.selects[3].value, empty);
    assert.equal(ui.next.disabled, true);
});

test('network failure restores manual instructions and a usable native GET submission', async () => {
    const ui = setup();
    const pending = ui.change(0, 'clinic');
    ui.requests[0].reject(new Error('offline'));
    await pending;
    assert.equal(ui.manual.hidden, false);
    assert.equal(ui.help.textContent, 'Manual instructions');
    assert.match(ui.status.textContent, /otomatik yüklenemedi/);
    assert.equal(ui.submit(), false);
});

test('unsupported enhancement leaves the manual fallback untouched', () => {
    const ui = setup({ supported: false });
    assert.equal(ui.manual.hidden, false);
    assert.equal(ui.help.textContent, 'Manual instructions');
    assert.equal(ui.listeners.change, undefined);
});
