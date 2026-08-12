(function () {

    'use strict';



    var form = document.getElementById('employeeCreateForm');

    if (!form) return;



    var currentStep = 1;

    var totalSteps = 4;

    var panels = form.querySelectorAll('[data-wizard-step]');

    var stepItems = document.querySelectorAll('.employee-wizard-step-item');

    var btnPrev = document.getElementById('wizardBtnPrev');

    var btnNext = document.getElementById('wizardBtnNext');

    var btnSubmit = document.getElementById('wizardBtnSubmit');

    var permAdvanced = document.getElementById('permAdvancedBlock');

    var permSummaryCount = document.getElementById('permSummaryCount');

    var permSummaryDept = document.getElementById('permSummaryDept');

    var toggleAdvancedBtn = document.getElementById('togglePermAdvanced');

    var validateUrl = form.getAttribute('data-validate-url');

    var step1Panel = form.querySelector('[data-wizard-step="1"]');



    function escapeHtml(text) {

        var div = document.createElement('div');

        div.textContent = text;

        return div.innerHTML;

    }



    function clearStep1Errors() {

        var box = document.getElementById('wizardStep1Errors');

        if (box) {

            box.innerHTML = '';

            box.classList.add('d-none');

        }

        if (step1Panel) {

            step1Panel.querySelectorAll('.is-invalid').forEach(function (el) {

                el.classList.remove('is-invalid');

            });

        }

    }



    function showStep1Errors(data) {

        var box = document.getElementById('wizardStep1Errors');

        if (!box) return;



        var msgs = [];

        if (data.generalErrors && data.generalErrors.length) {

            data.generalErrors.forEach(function (m) { msgs.push(m); });

        }

        if (data.fieldErrors) {

            Object.keys(data.fieldErrors).forEach(function (field) {

                var arr = data.fieldErrors[field];

                if (!arr || !arr.length) return;

                arr.forEach(function (m) {

                    if (msgs.indexOf(m) === -1) msgs.push(m);

                });

                var el = form.querySelector('[name="' + field + '"]');

                if (el) el.classList.add('is-invalid');

            });

        }



        if (!msgs.length) return;



        box.innerHTML = '<ul class="mb-0 ps-3">' + msgs.map(function (m) {

            return '<li>' + escapeHtml(m) + '</li>';

        }).join('') + '</ul>';

        box.classList.remove('d-none');

        try { box.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); } catch (_) {}

    }



    function getSelectedDept() {

        if (typeof window.employeeDeptGetSelected === 'function') {

            return window.employeeDeptGetSelected();

        }

        var hidden = document.getElementById('employeeDepartmentValue');

        return hidden ? parseInt(hidden.value || '0', 10) : 0;

    }



    function countSelectedPerms() {

        var set = new Set();

        form.querySelectorAll('input[name="SelectedPermissionIds"]:checked').forEach(function (i) {

            set.add(i.value);

        });

        return set.size;

    }



    function updatePermSummary() {

        var count = countSelectedPerms();

        if (permSummaryCount) permSummaryCount.textContent = String(count);

        if (permSummaryDept) {

            var dept = getSelectedDept();

            var card = document.querySelector('.employee-dept-card.is-active .employee-dept-card-title');

            permSummaryDept.textContent = card ? card.textContent.trim() : (dept === 99 ? 'تخصيص يدوي' : '—');

        }

        var el = document.getElementById('permSelectedCount');

        if (el) el.textContent = String(count);

    }



    function isCustomDept() {

        return getSelectedDept() === 99;

    }



    function refreshAdvancedVisibility() {

        if (!permAdvanced || !toggleAdvancedBtn) return;

        if (isCustomDept()) {

            permAdvanced.classList.add('is-open');

            toggleAdvancedBtn.style.display = 'none';

        } else {

            toggleAdvancedBtn.style.display = '';

        }

    }



    function validateStep1Html() {

        if (!step1Panel) return true;



        var valid = true;

        step1Panel.querySelectorAll('input, select, textarea').forEach(function (inp) {

            if (inp.disabled) return;

            if (!inp.checkValidity()) {

                valid = false;

                inp.classList.add('is-invalid');

            } else {

                inp.classList.remove('is-invalid');

            }

        });



        if (!valid) {

            var firstInvalid = step1Panel.querySelector(':invalid');

            if (firstInvalid) firstInvalid.reportValidity();

        }



        return valid;

    }



    async function validateStep1Account() {

        clearStep1Errors();

        if (!validateStep1Html()) return false;

        if (!validateUrl) return true;



        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

        var fd = new FormData();

        if (tokenInput) fd.append('__RequestVerificationToken', tokenInput.value);



        ['UserName', 'Email', 'PhoneNumber', 'FullName', 'Password', 'ConfirmPassword', 'IsActive'].forEach(function (name) {

            var el = form.querySelector('[name="' + name + '"]');

            if (!el) return;

            if (el.type === 'checkbox') {

                fd.append(name, el.checked ? 'true' : 'false');

            } else {

                fd.append(name, el.value || '');

            }

        });



        if (btnNext) btnNext.disabled = true;

        try {

            var resp = await fetch(validateUrl, {

                method: 'POST',

                body: fd,

                headers: { 'X-Requested-With': 'XMLHttpRequest' }

            });

            if (!resp.ok) {

                showStep1Errors({ generalErrors: ['تعذر التحقق من بيانات الحساب. حاول مرة أخرى.'] });

                return false;

            }

            var data = await resp.json();

            if (data.isValid) return true;

            showStep1Errors(data);

            return false;

        } catch (_) {

            showStep1Errors({ generalErrors: ['تعذر الاتصال بالخادم للتحقق من البيانات.'] });

            return false;

        } finally {

            if (btnNext) btnNext.disabled = false;

        }

    }



    async function validateStep(step) {

        var panel = form.querySelector('[data-wizard-step="' + step + '"]');

        if (!panel) return true;



        if (step === 1) {

            return await validateStep1Account();

        }



        var inputs = panel.querySelectorAll('input, select, textarea');

        var valid = true;

        inputs.forEach(function (inp) {

            if (inp.disabled) return;

            if (!inp.checkValidity()) {

                valid = false;

                inp.reportValidity();

            }

        });



        if (step === 2) {

            var dept = getSelectedDept();

            if (dept === 0 && countSelectedPerms() === 0) {

                alert('اختر دور الموظف أو انسخ صلاحيات موظف موجود.');

                return false;

            }

        }



        if (step === 3 && countSelectedPerms() === 0 && getSelectedDept() !== 99) {

            alert('لم تُحدَّد أي صلاحيات. اختر دوراً آخر أو فعّل التخصيص المتقدم.');

            return false;

        }



        return valid;

    }



    function goToStep(step) {

        currentStep = Math.max(1, Math.min(totalSteps, step));

        panels.forEach(function (p) {

            p.classList.toggle('is-active', parseInt(p.getAttribute('data-wizard-step'), 10) === currentStep);

        });

        stepItems.forEach(function (item) {

            var n = parseInt(item.getAttribute('data-step'), 10);

            item.classList.toggle('is-active', n === currentStep);

            item.classList.toggle('is-done', n < currentStep);

        });



        if (btnPrev) btnPrev.style.visibility = currentStep === 1 ? 'hidden' : 'visible';

        if (btnNext) btnNext.style.display = currentStep === totalSteps ? 'none' : '';

        if (btnSubmit) btnSubmit.style.display = currentStep === totalSteps ? '' : 'none';



        if (currentStep === 3) {

            updatePermSummary();

            refreshAdvancedVisibility();

        }

        if (currentStep === 4) {

            buildReview();

        }



        var firstInput = form.querySelector('[data-wizard-step="' + currentStep + '"] input:not([type="hidden"]):not([disabled])');

        if (firstInput) {

            try { firstInput.focus(); } catch (_) {}

        }

    }



    function buildReview() {

        var setUsername = document.getElementById('reviewUserName');

        var setFullName = document.getElementById('reviewFullName');

        var setEmail = document.getElementById('reviewEmail');

        var setDept = document.getElementById('reviewDepartment');

        var setPerms = document.getElementById('reviewPermissions');

        var setPayroll = document.getElementById('reviewPayroll');



        var userName = form.querySelector('[name="UserName"]');

        var fullName = form.querySelector('[name="FullName"]');

        var email = form.querySelector('[name="Email"]');

        var syncPayroll = document.getElementById('syncToPayroll');



        if (setUsername && userName) setUsername.textContent = userName.value.trim() || '—';

        if (setFullName && fullName) setFullName.textContent = fullName.value.trim() || '—';

        if (setEmail && email) setEmail.textContent = email.value.trim() || '—';

        if (setDept) {

            var card = document.querySelector('.employee-dept-card.is-active .employee-dept-card-title');

            setDept.textContent = card ? card.textContent.trim() : '—';

        }

        if (setPerms) setPerms.textContent = countSelectedPerms() + ' صلاحية';

        if (setPayroll && syncPayroll) {

            setPayroll.textContent = syncPayroll.checked ? 'نعم — مرتبط بالرواتب' : 'لا';

        }

    }



    if (btnPrev) {

        btnPrev.addEventListener('click', function (e) {

            e.preventDefault();

            goToStep(currentStep - 1);

        });

    }



    if (btnNext) {

        btnNext.addEventListener('click', async function (e) {

            e.preventDefault();

            var ok = await validateStep(currentStep);

            if (!ok) return;

            goToStep(currentStep + 1);

        });

    }



    if (step1Panel) {

        step1Panel.addEventListener('input', function (e) {

            if (e.target && e.target.classList) {

                e.target.classList.remove('is-invalid');

            }

        });

    }



    if (toggleAdvancedBtn && permAdvanced) {

        toggleAdvancedBtn.addEventListener('click', function (e) {

            e.preventDefault();

            permAdvanced.classList.toggle('is-open');

            toggleAdvancedBtn.textContent = permAdvanced.classList.contains('is-open')

                ? 'إخفاء التخصيص المتقدم'

                : 'تخصيص متقدم للصلاحيات';

        });

    }



    document.addEventListener('employeePermChanged', updatePermSummary);

    document.addEventListener('employeeDeptChanged', function () {

        updatePermSummary();

        refreshAdvancedVisibility();

    });



    form.addEventListener('submit', async function (e) {

        if (currentStep !== totalSteps) {

            e.preventDefault();

            var ok = await validateStep(currentStep);

            if (ok) goToStep(totalSteps);

            return;

        }

        if (getSelectedDept() === 0 && countSelectedPerms() === 0) {

            e.preventDefault();

            alert('اختر دور الموظف أو حدّد الصلاحيات.');

            goToStep(2);

        }

    });



    var initialStep = parseInt(form.getAttribute('data-wizard-initial-step') || '1', 10);

    if (isNaN(initialStep) || initialStep < 1) initialStep = 1;

    if (initialStep > totalSteps) initialStep = totalSteps;



    goToStep(initialStep);

    updatePermSummary();

})();


