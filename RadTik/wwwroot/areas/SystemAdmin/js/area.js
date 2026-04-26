/**
 * SystemAdmin Area JS
 * Place SystemAdmin-only behaviors here.
 */
(function () {
    'use strict';
    /*
      Reusable critical confirm pattern (bulk destructive actions):
      - Add `data-critical-confirm-keyword="..."` on <form>
      - Add one input with `data-critical-confirm-input`
      - Add submit button with `data-critical-confirm-submit`
      Optional:
      - Any element with `data-critical-confirm-keyword-text` will receive the keyword text automatically.
    */

    function normalize(value) {
        return (value || '').toString().trim();
    }

    function setupCriticalConfirm(form) {
        var keyword = normalize(form.getAttribute('data-critical-confirm-keyword'));
        if (!keyword) return;

        var input = form.querySelector('[data-critical-confirm-input]');
        var submitBtn = form.querySelector('[data-critical-confirm-submit]');
        var hint = form.querySelector('[data-critical-confirm-hint]');
        var keywordTargets = form.querySelectorAll('[data-critical-confirm-keyword-text]');

        for (var i = 0; i < keywordTargets.length; i++) {
            keywordTargets[i].textContent = keyword;
        }

        if (!input || !submitBtn) return;

        function syncState() {
            var isValid = normalize(input.value) === keyword;
            submitBtn.disabled = !isValid;
            form.classList.toggle('is-confirm-valid', isValid);
            form.classList.toggle('is-confirm-invalid', !isValid && normalize(input.value).length > 0);

            if (hint) {
                hint.classList.toggle('text-success', isValid);
                hint.classList.toggle('text-danger', !isValid && normalize(input.value).length > 0);
            }
        }

        input.addEventListener('input', syncState);
        form.addEventListener('reset', function () {
            window.requestAnimationFrame(syncState);
        });

        var modal = form.closest('.modal');
        if (modal) {
            modal.addEventListener('hidden.bs.modal', function () {
                form.reset();
                syncState();
            });
        }

        form.addEventListener('submit', function (e) {
            if (submitBtn.disabled) {
                e.preventDefault();
                input.focus();
            }
        });

        syncState();
    }

    function initCriticalConfirms() {
        var forms = document.querySelectorAll('form[data-critical-confirm-keyword]');
        for (var i = 0; i < forms.length; i++) {
            setupCriticalConfirm(forms[i]);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        initCriticalConfirms();
    });
})();

