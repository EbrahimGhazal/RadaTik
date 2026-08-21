/**
 * حقول مبلغ بالليرة السورية — فواصل آلاف.
 */
(function () {
    'use strict';

    function parseRaw(str) {
        if (str == null || str === '') {
            return NaN;
        }
        var cleaned = String(str).replace(/,/g, '').replace(/[^\d.]/g, '');
        if (!cleaned || cleaned === '.') {
            return NaN;
        }
        var parts = cleaned.split('.');
        if (parts.length > 2) {
            cleaned = parts[0] + '.' + parts.slice(1).join('');
        }
        return parseFloat(cleaned);
    }

    function formatWithCommas(num, maxDecimals) {
        if (!isFinite(num) || num < 0) {
            return '';
        }
        var fixed = num.toFixed(maxDecimals);
        var split = fixed.split('.');
        split[0] = split[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        if (maxDecimals === 0) {
            return split[0];
        }
        return split[1] === '00' && num % 1 === 0 ? split[0] : split.join('.');
    }

    function initField(root) {
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
                syncFromDisplay();
                var v = parseRaw(display.value);
                if (!isFinite(v) || v < minValue) {
                    e.preventDefault();
                    display.classList.add('is-invalid');
                    display.focus();
                    return;
                }
                display.classList.remove('is-invalid');
                hidden.value = v.toFixed(maxDecimals);
            });
        }
    }

    function initAll() {
        document.querySelectorAll('.syp-amount-field').forEach(initField);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }

    window.SypAmountInput = {
        initAll: initAll,
        initField: initField,
        parseRaw: parseRaw,
        formatWithCommas: formatWithCommas
    };
})();
