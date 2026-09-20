(function () {
  'use strict';

  var root = document.getElementById('calField');
  if (!root || typeof signalR === 'undefined') return;

  var boot = {};
  try { boot = JSON.parse(root.getAttribute('data-boot') || '{}'); } catch (e) { boot = {}; }

  var code = boot.code;
  var role = boot.role || 'rx';
  var connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/calibration')
    .withAutomaticReconnect()
    .build();

  function $(id) { return document.getElementById(id); }
  function setText(id, text) {
    var el = $(id);
    if (el) el.textContent = text;
  }
  function fmt(v, digits) {
    if (v == null || !isFinite(Number(v))) return '—';
    return Number(v).toFixed(digits == null ? 1 : digits);
  }
  function signedDelta(target, live) {
    var d = ((live - target + 540) % 360) - 180;
    return d;
  }

  function applySteer(live) {
    var azEl = $('cfSteerAz');
    var elEl = $('cfSteerEl');
    if (!azEl || !elEl) return;
    if (!live || !live.poseValid || live.azimuth == null) {
      azEl.textContent = 'فعّل الحساس';
      elEl.textContent = '—';
      azEl.className = 'calibrate-steer-az';
      elEl.className = 'calibrate-steer-el';
      return;
    }
    var daz = live.azimuthDelta != null ? live.azimuthDelta : signedDelta(boot.targetAz, live.azimuth);
    var del = live.elevationDelta != null ? live.elevationDelta : ((live.elevation || 0) - boot.targetEl);
    if (Math.abs(daz) <= 8) {
      azEl.textContent = 'السمت صحيح';
      azEl.className = 'calibrate-steer-az is-ok';
    } else if (daz > 0) {
      azEl.textContent = '← يسار ' + Math.abs(daz).toFixed(0) + '°';
      azEl.className = 'calibrate-steer-az is-left';
    } else {
      azEl.textContent = 'يمين ' + Math.abs(daz).toFixed(0) + '° →';
      azEl.className = 'calibrate-steer-az is-right';
    }
    if (Math.abs(del) <= 3) {
      elEl.textContent = 'الميل صحيح';
      elEl.className = 'calibrate-steer-el is-ok';
    } else if (del > 0) {
      elEl.textContent = '↓ انزل ' + Math.abs(del).toFixed(1) + '°';
      elEl.className = 'calibrate-steer-el is-down';
    } else {
      elEl.textContent = '↑ ارفع ' + Math.abs(del).toFixed(1) + '°';
      elEl.className = 'calibrate-steer-el is-up';
    }
  }

  function applyProSteps(radio, live) {
    var steps = document.querySelectorAll('#cfProSteps li');
    if (!steps.length) return;
    var near = radio && radio.nearPeak;
    var locked = radio && radio.successMet;
    var hOk = live && live.horizontalAligned;
    steps.forEach(function (li) { li.classList.remove('is-active', 'is-done'); });
    if (locked) {
      steps[0].classList.add('is-done');
      steps[1].classList.add('is-done');
      steps[2].classList.add('is-active', 'is-done');
    } else if (near || hOk) {
      steps[0].classList.add('is-done');
      steps[1].classList.add('is-active');
    } else {
      steps[0].classList.add('is-active');
    }
  }

  function applySnapshot(snap) {
    if (!snap) return;
    setText('cfAdvice', snap.advice || '');
    var path = $('cfPath');
    if (path) {
      path.textContent = snap.pathSummary || '';
      path.classList.toggle('is-blocked', !snap.pathClear);
    }

    var live = role === 'tx' ? snap.transmitterLive : snap.receiverLive;
    setText('cfAzLive', fmt(live && live.azimuth) + '°');
    setText('cfElLive', fmt(live && live.elevation, 2) + '°');
    var poseStatus = $('cfPoseStatus');
    if (poseStatus) {
      if (!live || !live.connected) poseStatus.textContent = 'غير متصل بالجلسة.';
      else if (live.compassUnstable) poseStatus.textContent = 'البوصلة غير مستقرة — أبعد المعدن وحرّك ببطء.';
      else if (live.horizontalAligned && live.verticalAligned) poseStatus.textContent = 'محاذاة هندسية جيدة.';
      else poseStatus.textContent = 'وجّه حسب الأسهم أو راقب الإشارة.';
    }
    applySteer(live);

    var radio = snap.radio || {};
    if (boot.needsRadio) {
      setText('cfSignal', radio.signalDbm == null ? '—' : String(radio.signalDbm));
      setText('cfPeak', radio.peakSignalDbm == null ? '—' : String(radio.peakSignalDbm));
      setText('cfSnr', radio.snrDb == null ? '—' : String(radio.snrDb));
      setText('cfCcq', radio.ccqPercent == null ? '—' : String(radio.ccqPercent));
      setText('cfRadioStatus', radio.status || '');
      var hero = $('cfSignalHero');
      if (hero) {
        hero.classList.toggle('is-peak', !!radio.nearPeak || !!radio.successMet);
      }
      var badge = $('cfSuccess');
      if (badge) badge.hidden = !radio.successMet;
    }
    applyProSteps(radio, live);
  }

  connection.on('sessionUpdated', applySnapshot);

  var resetBtn = $('cfResetPeak');
  if (resetBtn) {
    resetBtn.addEventListener('click', function () {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('ResetPeak', code).catch(function () {});
      }
    });
  }

  var motionEnabled = false;
  var lastPublish = 0;

  function publishOrientation(e) {
    if (!motionEnabled || connection.state !== signalR.HubConnectionState.Connected) return;
    var now = Date.now();
    if (now - lastPublish < 120) return;
    lastPublish = now;
    var alpha = e.alpha;
    var beta = e.beta;
    var gamma = e.gamma;
    if (alpha == null || beta == null || gamma == null) return;
    var absolute = !!e.absolute;
    var accuracy = (typeof e.webkitCompassAccuracy === 'number') ? e.webkitCompassAccuracy : null;
    connection.invoke('PublishOrientation', code, role, alpha, beta, gamma, absolute, accuracy)
      .catch(function () {});
  }

  function enableMotion() {
    function start() {
      motionEnabled = true;
      window.addEventListener('deviceorientation', publishOrientation, true);
      setText('cfPoseStatus', 'الحساسات مفعّلة. الصق ظهر الموبايل بظهر الصحن.');
    }
    if (typeof DeviceOrientationEvent !== 'undefined' &&
        typeof DeviceOrientationEvent.requestPermission === 'function') {
      DeviceOrientationEvent.requestPermission().then(function (state) {
        if (state === 'granted') start();
        else setText('cfPoseStatus', 'لم يُسمح بالوصول للحساسات.');
      }).catch(function () {
        setText('cfPoseStatus', 'تعذر طلب إذن الحساسات.');
      });
    } else {
      start();
    }
  }

  var enableBtn = $('cfEnableMotion');
  if (enableBtn) enableBtn.addEventListener('click', enableMotion);
  if (boot.needsCompass) {
    // Auto-try on Android; iOS still needs tap.
    if (!(typeof DeviceOrientationEvent !== 'undefined' &&
          typeof DeviceOrientationEvent.requestPermission === 'function')) {
      enableMotion();
    }
  }

  connection.start()
    .then(function () { return connection.invoke('JoinField', code, role); })
    .catch(function () {
      setText('cfAdvice', 'تعذر الاتصال بالجلسة. أعد فتح الرابط.');
    });
})();
