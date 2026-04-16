(function () {
    "use strict";

    const form = document.getElementById("invoiceEditorForm");
    const table = document.getElementById("itemsTable");
    const template = document.getElementById("invoiceRowTemplate");

    if (!form || !table || !template) {
        return;
    }

    const tableBody = table.querySelector("tbody");
    const addRowButton = document.querySelector("[data-invoice-add-row]");
    const discountInput = form.querySelector("[data-discount]");
    const paidInput = form.querySelector("[data-paid]");
    const grandTotalDisplay = document.querySelector("[data-grand-total]");
    const netTotalDisplay = document.querySelector("[data-net-total]");
    const remainingTotalDisplay = document.querySelector("[data-remaining-total]");
    const salesPartyMode = document.querySelector("[data-sales-party-mode]");
    const salesRegisteredClientField = document.querySelector("[data-sales-registered-client-field]");
    const salesWalkInFields = Array.from(document.querySelectorAll("[data-sales-walk-in-field]"));
    const purchaseTransactionType = document.querySelector("[data-purchase-transaction-type]");
    const purchaseSupplierField = document.querySelector("[data-purchase-supplier-field]");
    const purchaseOneTimeSupplierFields = Array.from(document.querySelectorAll("[data-purchase-one-time-supplier-field]"));
    const purchaseClientField = document.querySelector("[data-purchase-client-field]");
    const purchaseClientReturnSupervisorField = document.querySelector("[data-purchase-client-return-supervisor]");
    const purchaseRemainingLabel = document.querySelector("[data-purchase-remaining-label]");
    const registeredClientMode = "RegisteredClient";
    const walkInCustomerMode = "WalkInCustomer";
    const registeredSupplierMode = "RegisteredSupplier";
    const oneTimeSupplierMode = "OneTimeSupplier";
    const registeredClientReturnMode = "RegisteredClientReturn";

    function parseMoney(value) {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function formatMoney(value) {
        return value.toLocaleString("ar-EG", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function getRows() {
        return Array.from(tableBody.querySelectorAll("[data-invoice-row]"));
    }

    function getEditableCells() {
        return Array.from(tableBody.querySelectorAll(".invoice-cell:not([disabled])"));
    }

    function getSelectedOption(row) {
        const select = row.querySelector("[data-material-select]");
        return select ? select.options[select.selectedIndex] : null;
    }

    function syncRowMaterial(row, shouldFillPrice) {
        const option = getSelectedOption(row);
        const codeInput = row.querySelector("[data-material-code]");
        const stockDisplay = row.querySelector("[data-stock-display]");
        const priceInput = row.querySelector("[data-unit-price]");

        if (!option || !option.value) {
            codeInput.value = "";
            stockDisplay.textContent = "0.00";
            return;
        }

        codeInput.value = option.dataset.code || "";
        stockDisplay.textContent = formatMoney(parseMoney(option.dataset.stock));

        if (shouldFillPrice && priceInput && !priceInput.value) {
            const defaultPrice = parseMoney(option.dataset.price);
            if (defaultPrice > 0) {
                priceInput.value = defaultPrice.toFixed(2);
            }
        }
    }

    function syncMaterialFromCode(row) {
        const codeInput = row.querySelector("[data-material-code]");
        const select = row.querySelector("[data-material-select]");
        const typedCode = codeInput.value.trim();

        if (!typedCode || !select) {
            return false;
        }

        const match = Array.from(select.options).find(option =>
            (option.dataset.code || "").toLowerCase() === typedCode.toLowerCase());

        if (!match) {
            codeInput.classList.add("is-invalid");
            return false;
        }

        codeInput.classList.remove("is-invalid");
        select.value = match.value;
        syncRowMaterial(row, true);
        updateRowTotal(row);
        return true;
    }

    function updateRowTotal(row) {
        const quantity = parseMoney(row.querySelector("[data-quantity]")?.value);
        const unitPrice = parseMoney(row.querySelector("[data-unit-price]")?.value);
        const lineTotalInput = row.querySelector("[data-line-total]");
        const lineTotal = quantity * unitPrice;

        if (lineTotalInput) {
            lineTotalInput.value = formatMoney(lineTotal);
        }

        updateTotals();
    }

    function updateTotals() {
        const grandTotal = getRows().reduce((sum, row) => {
            const quantity = parseMoney(row.querySelector("[data-quantity]")?.value);
            const unitPrice = parseMoney(row.querySelector("[data-unit-price]")?.value);
            return sum + (quantity * unitPrice);
        }, 0);

        let discount = parseMoney(discountInput?.value);
        if (discount < 0) {
            discount = 0;
        }

        if (discount > grandTotal) {
            discount = grandTotal;
        }

        if (discountInput && parseMoney(discountInput.value) !== discount) {
            discountInput.value = discount.toFixed(2);
        }

        let paid = parseMoney(paidInput?.value);
        if (paid < 0) {
            paid = 0;
        }

        if (paidInput && parseMoney(paidInput.value) !== paid) {
            paidInput.value = paid.toFixed(2);
        }

        const netTotal = Math.max(grandTotal - discount, 0);
        const remainingTotal = netTotal - paid;

        grandTotalDisplay.textContent = formatMoney(grandTotal);
        netTotalDisplay.textContent = formatMoney(netTotal);
        remainingTotalDisplay.textContent = formatMoney(remainingTotal);
    }

    function reindexRows() {
        getRows().forEach((row, index) => {
            const rowNumber = row.querySelector("[data-row-number]");
            const materialSelect = row.querySelector("[data-material-select]");
            const quantityInput = row.querySelector("[data-quantity]");
            const unitPriceInput = row.querySelector("[data-unit-price]");

            if (rowNumber) {
                rowNumber.textContent = String(index + 1);
            }

            if (materialSelect) {
                materialSelect.name = `Items[${index}].MaterialId`;
            }

            if (quantityInput) {
                quantityInput.name = `Items[${index}].Quantity`;
            }

            if (unitPriceInput) {
                unitPriceInput.name = `Items[${index}].UnitPrice`;
            }
        });
    }

    function addRow(focusNewRow) {
        const index = getRows().length;
        const wrapper = document.createElement("tbody");
        wrapper.innerHTML = template.innerHTML.replaceAll("__index__", String(index)).trim();
        const row = wrapper.firstElementChild;

        tableBody.appendChild(row);
        reindexRows();
        updateTotals();

        if (focusNewRow) {
            row.querySelector("[data-material-code]")?.focus();
        }

        return row;
    }

    function setFieldGroupState(field, isVisible, clearWhenHidden) {
        if (!field) {
            return;
        }

        field.hidden = !isVisible;
        field.querySelectorAll("input, select, textarea").forEach(input => {
            input.disabled = !isVisible;
            if (!isVisible && clearWhenHidden) {
                input.value = "";
            }
        });
    }

    function syncSalesMode(clearInactiveParty) {
        if (!salesPartyMode || !salesRegisteredClientField) {
            return;
        }

        const isWalkInCustomer = salesPartyMode.value === walkInCustomerMode;
        setFieldGroupState(salesRegisteredClientField, !isWalkInCustomer, clearInactiveParty);
        salesWalkInFields.forEach(field => setFieldGroupState(field, isWalkInCustomer, clearInactiveParty));
    }

    function syncPurchaseMode(clearInactiveParty) {
        if (!purchaseTransactionType || !purchaseSupplierField || !purchaseClientField) {
            return;
        }

        const mode = purchaseTransactionType.value;
        const isRegisteredSupplier = mode === registeredSupplierMode;
        const isOneTimeSupplier = mode === oneTimeSupplierMode;
        const isClientReturn = mode === registeredClientReturnMode;

        setFieldGroupState(purchaseSupplierField, isRegisteredSupplier, clearInactiveParty);
        purchaseOneTimeSupplierFields.forEach(field => setFieldGroupState(field, isOneTimeSupplier, clearInactiveParty));
        setFieldGroupState(purchaseClientField, isClientReturn, clearInactiveParty);
        setFieldGroupState(purchaseClientReturnSupervisorField, isClientReturn, clearInactiveParty);
        purchaseClientReturnSupervisorField?.querySelectorAll("input, select, textarea").forEach(input => {
            input.required = isClientReturn;
        });

        if (purchaseRemainingLabel) {
            purchaseRemainingLabel.textContent = isClientReturn
                ? "المتبقي على العميل"
                : "المتبقي على المورد";
        }
    }

    function removeRow(row) {
        if (getRows().length === 1) {
            row.querySelector("[data-material-code]")?.focus();
            return;
        }

        row.remove();
        reindexRows();
        updateTotals();
    }

    function moveToNextCell(currentCell) {
        const editableCells = getEditableCells();
        const currentIndex = editableCells.indexOf(currentCell);

        if (currentIndex < 0) {
            return;
        }

        const nextCell = editableCells[currentIndex + 1];
        if (nextCell) {
            nextCell.focus();
            nextCell.select?.();
            return;
        }

        addRow(true);
    }

    tableBody.addEventListener("change", function (event) {
        const row = event.target.closest("[data-invoice-row]");
        if (!row) {
            return;
        }

        if (event.target.matches("[data-material-select]")) {
            syncRowMaterial(row, true);
            updateRowTotal(row);
        }

        if (event.target.matches("[data-material-code]")) {
            syncMaterialFromCode(row);
        }
    });

    tableBody.addEventListener("input", function (event) {
        const row = event.target.closest("[data-invoice-row]");
        if (!row) {
            return;
        }

        if (event.target.matches("[data-material-code]")) {
            event.target.classList.remove("is-invalid");
        }

        if (event.target.matches("[data-quantity], [data-unit-price]")) {
            updateRowTotal(row);
        }
    });

    tableBody.addEventListener("click", function (event) {
        const removeButton = event.target.closest("[data-invoice-remove-row]");
        if (!removeButton) {
            return;
        }

        removeRow(removeButton.closest("[data-invoice-row]"));
    });

    tableBody.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" || !event.target.matches(".invoice-cell")) {
            return;
        }

        event.preventDefault();

        if (event.target.matches("[data-material-code]")) {
            syncMaterialFromCode(event.target.closest("[data-invoice-row]"));
        }

        moveToNextCell(event.target);
    });

    document.addEventListener("keydown", function (event) {
        if (event.altKey && event.key.toLowerCase() === "n") {
            event.preventDefault();
            addRow(true);
        }

        if (event.ctrlKey && event.key === "Enter") {
            event.preventDefault();
            form.requestSubmit();
        }
    });

    addRowButton?.addEventListener("click", function () {
        addRow(true);
    });

    salesPartyMode?.addEventListener("change", function () {
        syncSalesMode(true);
    });

    purchaseTransactionType?.addEventListener("change", function () {
        syncPurchaseMode(true);
    });

    discountInput?.addEventListener("input", updateTotals);
    paidInput?.addEventListener("input", updateTotals);

    form.addEventListener("submit", function () {
        getRows().forEach(row => syncMaterialFromCode(row));
        reindexRows();
        updateTotals();
    });

    if (getRows().length === 0) {
        addRow(false);
    }

    syncSalesMode(false);
    syncPurchaseMode(false);

    getRows().forEach(row => {
        syncRowMaterial(row, false);
        updateRowTotal(row);
    });
    reindexRows();
    updateTotals();
})();
