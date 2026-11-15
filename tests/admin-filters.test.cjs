const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('src/MediClinic.Web/wwwroot/js/admin-filters.js', 'utf8');

test('branch changes constrain departments, clear an incompatible selection and allow all branches', () => {
    const options = [{ value: '', dataset: {} }, { value: 'd1', dataset: { branch: 'b1' } }, { value: 'd2', dataset: { branch: 'b2' } }];
    const department = { options, value: 'd1', get selectedOptions() { return options.filter(o => o.value === this.value); } };
    let change;
    const branch = { value: 'b2', addEventListener: (_, fn) => change = fn };
    vm.runInNewContext(source, { document: { querySelector: selector => selector === '[data-filter-branch]' ? branch : department } });
    change();
    assert.equal(department.value, '');
    assert.equal(options[1].disabled, true);
    assert.equal(options[2].disabled, false);
    branch.value = ''; change();
    assert.ok(options.every(o => !o.hidden && !o.disabled));
});

test('shared filter script tolerates pages with no filters', () => {
    assert.doesNotThrow(() => vm.runInNewContext(source, { document: { querySelector: () => null } }));
});

test('medical refresh updates only marked names and keeps form nodes and personal text', async () => {
    const medicalSource = fs.readFileSync('src/MediClinic.Web/wwwroot/js/medical-names.js', 'utf8');
    const label = { dataset: { medicalName: 'service:1' }, textContent: 'Muayene' };
    const person = { textContent: 'Muayene', value: 'unchanged password' };
    let language;
    const document = {
        addEventListener: (_, fn) => language = fn,
        querySelector: selector => selector === '[data-booking-selection]' ? null : label,
        querySelectorAll: () => [label], dispatchEvent() {}
    };
    const fresh = { querySelectorAll: selector => selector === '[data-medical-name]' ? [{ dataset: label.dataset, textContent: 'Consultation' }] : [] };
    vm.runInNewContext(medicalSource, { document, location: { href: 'https://example.test/Appointments' }, URL, CustomEvent: class {},
        fetch: async () => ({ ok: true, redirected: false, text: async () => '' }), DOMParser: class { parseFromString() { return fresh; } } });
    await language();
    assert.equal(label.textContent, 'Consultation');
    assert.equal(person.textContent, 'Muayene');
    assert.equal(person.value, 'unchanged password');
});

test('culture refresh includes live booking selections even before medical options are loaded', async () => {
    const medicalSource = fs.readFileSync('src/MediClinic.Web/wwwroot/js/medical-names.js', 'utf8');
    let language, requested;
    const target = { tagName: 'SELECT', value: 'd1', replaceChildren(...options) { this.options = options; this.value = options.find(o => o.selected).value; } };
    const booking = { contains: element => element === target };
    const document = {
        addEventListener: (_, fn) => language = fn,
        querySelector: selector => selector === '[data-booking-selection]' ? booking : null,
        querySelectorAll: () => [], getElementById: () => target, dispatchEvent() {}
    };
    const option = { value: 'd1', textContent: 'Dermatology', selected: true, cloneNode() { return { ...this }; } };
    const fresh = { querySelectorAll: selector => selector === 'select' ? [{ id: 'DepartmentId', options: [option], disabled: false }] : [] };
    vm.runInNewContext(medicalSource, { document, location: { href: 'https://example.test/Appointments/Book' }, URL, CustomEvent: class {},
        FormData: class { *[Symbol.iterator]() { yield ['BranchId', 'b2']; yield ['DepartmentId', 'd1']; } },
        fetch: async url => { requested = url; return { ok: true, text: async () => '' }; }, DOMParser: class { parseFromString() { return fresh; } } });
    await language();
    assert.equal(requested.searchParams.get('BranchId'), 'b2');
    assert.equal(requested.searchParams.get('Stage'), '1');
    assert.equal(target.value, 'd1');
    assert.equal(target.options[0].textContent, 'Dermatology');
});
