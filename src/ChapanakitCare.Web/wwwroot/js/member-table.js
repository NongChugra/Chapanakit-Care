(() => {
    const table = document.querySelector('#member-table');
    if (!table) return;

    const picker = document.querySelector('#column-picker');
    const token = document.querySelector('#preference-token input').value;
    const allColumns = [...table.querySelectorAll('thead th')].map(cell => cell.dataset.column);
    const allComponents = [...picker.querySelectorAll('[data-component-toggle]')].map(input => input.value);
    const initial = window.memberTablePreference || { columnOrder: [], hiddenColumns: [], visibleComponents: allComponents };

    const valueFrom = (object, camel, pascal, fallback) => object[camel] ?? object[pascal] ?? fallback;
    const initialOrder = valueFrom(initial, 'columnOrder', 'ColumnOrder', []);
    const initialHidden = valueFrom(initial, 'hiddenColumns', 'HiddenColumns', []);
    const initialComponents = valueFrom(initial, 'visibleComponents', 'VisibleComponents', allComponents);
    let sortColumn = valueFrom(initial, 'sortColumn', 'SortColumn', null);
    let sortDirection = valueFrom(initial, 'sortDirection', 'SortDirection', null);

    const moveColumn = (name, index) => {
        table.querySelectorAll('tr').forEach(row => {
            const cell = row.querySelector(`[data-column="${name}"]`);
            if (cell) row.insertBefore(cell, row.children[index] || null);
        });
    };
    const applyOrder = order => {
        const valid = [...new Set(order.filter(name => allColumns.includes(name)))];
        let next = [...valid, ...allColumns.filter(name => !valid.includes(name))];
        if (!order.includes('role')) {
            next = next.filter(name => name !== 'role');
            const nameIndex = next.indexOf('name');
            next.splice(nameIndex < 0 ? next.length : nameIndex + 1, 0, 'role');
        }
        next.forEach(moveColumn);
    };
    const applyVisibility = hidden => {
        allColumns.forEach(name => {
            const isHidden = hidden.includes(name);
            table.querySelectorAll(`[data-column="${name}"]`).forEach(cell => cell.hidden = isHidden);
            const checkbox = picker.querySelector(`[data-column-toggle][value="${name}"]`);
            if (checkbox) checkbox.checked = !isHidden;
        });
    };
    const renderComposites = visible => {
        const selected = new Set(visible);
        table.querySelectorAll('tbody [data-column="name"]').forEach(cell => {
            const firstPart = `${selected.has('name.title') ? cell.dataset.title || '' : ''}${selected.has('name.firstName') ? cell.dataset.firstName || '' : ''}`;
            const parts = [firstPart, selected.has('name.lastName') ? cell.dataset.lastName || '' : ''].filter(Boolean);
            cell.textContent = parts.join(' ') || '–';
        });
        table.querySelectorAll('tbody [data-column="address"]').forEach(cell => {
            const parts = [
                selected.has('address.houseNo') && cell.dataset.houseNo ? cell.dataset.houseNo : '',
                selected.has('address.moo') && cell.dataset.moo ? `ม.${cell.dataset.moo}` : '',
                selected.has('address.under') && cell.dataset.under ? cell.dataset.under : '',
                selected.has('address.subdistrict') && cell.dataset.subdistrict ? `ต.${cell.dataset.subdistrict}` : '',
                selected.has('address.district') && cell.dataset.district ? `อ.${cell.dataset.district}` : '',
                selected.has('address.province') && cell.dataset.province ? `จ.${cell.dataset.province}` : '',
                selected.has('address.postalCode') && cell.dataset.postalCode ? cell.dataset.postalCode : ''
            ].filter(Boolean);
            cell.textContent = parts.join(' ') || '–';
        });
        allComponents.forEach(name => {
            const checkbox = picker.querySelector(`[data-component-toggle][value="${name}"]`);
            if (checkbox) checkbox.checked = selected.has(name);
        });
    };

    const currentOrder = () => [...table.querySelectorAll('thead th')].map(cell => cell.dataset.column);
    const currentHidden = () => [...picker.querySelectorAll('[data-column-toggle]:not(:checked)')].map(input => input.value);
    const currentComponents = () => [...picker.querySelectorAll('[data-component-toggle]:checked')].map(input => input.value);
    const save = async () => fetch('?handler=Preference', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
        body: JSON.stringify({
            columnOrder: currentOrder(),
            hiddenColumns: currentHidden(),
            visibleComponents: currentComponents(),
            sortColumn,
            sortDirection
        })
    });

    applyOrder(initialOrder);
    applyVisibility(initialHidden);
    renderComposites(initialComponents);
    picker.addEventListener('change', event => {
        if (event.target.matches('[data-column-toggle]')) applyVisibility(currentHidden());
        if (event.target.matches('[data-component-toggle]')) renderComposites(currentComponents());
        save();
    });

    let dragged;
    let movedDuringDrag = false;
    let ignoreNextClick = false;
    const thead = table.querySelector('thead');
    thead.addEventListener('dragstart', event => {
        dragged = event.target.closest('th');
        if (!dragged) return;
        movedDuringDrag = false;
    });
    thead.addEventListener('dragover', event => {
        event.preventDefault();
        const target = event.target.closest('th');
        if (!dragged || !target || dragged === target) return;
        const targetIndex = [...target.parentNode.children].indexOf(target);
        moveColumn(dragged.dataset.column, targetIndex);
        movedDuringDrag = true;
    });
    thead.addEventListener('dragend', () => {
        ignoreNextClick = movedDuringDrag;
        dragged = null;
        if (movedDuringDrag) save();
    });

    const updateSortIndicator = () => {
        table.querySelectorAll('thead th').forEach(header => {
            const active = header.dataset.column === sortColumn;
            header.setAttribute('aria-sort', active ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none');
            const indicator = header.querySelector('.sort-indicator');
            if (indicator) indicator.textContent = active ? (sortDirection === 'asc' ? ' ▲' : ' ▼') : '';
        });
    };
    thead.addEventListener('click', async event => {
        if (ignoreNextClick) { ignoreNextClick = false; return; }
        const header = event.target.closest('th');
        if (!header || header.dataset.sortable !== 'true') return;
        const column = header.dataset.column;
        if (sortColumn !== column) {
            sortColumn = column;
            sortDirection = 'asc';
        } else if (sortDirection === 'asc') {
            sortDirection = 'desc';
        } else {
            sortColumn = null;
            sortDirection = null;
        }
        await save();
        window.location.reload();
    });
    updateSortIndicator();

    document.querySelector('#reset-columns')?.addEventListener('click', async () => {
        await fetch('?handler=ResetPreference', { method: 'POST', headers: { 'RequestVerificationToken': token } });
        applyOrder(allColumns);
        applyVisibility([]);
        renderComposites(allComponents);
        sortColumn = null;
        sortDirection = null;
        window.location.reload();
    });

    document.querySelector('#export-members')?.addEventListener('click', () => {
        const columns = currentOrder().filter(name => name !== 'actions' && !currentHidden().includes(name));
        if (!columns.length) {
            window.alert('กรุณาเลือกอย่างน้อยหนึ่งคอลัมน์ก่อนส่งออก');
            return;
        }
        const url = new URL(window.location.href);
        url.searchParams.delete('PageNumber');
        url.searchParams.delete('PageSize');
        url.searchParams.delete('columns');
        url.searchParams.delete('components');
        url.searchParams.set('handler', 'Export');
        columns.forEach(name => url.searchParams.append('columns', name));
        currentComponents().forEach(name => url.searchParams.append('components', name));
        window.location.assign(url);
    });
})();
