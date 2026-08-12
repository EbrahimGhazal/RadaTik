(function (global) {
    'use strict';

    var coordApplyTimer = null;
    var coordStatusTimer = null;
    var suppressCoordInputSync = false;
    var initialized = false;
    var applyLocationCallback = null;
    var mapNotReadyMessage = 'الخريطة غير جاهزة — انتظر لحظة ثم أعد المحاولة';

    function isValidCoordinate(lat, lng) {
        return isFinite(lat) && isFinite(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
    }

    function parseCoordinateText(raw) {
        if (!raw) return null;
        var text = String(raw).trim();
        if (!text) return null;

        var googleMatch = text.match(/@(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?)/);
        if (googleMatch) {
            var gLat = parseFloat(googleMatch[1]);
            var gLng = parseFloat(googleMatch[2]);
            if (isValidCoordinate(gLat, gLng)) return { lat: gLat, lng: gLng };
        }

        var qMatch = text.match(/[?&]q=(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?)/i);
        if (qMatch) {
            var qLat = parseFloat(qMatch[1]);
            var qLng = parseFloat(qMatch[2]);
            if (isValidCoordinate(qLat, qLng)) return { lat: qLat, lng: qLng };
        }

        var cleaned = text.replace(/[،]/g, ',').replace(/\s+/g, ' ').trim();
        var parts = cleaned.split(/[,;\s|]+/).filter(Boolean);
        if (parts.length >= 2) {
            var lat = parseFloat(parts[0]);
            var lng = parseFloat(parts[1]);
            if (isValidCoordinate(lat, lng)) return { lat: lat, lng: lng };
        }

        return null;
    }

    function showCoordStatus(message, type) {
        var el = document.getElementById('coordsStatus');
        if (!el) return;
        el.textContent = message || '';
        el.className = 'map-coords-status small' + (type ? ' is-' + type : '');
        if (coordStatusTimer) clearTimeout(coordStatusTimer);
        if (message) {
            coordStatusTimer = setTimeout(function () {
                el.textContent = '';
                el.className = 'map-coords-status small';
            }, 4000);
        }
    }

    function copyTextToClipboard(text, successMessage) {
        if (!text) return;
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () {
                showCoordStatus(successMessage || 'تم النسخ', 'success');
            }).catch(function () {
                showCoordStatus('تعذر النسخ', 'error');
            });
            return;
        }
        var temp = document.createElement('textarea');
        temp.value = text;
        document.body.appendChild(temp);
        temp.select();
        try {
            document.execCommand('copy');
            showCoordStatus(successMessage || 'تم النسخ', 'success');
        } catch (e) {
            showCoordStatus('تعذر النسخ', 'error');
        }
        document.body.removeChild(temp);
    }

    function setCoordMode(mode) {
        document.querySelectorAll('.map-coords-mode-btn').forEach(function (btn) {
            btn.classList.toggle('active', btn.getAttribute('data-coord-mode') === mode);
        });
        var pasteRow = document.querySelector('.map-coords-paste-row');
        if (pasteRow) pasteRow.classList.toggle('is-highlight', mode === 'manual');
    }

    function setInputValues(lat, lng, options) {
        options = options || {};
        var latInput = document.getElementById('latitudeInput');
        var lngInput = document.getElementById('longitudeInput');
        var pasteInput = document.getElementById('coordsPasteInput');

        suppressCoordInputSync = true;
        if (latInput) latInput.value = lat.toFixed(6);
        if (lngInput) lngInput.value = lng.toFixed(6);
        if (pasteInput && options.updatePaste !== false) {
            pasteInput.value = lat.toFixed(6) + ', ' + lng.toFixed(6);
        }
        suppressCoordInputSync = false;
    }

    function buildStatusMessage(meta) {
        if (!meta || meta.showStatus === false) return null;
        var source = meta.source === 'map' ? 'من الخريطة' : (meta.source === 'paste' ? 'من اللصق' : 'يدوياً');
        return 'تم تحديد الموقع ' + source;
    }

    function applyCoordinates(lat, lng, options) {
        options = options || {};
        if (!isValidCoordinate(lat, lng)) return false;

        setInputValues(lat, lng, options);

        if (typeof applyLocationCallback !== 'function') return false;

        var applied = applyLocationCallback(lat, lng, options);
        if (applied === false) {
            if (options.showStatus !== false) {
                showCoordStatus(mapNotReadyMessage, 'error');
            }
            return false;
        }

        var statusMessage = buildStatusMessage(options);
        if (statusMessage) showCoordStatus(statusMessage, 'success');
        return true;
    }

    function applyManualCoordinates(options) {
        options = options || {};
        var lat = parseFloat(document.getElementById('latitudeInput').value);
        var lng = parseFloat(document.getElementById('longitudeInput').value);
        if (!isValidCoordinate(lat, lng)) {
            showCoordStatus('أدخل إحداثيات صحيحة (عرض: -90→90، طول: -180→180)', 'error');
            return false;
        }
        return applyCoordinates(lat, lng, {
            source: options.source || 'manual',
            fly: options.fly !== false,
            showStatus: options.showStatus !== false,
            updatePaste: options.updatePaste !== false
        });
    }

    function applyPastedCoordinates() {
        var pasteInput = document.getElementById('coordsPasteInput');
        var parsed = parseCoordinateText(pasteInput ? pasteInput.value : '');
        if (!parsed) {
            showCoordStatus('صيغة غير مفهومة — استخدم: 35.52, 35.78', 'error');
            return false;
        }
        setCoordMode('manual');
        return applyCoordinates(parsed.lat, parsed.lng, {
            source: 'paste',
            fly: true,
            showStatus: true,
            updatePaste: true
        });
    }

    function queueManualCoordinateApply() {
        if (suppressCoordInputSync) return;
        if (coordApplyTimer) clearTimeout(coordApplyTimer);
        coordApplyTimer = setTimeout(function () {
            applyManualCoordinates({ source: 'manual', fly: true, showStatus: false });
        }, 500);
    }

    function wireControls() {
        if (initialized) return;
        initialized = true;

        document.querySelectorAll('.map-coords-mode-btn').forEach(function (btn) {
            btn.addEventListener('click', function () {
                setCoordMode(this.getAttribute('data-coord-mode'));
            });
        });

        document.querySelectorAll('.btn-copy-coord').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var input = document.getElementById(this.getAttribute('data-copy'));
                if (!input || !input.value) return;
                copyTextToClipboard(input.value, 'تم نسخ الإحداثية');
            });
        });

        var copyPairBtn = document.getElementById('btnCopyCoordsPair');
        if (copyPairBtn) {
            copyPairBtn.addEventListener('click', function () {
                var lat = document.getElementById('latitudeInput').value;
                var lng = document.getElementById('longitudeInput').value;
                if (!lat || !lng) {
                    showCoordStatus('لا توجد إحداثيات للنسخ', 'error');
                    return;
                }
                copyTextToClipboard(lat + ', ' + lng, 'تم نسخ الإحداثيات');
            });
        }

        var applyManualBtn = document.getElementById('btnApplyManualCoords');
        if (applyManualBtn) {
            applyManualBtn.addEventListener('click', function () {
                setCoordMode('manual');
                applyManualCoordinates({ source: 'manual', fly: true, showStatus: true });
            });
        }

        var applyPasteBtn = document.getElementById('btnApplyCoordsPaste');
        if (applyPasteBtn) applyPasteBtn.addEventListener('click', applyPastedCoordinates);

        var pasteFromClipboardBtn = document.getElementById('btnPasteFromClipboard');
        if (pasteFromClipboardBtn) {
            pasteFromClipboardBtn.addEventListener('click', function () {
                if (!navigator.clipboard || !navigator.clipboard.readText) {
                    showCoordStatus('اللصق من الحافظة غير مدعوم في هذا المتصفح', 'error');
                    return;
                }
                navigator.clipboard.readText().then(function (text) {
                    var pasteInput = document.getElementById('coordsPasteInput');
                    if (pasteInput) pasteInput.value = text.trim();
                    setCoordMode('manual');
                    applyPastedCoordinates();
                }).catch(function () {
                    showCoordStatus('تعذر قراءة الحافظة — الصق يدوياً', 'error');
                });
            });
        }

        var pasteInput = document.getElementById('coordsPasteInput');
        if (pasteInput) {
            pasteInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    applyPastedCoordinates();
                }
            });
            pasteInput.addEventListener('paste', function () {
                setCoordMode('manual');
                setTimeout(applyPastedCoordinates, 0);
            });
        }

        ['latitudeInput', 'longitudeInput'].forEach(function (id) {
            var input = document.getElementById(id);
            if (!input) return;
            input.addEventListener('input', queueManualCoordinateApply);
            input.addEventListener('blur', function () {
                if (coordApplyTimer) clearTimeout(coordApplyTimer);
                applyManualCoordinates({ source: 'manual', fly: true, showStatus: false });
            });
        });
    }

    function init(options) {
        options = options || {};
        applyLocationCallback = options.applyLocation || null;
        if (options.mapNotReadyMessage) mapNotReadyMessage = options.mapNotReadyMessage;
        wireControls();
        return {
            applyCoordinates: applyCoordinates,
            applyManualCoordinates: applyManualCoordinates,
            applyPastedCoordinates: applyPastedCoordinates,
            notifyMapSelection: function (lat, lng, opts) {
                setCoordMode('map');
                return applyCoordinates(lat, lng, Object.assign({
                    source: 'map',
                    fly: false,
                    showStatus: true
                }, opts || {}));
            },
            setCoordMode: setCoordMode,
            setInputValues: setInputValues,
            showStatus: showCoordStatus,
            isValidCoordinate: isValidCoordinate,
            parseCoordinateText: parseCoordinateText
        };
    }

    global.MapCoordinatesPanel = {
        init: init,
        isValidCoordinate: isValidCoordinate,
        parseCoordinateText: parseCoordinateText
    };
})(window);
