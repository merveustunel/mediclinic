(() => {
    const branch = document.querySelector('[data-filter-branch]');
    const department = document.querySelector('[data-filter-department]');
    if (!branch || !department) return;
    branch.addEventListener('change', () => {
        for (const option of department.options) {
            option.hidden = !!(option.value && branch.value && option.dataset.branch !== branch.value);
            option.disabled = option.hidden;
        }
        if (department.selectedOptions[0]?.disabled) department.value = '';
    });
})();
