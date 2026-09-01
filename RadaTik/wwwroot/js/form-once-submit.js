/**
 * Prevent double-submit on slow POSTs (maintenance requests, invoice pay).
 */
(function () {
    'use strict';

    function isInvalid(form) {
        if (window.jQuery) {
            var $form = window.jQuery(form);
            var validator = $form.data('validator');
            if (validator && !$form.valid()) {
                return true;
            }
        }

        if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
            return true;
        }

        return false;
    }

    function lock(form) {
        if (form.dataset.submitting === '1') {
            return false;
        }

        form.dataset.submitting = '1';
        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (btn) {
            btn.disabled = true;
            btn.classList.add('is-loading');
            var loading = btn.getAttribute('data-loading-text');
            if (loading) {
                btn.textContent = loading;
            }
        });
        return true;
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form || !form.classList || !form.classList.contains('js-once-submit')) {
            return;
        }

        if (isInvalid(form)) {
            return;
        }

        if (!lock(form)) {
            event.preventDefault();
        }
    }, true);
})();
