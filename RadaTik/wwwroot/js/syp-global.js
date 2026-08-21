/**
 * RadaTik — تنسيق عالمي للأرقام: فواصل آلاف للمبالغ (ل.س.ج) وللأعداد (عدد، كمية، …).
 */
(function () {
    'use strict';

    var MIN_COMMA_INTEGER = 1000;

    var MONEY_LABEL_RE = /رصيد|مبلغ|إجمال|صافي|رواتب|مصروف|دخل|محفظة|صندوق|مدفوع|مستحق|عمولة|تكلفة|سعر|قيمة|تحصيل|خصم|إضاف|تغذية|دفع|فاتورة|قبض|سحب|إيراد|المبلغ|الرصيد/i;
    // ملاحظة: لا نستخدم «منفذ» هنا لأنها تطابق عمود «المنفذ» (Port) وتضيف فواصل خاطئة (مثل 8,728).
    // استخدم «منافذ» لأعمدة العدد، و«عدد …» يغطي «عدد المنافذ».
    var COUNT_LABEL_RE = /عدد|أصناف|عمليات|طلبات|عميل|مشترك|منافذ|جهاز|بروفايل|سيرفر|قطاع|كمية|وحدات|نشط|معلق|إجمالي السجلات/i;
    var COUNT_HEADER_RE = /عدد|أصناف|عمليات|طلب|عميل|مشترك|منافذ|جهاز|بروفايل|سيرفر|قطاع|كمية|وحدات|تسلسل/i;
    var PERCENT_HEADER_RE = /نسبة|percent|ضريبة|vat/i;
    var SKIP_HEADER_RE = /^(#|تاريخ|وقت|اسم|حالة|نوع|ملاحظ|بواسطة|إجراء|المنفذ|port)\b/i;

    var MONEY_HEADER_RE = /مبلغ|رصيد|سعر|إجمال|قيمة|مدفوع|المبلغ|الرصيد|قبل|بعد|صافي|عمولة|تكلفة|دخل|مصروف|محصّل|محصل/i;

    var COUNT_INPUT_RE = /count|devices|users|quantity|qty|maxusers|mindevices|maxdevices|unitsperpackage|packagequantity/i;

    var INPUT_EXCLUDE_RE = /percent|vatpercentage|devices|users|^port$|\.port$|portnumber|index|lineindex|unitsperpackage|packagequantity|mindevices|maxdevices|maxusers|month|year|speed|download|upload|quantity$|qty$|count$|id$|age$|radius|priority|order|rank|page|size|limit|offset|hour|minute|second|day(?!s)|vlan|profileid|serverid|networkid|clientid|host|password|pass$|elevation|latitude|longitude|coverage|direction|angle|antenna|noise|snr|ccq|agl|dbm|range$|height|meters|mask|ipaddress/i;
    var INPUT_INCLUDE_RE = /amount|balance|price|total|cost|salary|fee|charge|mblg|deposit|withdraw|paid|payment|revenue|commission|wallet|topup|top-up|invoice|netpay|gross|unitprice|lineprice|retail|wholesale|purchase/i;

    var MONEY_CONTEXT_SELECTOR = '[data-syp-money-context], [data-syp-page-amount-note], .financial-page, .financial-hub-page, .material-invoice-page--purchase, .material-invoice-page--sales';

    function isMoneyFormattingContext(node) {
        if (!node) {
            return false;
        }
        if (node.closest('[data-syp-skip-format]')) {
            return false;
        }
        return !!node.closest(MONEY_CONTEXT_SELECTOR);
    }

    function parseRaw(str) {
        if (str == null || str === '') {
            return NaN;
        }
        var cleaned = String(str)
            .replace(/[\u0660-\u0669]/g, function (d) { return String(d.charCodeAt(0) - 0x0660); })
            .replace(/,/g, '')
            .replace(/[^\d.\-+]/g, '');
        if (!cleaned || cleaned === '.' || cleaned === '-' || cleaned === '+') {
            return NaN;
        }
        var parts = cleaned.split('.');
        if (parts.length > 2) {
            cleaned = parts[0] + '.' + parts.slice(1).join('');
        }
        return parseFloat(cleaned);
    }

    function formatWithCommas(num, maxDecimals) {
        if (!isFinite(num)) {
            return '';
        }
        var abs = Math.abs(num);
        var sign = num < 0 ? '-' : '';
        var fixed = abs.toFixed(maxDecimals);
        var split = fixed.split('.');
        split[0] = split[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        if (maxDecimals === 0) {
            return sign + split[0];
        }
        if (split[1] === '00' && abs % 1 === 0) {
            return sign + split[0];
        }
        return sign + split.join('.');
    }

    function looksLikeMoneyText(text) {
        var t = (text || '').trim();
        if (!t || /[a-zA-Z\u0600-\u06FF]{3,}/.test(t.replace(/ل\.س/g, '').replace(/[,.\d\s+\-%]/g, ''))) {
            return false;
        }
        var n = parseRaw(t);
        if (!isFinite(n)) {
            return false;
        }
        if (/\.\d{1,3}$/.test(t.replace(/,/g, ''))) {
            return true;
        }
        return Math.abs(n) >= 500;
    }

    function looksLikePlainNumberText(text) {
        var t = (text || '').trim();
        if (!t) {
            return false;
        }
        if (/[a-zA-Z\u0600-\u06FF]{4,}/.test(t.replace(/[%\d,.\s+\-]/g, ''))) {
            return false;
        }
        if (/\d{4}[\/\-]\d{1,2}/.test(t) || /\d{1,2}:\d{2}/.test(t)) {
            return false;
        }
        if (/\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/.test(t)) {
            return false;
        }
        return isFinite(parseRaw(t.replace(/%$/, '')));
    }

    function shouldFormatWithCommas(num, decimals, force) {
        if (!isFinite(num)) {
            return false;
        }
        if (force) {
            return true;
        }
        if (decimals > 0) {
            return Math.abs(num) >= 1;
        }
        return Math.abs(num) >= MIN_COMMA_INTEGER;
    }

    function applyNumberFormat(el, options) {
        options = options || {};
        if (!el || el.dataset.sypFormatted === '1') {
            return;
        }
        if (el.closest('[data-syp-skip-format]')) {
            return;
        }
        if (el.children.length > 0 && !el.hasAttribute('data-syp-new') && !el.hasAttribute('data-fmt-number')) {
            return;
        }

        var rawText = el.hasAttribute('data-fmt-number')
            ? String(el.getAttribute('data-fmt-number'))
            : el.textContent.trim();
        var percentSuffix = '';
        if (/%\s*$/.test(rawText)) {
            percentSuffix = '%';
            rawText = rawText.replace(/%\s*$/, '').trim();
        }

        var rawAttr = el.getAttribute('data-syp-new');
        var num = rawAttr != null ? parseFloat(rawAttr) : parseRaw(rawText);
        if (!isFinite(num)) {
            return;
        }

        var decimals = options.decimals != null ? options.decimals : (percentSuffix ? 1 : 2);
        if (el.hasAttribute('data-fmt-decimals')) {
            decimals = parseInt(el.getAttribute('data-fmt-decimals'), 10);
        }
        if (!shouldFormatWithCommas(num, decimals, options.force)) {
            return;
        }

        var formatted = formatWithCommas(num, decimals) + percentSuffix;
        var cur = el.querySelector('.wallet-currency, .header-wallet-currency, .syp-currency-suffix');
        if (cur) {
            el.textContent = formatWithCommas(num, decimals);
            el.appendChild(cur);
        } else {
            el.textContent = formatted;
        }

        el.dataset.sypFormatted = '1';
        el.dataset.sypFmtKind = options.kind || 'number';
        el.classList.add('syp-fmt-applied');
    }

    function formatDataAttributes() {
        document.querySelectorAll('[data-syp-new]').forEach(function (el) {
            var dec = parseInt(el.getAttribute('data-syp-decimals') || '2', 10);
            applyNumberFormat(el, { decimals: dec, kind: 'money', force: true });
        });
        document.querySelectorAll('[data-fmt-number]').forEach(function (el) {
            var dec = parseInt(el.getAttribute('data-fmt-decimals') || '0', 10);
            applyNumberFormat(el, { decimals: dec, kind: 'count', force: true });
        });
    }

    function formatStatCards() {
        document.querySelectorAll('.financial-stat-card, .wallet-stat-card').forEach(function (card) {
            var labelEl = card.querySelector('.financial-stat-label, .wallet-stat-label');
            var valueEl = card.querySelector('.financial-stat-value, .wallet-stat-value');
            if (!valueEl) {
                return;
            }
            var label = (labelEl && labelEl.textContent) ? labelEl.textContent.trim() : '';
            if (/شبكة|شركة|اسم/i.test(label) && !COUNT_LABEL_RE.test(label) && !MONEY_LABEL_RE.test(label)) {
                return;
            }
            if (COUNT_LABEL_RE.test(label) && !MONEY_LABEL_RE.test(label)) {
                if (looksLikePlainNumberText(valueEl.textContent)) {
                    applyNumberFormat(valueEl, { decimals: 0, kind: 'count' });
                }
                return;
            }
            if (MONEY_LABEL_RE.test(label) || looksLikeMoneyText(valueEl.textContent)) {
                applyNumberFormat(valueEl, { decimals: 2, kind: 'money' });
            } else if (looksLikePlainNumberText(valueEl.textContent)) {
                applyNumberFormat(valueEl, { decimals: 0, kind: 'count' });
            }
        });
    }

    function getTableHeaderText(table, cell) {
        var colIndex = Array.prototype.indexOf.call(cell.parentElement.children, cell);
        var headRow = table.querySelector('thead tr');
        if (!headRow || colIndex < 0) {
            return '';
        }
        var th = headRow.children[colIndex];
        return th ? th.textContent.trim() : '';
    }

    function formatTables() {
        var tableSelectors = [
            '.financial-table',
            '.wallet-tx-table',
            '.employee-wallet-table',
            'table.table-hover',
            '.table'
        ];
        tableSelectors.forEach(function (sel) {
            document.querySelectorAll(sel).forEach(function (table) {
                table.querySelectorAll('tbody td').forEach(function (td) {
                    if (td.dataset.sypFormatted === '1' || td.querySelector('a, button, input, form, select')) {
                        return;
                    }
                    if (td.closest('[data-syp-skip-format]') || td.getAttribute('data-syp-skip-format') === 'true') {
                        return;
                    }
                    var header = getTableHeaderText(table, td);
                    var dataLabel = (td.getAttribute('data-label') || '').trim();
                    if (SKIP_HEADER_RE.test(header) || SKIP_HEADER_RE.test(dataLabel)) {
                        return;
                    }
                    if (MONEY_HEADER_RE.test(header) ||
                        ((td.classList.contains('text-success') || td.classList.contains('text-danger')) &&
                            looksLikeMoneyText(td.textContent))) {
                        if (looksLikeMoneyText(td.textContent) || looksLikePlainNumberText(td.textContent)) {
                            applyNumberFormat(td, { decimals: 2, kind: 'money' });
                        }
                        return;
                    }
                    if (PERCENT_HEADER_RE.test(header) && looksLikePlainNumberText(td.textContent)) {
                        applyNumberFormat(td, { decimals: 1, kind: 'percent', force: true });
                        return;
                    }
                    if (COUNT_HEADER_RE.test(header) && looksLikePlainNumberText(td.textContent)) {
                        applyNumberFormat(td, { decimals: 0, kind: 'count' });
                    }
                });
            });
        });
    }

    function formatHeaderWallet() {
        document.querySelectorAll('.header-wallet-amount').forEach(function (el) {
            applyNumberFormat(el, { decimals: 0, kind: 'money', force: true });
        });
    }

    function shouldSkipNumericFormat(input) {
        return input && (input.getAttribute('data-syp-skip-format') === 'true'
            || input.getAttribute('data-fmt-number') === 'false'
            || !!input.closest('[data-syp-skip-format]'));
    }

    function shouldUpgradeCountInput(input) {
        if (!input || input.dataset.sypUpgraded === '1' || input.type !== 'number') {
            return false;
        }
        if (shouldSkipNumericFormat(input)) {
            return false;
        }
        if (input.classList.contains('syp-amount-display') || input.closest('.syp-amount-field, .fmt-number-field')) {
            return false;
        }
        var name = (input.name || input.id || '').toLowerCase();
        if (!name || INPUT_INCLUDE_RE.test(name)) {
            return false;
        }
        if (INPUT_EXCLUDE_RE.test(name)) {
            return false;
        }
        return COUNT_INPUT_RE.test(name) || (input.getAttribute('step') === '1' && /count|qty|devices|users/i.test(name));
    }

    function upgradeCountInput(input) {
        if (!shouldUpgradeCountInput(input)) {
            return;
        }

        var wrapper = document.createElement('div');
        wrapper.className = 'fmt-number-field mb-0';
        wrapper.setAttribute('data-decimals', '0');
        wrapper.setAttribute('data-min', input.getAttribute('min') || '0');

        var display = document.createElement('input');
        display.type = 'text';
        display.className = 'form-control fmt-number-display ' + (input.className || '').replace('form-control', '').trim();
        display.autocomplete = 'off';
        display.inputMode = 'numeric';
        display.required = input.required;
        if (input.min != null) display.min = input.min;
        if (input.max != null) display.max = input.max;
        if (input.value) {
            var v = parseInt(input.value, 10);
            if (isFinite(v)) {
                display.value = formatWithCommas(v, 0);
            }
        }

        var hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.className = 'fmt-number-value';
        hidden.name = input.name;
        if (input.value) {
            hidden.value = input.value;
        }

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(display);
        wrapper.appendChild(hidden);
        input.remove();
        wrapper.dataset.sypUpgraded = '1';
        initCountField(wrapper);
    }

    function initCountField(root) {
        var display = root.querySelector('.fmt-number-display');
        var hidden = root.querySelector('.fmt-number-value');
        if (!display || !hidden) {
            return;
        }
        var maxDecimals = parseInt(root.getAttribute('data-decimals') || '0', 10);

        function sync() {
            var v = parseRaw(display.value);
            if (isFinite(v)) {
                hidden.value = String(Math.round(v));
            } else {
                hidden.value = '';
            }
        }

        display.addEventListener('input', function () {
            var raw = parseRaw(display.value);
            if (!isFinite(raw)) {
                display.value = display.value.replace(/[^\d,]/g, '');
                sync();
                return;
            }
            display.value = formatWithCommas(Math.round(raw), maxDecimals);
            sync();
        });

        display.addEventListener('blur', function () {
            var raw = parseRaw(display.value);
            if (isFinite(raw)) {
                display.value = formatWithCommas(Math.round(raw), maxDecimals);
            }
            sync();
        });

        var form = root.closest('form');
        if (form) {
            form.addEventListener('submit', function () {
                sync();
            });
        }
        sync();
    }

    function shouldUpgradeInput(input) {
        if (!input || input.dataset.sypUpgraded === '1' || input.type !== 'number') {
            return false;
        }
        if (shouldSkipNumericFormat(input)) {
            return false;
        }
        if (!isMoneyFormattingContext(input)) {
            return false;
        }
        if (input.classList.contains('syp-amount-display') || input.closest('.syp-amount-field')) {
            return false;
        }
        var name = (input.name || input.id || '').toLowerCase();
        if (!name) {
            return false;
        }
        if (INPUT_EXCLUDE_RE.test(name)) {
            return false;
        }
        if (INPUT_INCLUDE_RE.test(name)) {
            return true;
        }
        var step = (input.getAttribute('step') || '').toLowerCase();
        return step === '0.01' || step === 'any' || step === '0.001';
    }

    function upgradeNumberInput(input) {
        if (!shouldUpgradeInput(input)) {
            return;
        }

        var wrapper = document.createElement('div');
        wrapper.className = 'syp-amount-field mb-0';
        wrapper.setAttribute('data-decimals', input.getAttribute('step') === '0.001' ? '3' : '2');
        wrapper.setAttribute('data-min', input.getAttribute('min') || '0.01');

        var display = document.createElement('input');
        display.type = 'text';
        display.className = 'form-control syp-amount-display ' + (input.className || '').replace('form-control', '').trim();
        display.placeholder = input.placeholder || 'مثال: 1,000,000';
        display.required = input.required;
        display.autocomplete = 'off';
        display.inputMode = 'decimal';
        if (input.value) {
            var v = parseFloat(input.value);
            if (isFinite(v)) {
                display.value = formatWithCommas(v, parseInt(wrapper.getAttribute('data-decimals'), 10));
            }
        }

        var hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.className = 'syp-amount-value';
        hidden.name = input.name;
        if (input.value) {
            hidden.value = input.value;
        }

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(display);
        wrapper.appendChild(hidden);
        input.remove();
        wrapper.dataset.sypUpgraded = '1';
        initAmountField(wrapper);
    }

    function initAmountField(root) {
        var display = root.querySelector('.syp-amount-display');
        var hidden = root.querySelector('.syp-amount-value');
        if (!display || !hidden) {
            return;
        }

        var maxDecimals = parseInt(root.getAttribute('data-decimals') || '2', 10);
        var minValue = parseFloat(root.getAttribute('data-min') || '0.01');

        function syncFromDisplay() {
            var v = parseRaw(display.value);
            if (isFinite(v) && v > 0) {
                hidden.value = v.toFixed(maxDecimals);
            } else {
                hidden.value = '';
            }
        }

        display.addEventListener('input', function () {
            var raw = parseRaw(display.value);
            if (!isFinite(raw)) {
                display.value = display.value.replace(/[^\d.,]/g, '');
                syncFromDisplay();
                return;
            }
            display.value = formatWithCommas(raw, maxDecimals);
            syncFromDisplay();
        });

        display.addEventListener('blur', function () {
            var raw = parseRaw(display.value);
            if (isFinite(raw) && raw > 0) {
                display.value = formatWithCommas(raw, maxDecimals);
            }
            syncFromDisplay();
        });

        var form = root.closest('form');
        if (form) {
            form.addEventListener('submit', function (e) {
                if (display.disabled || (hidden && hidden.disabled) || display.closest('.d-none')) {
                    return;
                }
                syncFromDisplay();
                var v = parseRaw(display.value);
                if (display.required && (!isFinite(v) || v < minValue)) {
                    e.preventDefault();
                    display.classList.add('is-invalid');
                    display.focus();
                    return;
                }
                display.classList.remove('is-invalid');
                if (isFinite(v)) {
                    hidden.value = v.toFixed(maxDecimals);
                }
            });
        }

        syncFromDisplay();
    }

    function initAmountFields() {
        document.querySelectorAll('.syp-amount-field').forEach(initAmountField);
        document.querySelectorAll('input[type="number"]').forEach(function (input) {
            if (shouldUpgradeCountInput(input)) {
                upgradeCountInput(input);
            } else {
                upgradeNumberInput(input);
            }
        });
        document.querySelectorAll('.fmt-number-field').forEach(initCountField);
    }

    function debounce(fn, ms) {
        var t;
        return function () {
            clearTimeout(t);
            t = setTimeout(fn, ms);
        };
    }

    function runAll() {
        formatDataAttributes();
        formatStatCards();
        formatTables();
        formatHeaderWallet();
        initAmountFields();
    }

    function observeDynamicContent() {
        var root = document.querySelector('.page-content') || document.body;
        var scheduled = debounce(runAll, 250);
        var obs = new MutationObserver(scheduled);
        obs.observe(root, { childList: true, subtree: true });
        if (window.jQuery && jQuery.fn.dataTable) {
            jQuery(document).on('draw.dt', scheduled);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            runAll();
            observeDynamicContent();
        });
    } else {
        runAll();
        observeDynamicContent();
    }

    window.SypGlobal = {
        MIN_COMMA_INTEGER: MIN_COMMA_INTEGER,
        runAll: runAll,
        parseRaw: parseRaw,
        formatWithCommas: formatWithCommas,
        formatNumber: function (n, d) { return formatWithCommas(n, d != null ? d : 0); },
        applyNumberFormat: applyNumberFormat,
        initAmountField: initAmountField,
        initAmountFields: initAmountFields
    };

    window.SypAmountInput = {
        initAll: initAmountFields,
        initField: initAmountField,
        parseRaw: parseRaw,
        formatWithCommas: formatWithCommas
    };
})();
