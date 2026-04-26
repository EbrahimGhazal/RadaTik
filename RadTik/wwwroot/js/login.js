/**
 * RadTik - Login Page Scripts
 * سكريبتات صفحة تسجيل الدخول
 */

(function() {
    'use strict';

    const LoginPage = {
        init: function() {
            this.setupFormValidation();
            this.setupInputFocus();
            this.setupPasswordToggle();
            this.setupCapsLockHint();
            this.setupSubmitLoadingState();
            this.setupAutofocus();
        },

        setupFormValidation: function() {
            // NOTE: TagHelper attributes (asp-*) are not present in rendered HTML.
            // Use a stable id on the form instead.
            const form = document.getElementById('loginForm');
            if (!form) return;

            const inputs = form.querySelectorAll('input');
            inputs.forEach(input => {
                input.addEventListener('blur', () => {
                    this.validateInput(input);
                });

                input.addEventListener('input', () => {
                    if (input.classList.contains('is-invalid')) {
                        this.validateInput(input);
                    }
                });
            });

            form.addEventListener('submit', (e) => {
                let isValid = true;
                inputs.forEach(input => {
                    if (!this.validateInput(input)) {
                        isValid = false;
                    }
                });

                if (!isValid) {
                    e.preventDefault();
                }
            });
        },

        validateInput: function(input) {
            const value = input.value.trim();
            const type = input.type;
            let isValid = true;
            let errorMessage = '';

            // Remove previous validation classes
            input.classList.remove('is-invalid', 'is-valid');
            const errorElement = input.parentElement.querySelector('.validation-error');
            if (errorElement) {
                errorElement.remove();
            }

            // Validation rules
            if (input.hasAttribute('required') && !value) {
                isValid = false;
                errorMessage = 'هذا الحقل مطلوب';
            } else if (type === 'email' && value && !this.isValidEmail(value)) {
                isValid = false;
                errorMessage = 'البريد الإلكتروني غير صحيح';
            } else if (type === 'password' && value && value.length < 6) {
                isValid = false;
                errorMessage = 'كلمة المرور يجب أن تكون 6 أحرف على الأقل';
            }

            // Apply validation classes
            if (input.hasAttribute('required') || value) {
                input.classList.add(isValid ? 'is-valid' : 'is-invalid');
            }

            // Show error message
            if (!isValid && errorMessage) {
                const errorDiv = document.createElement('div');
                errorDiv.className = 'validation-error';
                errorDiv.textContent = errorMessage;
                input.parentElement.appendChild(errorDiv);
            }

            return isValid;
        },

        isValidEmail: function(email) {
            const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            return re.test(email);
        },

        setupInputFocus: function() {
            const inputs = document.querySelectorAll('.form-control');
            inputs.forEach(input => {
                input.addEventListener('focus', function() {
                    this.parentElement.classList.add('focused');
                });

                input.addEventListener('blur', function() {
                    this.parentElement.classList.remove('focused');
                });
            });
        },

        setupPasswordToggle: function() {
            const toggleBtn = document.getElementById('passwordToggle');
            const input = document.getElementById('passwordInput');
            const icon = document.getElementById('passwordToggleIcon');
            if (!toggleBtn || !input || !icon) return;

            const setState = (visible) => {
                input.type = visible ? 'text' : 'password';
                icon.className = visible ? 'fas fa-eye-slash' : 'fas fa-eye';
                toggleBtn.setAttribute('aria-label', visible ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور');
                toggleBtn.title = visible ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور';
            };

            let visible = false;
            toggleBtn.addEventListener('click', () => {
                visible = !visible;
                setState(visible);
                input.focus();
            });
        },

        setupCapsLockHint: function() {
            const input = document.getElementById('passwordInput');
            const hint = document.getElementById('capsLockHint');
            if (!input || !hint) return;

            const update = (e) => {
                const caps = e && typeof e.getModifierState === 'function' ? e.getModifierState('CapsLock') : false;
                hint.style.display = caps ? 'flex' : 'none';
            };

            input.addEventListener('keydown', update);
            input.addEventListener('keyup', update);
            input.addEventListener('blur', () => (hint.style.display = 'none'));
        },

        setupSubmitLoadingState: function() {
            const form = document.getElementById('loginForm');
            const btn = document.getElementById('loginSubmitBtn');
            if (!form || !btn) return;

            form.addEventListener('submit', () => {
                // Let server-side validation handle correctness, but prevent double submits.
                btn.disabled = true;
                btn.classList.add('is-loading');
            });
        },

        setupAutofocus: function() {
            const user = document.querySelector('#loginForm input[name="UserName"], #loginForm input#UserName, #loginForm input[type="text"]');
            if (user) {
                try { user.focus(); } catch (_) {}
            }
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => LoginPage.init());
    } else {
        LoginPage.init();
    }

    // Export for global access
    window.LoginPage = LoginPage;
})();
