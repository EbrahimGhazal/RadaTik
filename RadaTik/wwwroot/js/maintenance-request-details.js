/**
 * تفاصيل طلب الصيانة — تحقق الإرسال، إجمالي التسعير الحي، وخريطة موقع الزيارة.
 */
(function () {
    'use strict';

    window.validateRejection = function (form) {
        var reasonInput = form.querySelector('textarea[name="rejectionReason"]');
        var reason = reasonInput ? reasonInput.value : '';
        if (!reason.trim()) {
            alert('يجب تحديد سبب الرفض');
            return false;
        }
        return confirm('هل أنت متأكد من رفض هذا الطلب؟');
    };

    window.validateCompletion = function (form) {
        var checks = form.querySelectorAll('input[name="selectedMaintenanceTypes"]:checked');
        var validationNode = document.getElementById('selectionValidationMessage');
        if (checks.length === 0) {
            if (validationNode) {
                validationNode.style.display = '';
            }
            return false;
        }
        if (validationNode) {
            validationNode.style.display = 'none';
        }
        return true;
    };

    function updateSelectedServicesLiveTotal() {
        var checks = document.querySelectorAll('input[name="selectedMaintenanceTypes"]:checked');
        var totalNode = document.getElementById('liveSelectedServiceTotal');
        var countNode = document.getElementById('liveSelectedServiceCount');
        var subtotalNode = document.getElementById('liveSubtotalEstimate');
        var commissionNode = document.getElementById('liveCommissionEstimate');
        var grossNode = document.getElementById('liveGrossEstimate');
        var netNode = document.getElementById('liveNetEstimate');
        var transportNode = document.getElementById('liveTransportFee');
        var transportInput = document.getElementById('transportFeeOverrideInput');
        var submitBtn = document.getElementById('completeMaintenanceSubmitBtn');
        var validationNode = document.getElementById('selectionValidationMessage');
        if (!totalNode) return;

        var total = 0;
        checks.forEach(function (c) {
            var v = Number(c.dataset.price || 0);
            if (!Number.isNaN(v)) total += v;
        });

        var formatSyp = function (n) {
            return n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
        };
        var ceilSyp = function (n) { return Math.ceil(n); };

        totalNode.textContent = formatSyp(total) + ' ل.س';
        if (countNode) {
            countNode.textContent = checks.length.toString();
        }
        var transportValue = transportInput
            ? Number(transportInput.value || 0)
            : Number((subtotalNode && subtotalNode.dataset.transport) || 0);
        var safeTransport = Number.isNaN(transportValue) || transportValue < 0 ? 0 : transportValue;
        if (transportNode) {
            transportNode.textContent = formatSyp(safeTransport) + ' ل.س';
        }
        if (submitBtn) {
            submitBtn.disabled = checks.length === 0;
        }
        if (validationNode && checks.length > 0) {
            validationNode.style.display = 'none';
        }
        if (subtotalNode) {
            var subtotal = total + safeTransport;
            subtotalNode.textContent = formatSyp(subtotal) + ' ل.س';

            if (commissionNode) {
                var mode = Number(commissionNode.dataset.commissionMode || 2);
                var value = Number(commissionNode.dataset.commissionValue || 0);
                var commission = mode === 1
                    ? ceilSyp(subtotal * (value / 100))
                    : ceilSyp(value);
                if (Number.isNaN(commission) || commission < 0) commission = 0;
                commissionNode.textContent = formatSyp(commission) + ' ل.س';

                if (grossNode) {
                    grossNode.textContent = formatSyp(Math.max(0, subtotal + commission)) + ' ل.س';
                }
                if (netNode) {
                    netNode.textContent = formatSyp(Math.max(0, subtotal)) + ' ل.س';
                }
            }
        }
    }

    function initVisitMap() {
        var el = document.getElementById('maintVisitMap');
        if (!el || typeof window.L === 'undefined') return;
        var lat = Number(el.getAttribute('data-lat'));
        var lng = Number(el.getAttribute('data-lng'));
        if (Number.isNaN(lat) || Number.isNaN(lng)) return;

        var map = window.L.map(el, {
            zoomControl: true,
            attributionControl: false,
            scrollWheelZoom: false
        }).setView([lat, lng], 16);

        window.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19
        }).addTo(map);
        window.L.marker([lat, lng]).addTo(map);
        setTimeout(function () { map.invalidateSize(); }, 250);
    }

    document.addEventListener('change', function (e) {
        if (e.target && (e.target.matches('input[name="selectedMaintenanceTypes"]') || e.target.matches('#transportFeeOverrideInput'))) {
            updateSelectedServicesLiveTotal();
        }
    });
    document.addEventListener('input', function (e) {
        if (e.target && e.target.matches('#transportFeeOverrideInput')) {
            updateSelectedServicesLiveTotal();
        }
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            updateSelectedServicesLiveTotal();
            initVisitMap();
        });
    } else {
        updateSelectedServicesLiveTotal();
        initVisitMap();
    }
})();
