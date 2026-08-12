(function () {
    'use strict';

    var BLOCKED = [
        'admin@123',
        '123456',
        'password',
        'Password123!',
        'Admin@123456'
    ];

    function $(id) {
        return id ? document.getElementById(id) : null;
    }

    function hasSpecialChar(value) {
        return /[^a-zA-Z0-9]/.test(value);
    }

    function evaluateRules(password, userName, email, minLength) {
        var localPart = '';
        if (email && email.indexOf('@') > 0) {
            localPart = email.split('@')[0];
        }

        var blocked = BLOCKED.some(function (p) {
            return p.toLowerCase() === (password || '').toLowerCase();
        });

        var containsUser = userName && userName.length >= 2 &&
            password.toLowerCase().indexOf(userName.toLowerCase()) >= 0;

        var containsEmail = localPart.length >= 3 &&
            password.toLowerCase().indexOf(localPart.toLowerCase()) >= 0;

        return {
            length: password.length >= minLength,
            upper: /[A-Z]/.test(password),
            lower: /[a-z]/.test(password),
            digit: /\d/.test(password),
            special: hasSpecialChar(password),
            personal: password.length > 0 && !blocked && !containsUser && !containsEmail
        };
    }

    function strengthMeta(rules, hasInput) {
        if (!hasInput) {
            return { pct: 0, label: '—', level: '' };
        }

        var keys = ['length', 'upper', 'lower', 'digit', 'special', 'personal'];
        var passed = keys.filter(function (k) { return rules[k]; }).length;
        var pct = Math.round((passed / keys.length) * 100);

        if (passed <= 2) return { pct: pct, label: 'ضعيفة', level: 'is-weak' };
        if (passed <= 4) return { pct: pct, label: 'متوسطة', level: 'is-fair' };
        if (passed <= 5) return { pct: pct, label: 'جيدة', level: 'is-good' };
        return { pct: 100, label: 'قوية', level: 'is-strong' };
    }

    function setRuleState(item, state) {
        item.classList.remove('is-pass', 'is-fail', 'is-neutral');
        if (state) item.classList.add(state);

        var icon = item.querySelector('.sp-rule-icon i');
        if (!icon) return;

        if (state === 'is-pass') {
            icon.className = 'fas fa-check-circle';
        } else if (state === 'is-fail') {
            icon.className = 'fas fa-times-circle';
        } else {
            icon.className = 'fas fa-circle';
        }
    }

    function bindPanel(panel) {
        var mode = panel.getAttribute('data-sp-rules-mode') || 'strong';
        var passwordId = panel.getAttribute('data-sp-password-id');
        var userNameId = panel.getAttribute('data-sp-username-id');
        var emailId = panel.getAttribute('data-sp-email-id');
        var minLength = parseInt(panel.getAttribute('data-sp-min-length') || '8', 10);
        var isClientMode = mode === 'client';

        var passwordInput = $(passwordId);
        if (!passwordInput) return;

        var userNameInput = userNameId ? $(userNameId) : null;
        var emailInput = emailId ? $(emailId) : null;
        var items = panel.querySelectorAll('[data-sp-rule]');
        var strengthWrap = panel.querySelector('.sp-rules-strength');
        var strengthLabel = panel.querySelector('.sp-rules-strength-label');
        var strengthBar = panel.querySelector('.sp-rules-strength-track');
        var strengthFill = panel.querySelector('.sp-rules-strength-fill');

        function refresh() {
            var password = passwordInput.value || '';
            var hasInput = password.length > 0;
            var rules = isClientMode
                ? { length: password.length >= minLength }
                : evaluateRules(
                    password,
                    userNameInput ? userNameInput.value.trim() : '',
                    emailInput ? emailInput.value.trim() : '',
                    minLength
                );

            items.forEach(function (item) {
                var key = item.getAttribute('data-sp-rule');
                if (!key || !(key in rules)) return;

                if (item.classList.contains('sp-rule-item--info')) return;

                if (!hasInput) {
                    setRuleState(item, 'is-neutral');
                    return;
                }

                setRuleState(item, rules[key] ? 'is-pass' : 'is-fail');
            });

            if (strengthWrap && strengthLabel && strengthFill) {
                var meta = isClientMode
                    ? (function () {
                        if (!hasInput) return { pct: 0, label: '—', level: '' };
                        if (rules.length) return { pct: 100, label: 'مقبولة', level: 'is-strong' };
                        return { pct: 35, label: 'قصيرة', level: 'is-weak' };
                    })()
                    : strengthMeta(rules, hasInput);
                strengthWrap.classList.remove('is-weak', 'is-fair', 'is-good', 'is-strong');
                if (meta.level) strengthWrap.classList.add(meta.level);
                strengthLabel.textContent = hasInput ? meta.label : '—';
                strengthFill.style.width = meta.pct + '%';
                if (strengthBar) {
                    strengthBar.setAttribute('aria-valuenow', String(meta.pct));
                }
            }
        }

        passwordInput.addEventListener('input', refresh);
        if (userNameInput) userNameInput.addEventListener('input', refresh);
        if (emailInput) emailInput.addEventListener('input', refresh);
        refresh();
    }

    function bindPasswordToggles(root) {
        (root || document).querySelectorAll('[data-sp-toggle-password]').forEach(function (btn) {
            if (btn.dataset.spBound) return;
            btn.dataset.spBound = '1';

            btn.addEventListener('click', function () {
                var targetId = btn.getAttribute('data-sp-toggle-password');
                var input = $(targetId);
                if (!input) return;

                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                btn.setAttribute('aria-pressed', show ? 'true' : 'false');
                btn.setAttribute('aria-label', show ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور');

                var icon = btn.querySelector('i');
                if (icon) {
                    icon.className = show ? 'fas fa-eye-slash' : 'fas fa-eye';
                }
            });
        });
    }

    function init(root) {
        (root || document).querySelectorAll('[data-sp-rules-panel]').forEach(bindPanel);
        bindPasswordToggles(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { init(); });
    } else {
        init();
    }

    window.StrongPasswordRulesUi = { init: init, evaluateRules: evaluateRules };
})();
