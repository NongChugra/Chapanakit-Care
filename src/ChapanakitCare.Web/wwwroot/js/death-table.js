(() => {
    const table = document.querySelector('#death-table');
    const picker = document.querySelector('#death-column-picker');
    if (!table || !picker) return;
    const token = document.querySelector('#death-preference-token input').value;
    const tbody = table.querySelector('tbody');
    const allColumns = [...table.querySelectorAll('thead th')].map(cell => cell.dataset.column);
    const initial = window.deathTablePreference || {};
    const valueFrom = (object, camel, pascal, fallback) => object[camel] ?? object[pascal] ?? fallback;
    let sortColumn = valueFrom(initial, 'sortColumn', 'SortColumn', null);
    let sortDirection = valueFrom(initial, 'sortDirection', 'SortDirection', null);
    const moveColumn = (name, index) => table.querySelectorAll('tr').forEach(row => {
        const cell = row.querySelector(`[data-column="${name}"]`);
        if (cell) row.insertBefore(cell, row.children[index] || null);
    });
    const applyOrder = order => {
        const valid = order.filter(name => allColumns.includes(name));
        [...valid, ...allColumns.filter(name => !valid.includes(name))].forEach(moveColumn);
    };
    const applyVisibility = hidden => allColumns.forEach(name => {
        const isHidden = hidden.includes(name);
        table.querySelectorAll(`[data-column="${name}"]`).forEach(cell => cell.hidden = isHidden);
        const checkbox = picker.querySelector(`[data-column-toggle][value="${name}"]`);
        if (checkbox) checkbox.checked = !isHidden;
    });
    const currentOrder = () => [...table.querySelectorAll('thead th')].map(cell => cell.dataset.column);
    const currentHidden = () => [...picker.querySelectorAll('[data-column-toggle]:not(:checked)')].map(input => input.value);
    const save = async () => fetch('?handler=Preference', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
        body: JSON.stringify({ columnOrder: currentOrder(), hiddenColumns: currentHidden(), sortColumn, sortDirection })
    });
    applyOrder(valueFrom(initial, 'columnOrder', 'ColumnOrder', []));
    applyVisibility(valueFrom(initial, 'hiddenColumns', 'HiddenColumns', []));
    picker.addEventListener('change', () => { applyVisibility(currentHidden()); save(); });
    let dragged;
    let movedDuringDrag = false;
    let ignoreNextClick = false;
    const thead = table.querySelector('thead');
    thead.addEventListener('dragstart', event => { dragged = event.target.closest('th'); movedDuringDrag = false; });
    thead.addEventListener('dragover', event => {
        event.preventDefault();
        const target = event.target.closest('th');
        if (!dragged || !target || dragged === target) return;
        moveColumn(dragged.dataset.column, [...target.parentNode.children].indexOf(target));
        movedDuringDrag = true;
    });
    thead.addEventListener('dragend', () => {
        ignoreNextClick = movedDuringDrag;
        dragged = null;
        if (movedDuringDrag) save();
    });
    const updateSortIndicator = () => table.querySelectorAll('thead th').forEach(header => {
        const active = header.dataset.column === sortColumn;
        header.setAttribute('aria-sort', active ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none');
        const indicator = header.querySelector('.sort-indicator');
        if (indicator) indicator.textContent = active ? (sortDirection === 'asc' ? ' ▲' : ' ▼') : '';
    });
    const sortRows = () => {
        [...tbody.querySelectorAll('tr')].sort((left, right) => {
            if (!sortColumn) return Number(left.dataset.originalOrder) - Number(right.dataset.originalOrder);
            const leftValue = left.querySelector(`[data-column="${sortColumn}"]`)?.dataset.sortValue || '';
            const rightValue = right.querySelector(`[data-column="${sortColumn}"]`)?.dataset.sortValue || '';
            const comparison = leftValue.localeCompare(rightValue, 'th', { numeric: true, sensitivity: 'base' });
            return sortDirection === 'asc' ? comparison : -comparison;
        }).forEach(row => tbody.appendChild(row));
        updateSortIndicator();
    };
    thead.addEventListener('click', event => {
        if (ignoreNextClick) { ignoreNextClick = false; return; }
        const header = event.target.closest('th');
        if (!header || header.dataset.sortable !== 'true') return;
        const column = header.dataset.column;
        if (sortColumn !== column) { sortColumn = column; sortDirection = 'asc'; }
        else if (sortDirection === 'asc') sortDirection = 'desc';
        else { sortColumn = null; sortDirection = null; }
        sortRows();
        save();
    });
    sortRows();
    document.querySelector('#reset-death-columns')?.addEventListener('click', async () => {
        await fetch('?handler=ResetPreference', { method: 'POST', headers: { 'RequestVerificationToken': token } });
        applyOrder(allColumns);
        applyVisibility([]);
        sortColumn = null;
        sortDirection = null;
        sortRows();
    });
})();
