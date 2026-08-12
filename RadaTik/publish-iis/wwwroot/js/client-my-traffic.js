(function () {
  'use strict'

  function formatBps(bps) {
    if (typeof bps !== 'number' || !isFinite(bps) || bps < 0) return '—'
    if (bps < 1000) return Math.round(bps) + ' bps'
    if (bps < 1e6) return (bps / 1e3).toFixed(1) + ' Kbps'
    return (bps / 1e6).toFixed(2) + ' Mbps'
  }

  function formatBytes(n) {
    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    if (n < 1024) return Math.round(n) + ' B'
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB'
    if (n < 1024 * 1024 * 1024) return (n / (1024 * 1024)).toFixed(2) + ' MB'
    return (n / (1024 * 1024 * 1024)).toFixed(2) + ' GB'
  }

  function formatNumber(n) {
    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    return n.toLocaleString()
  }

  function setText(id, value) {
    var el = document.getElementById(id)
    if (el) el.textContent = value
  }

  window.initClientMyTraffic = function () {
    var alertEl = document.getElementById('client-traffic-alert')
    var chartEl = document.getElementById('client-traffic-chart')
    var startBtn = document.getElementById('client-traffic-start-btn')
    var testStatusEl = document.getElementById('client-traffic-test-status')
    if (!chartEl || !window.Chart) return

    var labels = []
    var rxData = []
    var txData = []

    var chart = new window.Chart(chartEl.getContext('2d'), {
      type: 'line',
      data: {
        labels: labels,
        datasets: [
          {
            label: 'RX',
            data: rxData,
            borderColor: '#2563eb',
            backgroundColor: 'rgba(37,99,235,0.12)',
            pointRadius: 0,
            tension: 0.25,
            borderWidth: 2,
          },
          {
            label: 'TX',
            data: txData,
            borderColor: '#16a34a',
            backgroundColor: 'rgba(22,163,74,0.12)',
            pointRadius: 0,
            tension: 0.25,
            borderWidth: 2,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
          y: {
            ticks: {
              callback: function (v) {
                return formatBps(Number(v))
              },
            },
          },
        },
      },
    })

    function setError(msg) {
      if (!alertEl) return
      if (msg) {
        alertEl.textContent = msg
        alertEl.classList.remove('d-none')
      } else {
        alertEl.classList.add('d-none')
        alertEl.textContent = ''
      }
    }

    var lockUntil = 0
    var testActive = false
    var currentCharge = 50

    async function load() {
      var nowMs = Date.now()
      if (lockUntil > nowMs) {
        return
      }
      if (!testActive) {
        return
      }
      try {
        var r = await fetch(window.location.origin + '/api/client/traffic/live', {
          credentials: 'include',
          headers: { Accept: 'application/json' },
        })
        if (r.status === 429) {
          var retryAfter = Number(r.headers.get('Retry-After') || '1')
          if (isFinite(retryAfter) && retryAfter > 0) {
            lockUntil = Date.now() + retryAfter * 1000
          }
          throw new Error('rateLimited')
        }
        if (!r.ok) throw new Error('loadFailed')
        var data = await r.json()
        setError('')

        setText('client-traffic-updated', 'آخر تحديث: ' + new Date(data.utcIso).toLocaleString())
        setText('client-traffic-rx-rate', formatBps(data.rxBps))
        setText('client-traffic-tx-rate', formatBps(data.txBps))
        setText('client-traffic-rx-total', formatBytes(data.rxBytes))
        setText('client-traffic-tx-total', formatBytes(data.txBytes))
        // نحدّث القسم المباشر فقط (RX/TX والإجماليات والمخطط) بدون إعادة تحميل الصفحة.

        labels.push(new Date(data.utcIso).toLocaleTimeString())
        rxData.push(Number(data.rxBps || 0))
        txData.push(Number(data.txBps || 0))
        if (labels.length > 24) {
          labels.shift()
          rxData.shift()
          txData.shift()
        }
        chart.update('none')
      } catch (e) {
        if (e && e.message === 'rateLimited') {
          setError('يتم تحديث بيانات المعدل المباشر بسرعة كبيرة. يرجى الانتظار لحظات ثم المحاولة تلقائياً.')
          return
        }
        setError('تعذر تحميل بيانات معدل اتصالك حالياً. يرجى المحاولة بعد قليل.')
      }
    }

    function setTestStatusText(text) {
      if (testStatusEl) testStatusEl.textContent = text
    }

    async function refreshTestStatus() {
      try {
        var r = await fetch(window.location.origin + '/api/client/traffic/test-status', {
          credentials: 'include',
          headers: { Accept: 'application/json' },
        })
        if (!r.ok) throw new Error('statusFailed')
        var s = await r.json()
        testActive = !!s.testActive
        currentCharge = Number(s.chargeAmount || 50)
        if (startBtn) {
          startBtn.disabled = !s.canStartTest
        }
        if (s.testActive) {
          setTestStatusText('الاختبار فعال حتى: ' + new Date(s.activeUntilUtcIso).toLocaleTimeString())
        } else if (s.canStartTest) {
          setTestStatusText('يمكنك بدء اختبار جديد الآن.')
        } else {
          setTestStatusText('الاختبار التالي متاح عند: ' + new Date(s.nextEligibleUtcIso).toLocaleString())
        }
      } catch (e) {
        setTestStatusText('تعذر جلب حالة الاختبار حالياً.')
      }
    }

    if (startBtn) {
      startBtn.addEventListener('click', async function () {
        var msg = 'سيتم حسم ' + currentCharge.toLocaleString() + ' ل.س جديدة من رصيدك لبدء اختبار مدته 10 ثوانٍ. هل تريد المتابعة؟'
        if (!window.confirm(msg)) return
        startBtn.disabled = true
        try {
          var r = await fetch(window.location.origin + '/api/client/traffic/start-test', {
            method: 'POST',
            credentials: 'include',
            headers: { Accept: 'application/json' },
          })
          if (r.status === 402) {
            throw new Error('insufficient')
          }
          if (r.status === 409) {
            throw new Error('cooldown')
          }
          if (!r.ok) {
            throw new Error('startFailed')
          }
          setError('')
          await refreshTestStatus()
          await load()
        } catch (e) {
          if (e && e.message === 'insufficient') {
            setError('رصيدك غير كافٍ. يلزم 50 ل.س جديدة لبدء الاختبار.')
          } else if (e && e.message === 'cooldown') {
            setError('لا يمكنك بدء اختبار جديد الآن. حاول لاحقاً بعد انتهاء فترة الانتظار.')
          } else {
            setError('تعذر بدء الاختبار حالياً.')
          }
        } finally {
          await refreshTestStatus()
        }
      })
    }

    refreshTestStatus()
    window.setInterval(refreshTestStatus, 5000)
    window.setInterval(load, 2500)
  }
})()
