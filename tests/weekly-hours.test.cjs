const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('src/MediClinic.Web/wwwroot/js/weekly-hours.js', 'utf8');
function setup() {
    const control = value => ({ value, disabled: false, addEventListener(name, fn) { this[name] = fn; }, setCustomValidity(value) { this.validity = value; } });
    const rows = Array.from({ length: 7 }, (_, day) => {
        const state = control('true'), inputs = [control(''), control('')];
        return { dataset: { day: String(day), dayName: `Day ${day}` }, state, inputs, querySelector: () => state, querySelectorAll: () => inputs };
    });
    const weekdays = control(), copy = control(), select = control('1'), status = {};
    const controls = [weekdays, copy, select, ...rows.flatMap(r => [r.state, ...r.inputs])];
    const selectors = { '[data-status]': status, '[data-weekdays]': weekdays, '[data-copy]': copy, '[data-source]': select };
    const summary = { textContent: 'Previously saved' };
    const form = { action: '/ClinicManagement/WeeklyHours', closest: () => ({ querySelector: () => summary }), querySelector: s => selectors[s], querySelectorAll: s => s === '[data-day]' ? rows : controls,
        reportValidity: () => !rows.some(r => r.inputs.some(i => i.validity)), addEventListener(name, fn) { this[name] = fn; } };
    const requests = [];
    vm.runInNewContext(source, { document: { querySelectorAll: () => [form] }, FormData: class { constructor() { this.token = 'antiforgery'; } },
        fetch: async (url, options) => { requests.push({ url, options }); if (requests.fail) throw Error('Network error');
            return { ok: !requests.invalid, headers: { get: () => 'application/json' }, json: async () => ({ message: requests.invalid ? 'Invalid hours' : 'Saved' }) }; } });
    return { form, rows, weekdays, copy, select, status, requests, summary };
}
test('weekday shortcut and copy preserve closed weekends and isolate branch forms', () => {
    const a = setup(), b = setup(); a.weekdays.click();
    for (let d = 1; d <= 5; d++) { assert.equal(a.rows[d].state.value, 'false'); assert.equal(a.rows[d].inputs[0].value, '09:00'); }
    a.rows[1].inputs[0].value = '10:00'; a.copy.click();
    assert.equal(a.rows[2].inputs[0].value, '10:00');
    for (const d of [0, 6]) { assert.equal(a.rows[d].state.value, 'true'); assert.equal(a.rows[d].inputs[0].disabled, true); }
    assert.equal(b.rows[1].state.value, 'true');
});
test('success, validation and network failures preserve input and restore controls', async () => {
    const a = setup(); a.weekdays.click();
    let prevented = 0;
    const submit = () => a.form.submit({ preventDefault() { prevented++; } });
    await submit(); assert.equal(a.status.textContent, 'Saved'); assert.equal(a.requests[0].options.body.token, 'antiforgery');
    a.requests.invalid = true; await submit(); assert.equal(a.status.textContent, 'Invalid hours');
    a.requests.fail = true; await submit(); assert.equal(a.status.textContent, 'Bağlantı hatası. Değerleriniz korunuyor; tekrar deneyiniz.');
    assert.equal(a.rows[1].inputs[0].value, '09:00'); assert.equal(a.rows[1].inputs[0].disabled, false); assert.equal(a.rows[0].inputs[0].disabled, true);
    assert.equal(prevented, 3);
});
test('invalid intervals never reach fetch and closed sources cannot open days', async () => {
    const a = setup(); a.copy.click(); assert.match(a.status.textContent, /kaynak/);
    a.weekdays.click(); a.rows[1].inputs[1].value = '08:00';
    await a.form.submit({ preventDefault() {} }); assert.equal(a.requests.length, 0);
});

test('summary changes only after a successful save and stays isolated to its branch', async () => {
    const a = setup(), b = setup();
    a.weekdays.click();
    a.rows[1].inputs[0].value = '10:15';
    await a.form.submit({ preventDefault() {} });
    assert.match(a.summary.textContent, /Day 1: 10:15–17:00/);
    assert.match(a.summary.textContent, /Day 0: Kapalı/);
    assert.equal(b.summary.textContent, 'Previously saved');
    const saved = a.summary.textContent;
    a.rows[1].inputs[0].value = '11:30';
    a.requests.invalid = true;
    await a.form.submit({ preventDefault() {} });
    assert.equal(a.summary.textContent, saved);
    a.requests.fail = true;
    await a.form.submit({ preventDefault() {} });
    assert.equal(a.summary.textContent, saved);
});
