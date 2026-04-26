/* global signalR, Chart, $ */
(function () {
  /* ~3 min history: 180s / poll interval (default 0.5s → 360 points) */
  var MAX_CHART_POINTS = 360

  function formatBps(bps) {
    if (typeof bps !== 'number' || !isFinite(bps) || bps < 0) return '—'
    if (bps < 1000) return Math.round(bps) + ' bps'
    if (bps < 1e6) return (bps / 1e3).toFixed(1) + ' Kbps'
    return (bps / 1e6).toFixed(2) + ' Mbps'

  }


  function formatBytes(n) {

    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    var gb = n / (1024 * 1024 * 1024)
    return gb.toFixed(3) + ' GB'

  }


  function formatPackets(n) {

    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    return Math.round(n).toLocaleString() + ' pkt'

  }

  function formatGbValue(n) {
    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    return n.toFixed(3) + ' GB'
  }

  function formatMegaPacketsValue(n) {
    if (typeof n !== 'number' || !isFinite(n) || n < 0) return '—'
    return n.toFixed(3) + ' MPkt'
  }



  function escapeHtml(s) {

    if (!s) return ''

    return String(s)

      .replace(/&/g, '&amp;')

      .replace(/</g, '&lt;')

      .replace(/>/g, '&gt;')

      .replace(/"/g, '&quot;')

  }



  function escapeAttr(s) {

    return String(s).replace(/&/g, '&amp;').replace(/"/g, '&quot;')

  }



  function hubUrl() {

    return window.location.origin + '/hubs/traffic'

  }



  function getSelectedMetas(selectEl) {

    if (!selectEl || !selectEl.options) return []

    var out = []
    for (var i = 0; i < selectEl.options.length; i++) {
      var opt = selectEl.options[i]
      if (!opt || !opt.selected) continue
      var id = parseInt(opt.value, 10)
      var networkId = parseInt(opt.getAttribute('data-network'), 10)
      if (!id || !networkId) continue
      out.push({
        id: id,
        networkId: networkId,
        serverName: String(opt.getAttribute('data-server-name') || opt.textContent || '').trim(),
      })
    }
    return out

  }



  /** @typedef {{ type: 'all' }} SourceAll */
  /** @typedef {{ type: 'names', names: string[] }} SourceNames */

  function aggregateSelectedInterfaces(interfaces, sourceSpec) {

    var out = { rxBps: 0, txBps: 0, rxBytes: 0, txBytes: 0, rxPackets: 0, txPackets: 0 }
    if (!interfaces || !interfaces.length || !sourceSpec) return out

    var nameSet = null
    if (sourceSpec.type === 'names' && sourceSpec.names && sourceSpec.names.length) {
      nameSet = {}
      for (var n = 0; n < sourceSpec.names.length; n++) nameSet[sourceSpec.names[n]] = true
    }

    for (var i = 0; i < interfaces.length; i++) {
      var row = interfaces[i]
      if (!row) continue

      var itemKey = String(row.sourceKey || row.name || '')
      var included = sourceSpec.type === 'all' || (nameSet && nameSet[itemKey])
      if (!included) continue

      out.rxBps += row.rxBps || 0
      out.txBps += row.txBps || 0
      out.rxBytes += row.rxBytes || 0
      out.txBytes += row.txBytes || 0
      out.rxPackets += row.rxPackets || 0
      out.txPackets += row.txPackets || 0
    }

    return out

  }


  function isChartSourceReady(spec) {

    if (!spec) return false

    if (spec.type === 'all') return true

    return spec.type === 'names' && spec.names.length > 0

  }



  function getSourceSpecFromUI(allEl, listEl) {

    if (!allEl || !listEl) return null

    if (allEl.checked) return { type: 'all' }

    var names = []

    var boxes = listEl.querySelectorAll('input[type="checkbox"][data-iface]')

    for (var i = 0; i < boxes.length; i++) {

      if (boxes[i].checked) names.push(boxes[i].getAttribute('data-iface'))

    }

    if (!names.length) return null

    return { type: 'names', names: names }

  }



  function refreshIfaceCheckboxes(allEl, listEl, hintEl, interfaces, preserved) {

    var prevAll = preserved && preserved.all

    var prevNames = preserved && preserved.names ? preserved.names.slice() : []

    if (hintEl) hintEl.classList.add('d-none')

    listEl.innerHTML = ''

    var entries = (interfaces || [])
      .map(function (r) {
        return {
          key: String(r.sourceKey || ''),
          label: String(r.displayName || r.name || ''),
        }
      })
      .filter(function (x) { return x.key && x.label })

    entries.sort(function (a, b) {
      return a.label.localeCompare(b.label, undefined, { sensitivity: 'base' })
    })

    if (!entries.length) {

      listEl.innerHTML =

        '<div class="text-muted py-1">' +

        (!interfaces || !interfaces.length ? 'لا توجد واجهات في هذه اللقطة.' : '') +

        '</div>'

      if (hintEl) hintEl.classList.remove('d-none')

      allEl.checked = false

      allEl.disabled = true

      return

    }

    allEl.disabled = false

    if (prevAll) {

      allEl.checked = true

    }

    for (var i = 0; i < entries.length; i++) {
      var nm = entries[i]

      var id = 'traffic-iface-cb-' + i

      var checked = prevAll ? false : prevNames.indexOf(nm.key) >= 0

      var row = document.createElement('div')

      row.className = 'form-check'

      row.innerHTML =

        '<input class="form-check-input" type="checkbox" id="' +

        escapeAttr(id) +

        '" data-iface="' +

        escapeAttr(nm.key) +

        '"' +

        (checked ? ' checked' : '') +

        ' />' +

        '<label class="form-check-label" for="' +

        escapeAttr(id) +

        '">' +

        escapeHtml(nm.label) +

        '</label>'

      listEl.appendChild(row)

    }

    applyIfaceSearchFilter()

  }



  function applyIfaceSearchFilter() {

    var listEl = document.getElementById('traffic-chart-iface-list')
    if (!listEl) return

    var searchEl = document.getElementById('traffic-chart-iface-search')

    var term = searchEl ? String(searchEl.value || '').trim().toLowerCase() : ''

    var rows = listEl.querySelectorAll('.form-check')

    var visibleCount = 0

    for (var i = 0; i < rows.length; i++) {

      var labelEl = rows[i].querySelector('label')

      var text = labelEl ? String(labelEl.textContent || '').toLowerCase() : ''

      var visible = !term || text.indexOf(term) >= 0

      rows[i].style.display = visible ? '' : 'none'

      if (visible) visibleCount++

    }

    var emptyEl = listEl.querySelector('.tm-iface-empty')

    if (!emptyEl) {

      emptyEl = document.createElement('div')

      emptyEl.className = 'tm-iface-empty text-muted py-1'

      emptyEl.textContent = 'لا توجد واجهات مطابقة للبحث.'

      listEl.appendChild(emptyEl)

    }

    emptyEl.style.display = term && visibleCount === 0 ? '' : 'none'

  }



  function setIfaceListEnabled(listEl, enabled) {

    if (!listEl) return

    listEl.classList.toggle('tm-disabled', !enabled)

  }



  function createTrafficChart(canvasEl) {

    var primary = 'rgb(13, 110, 253)'

    var txCol = 'rgb(234, 88, 12)'

    var chart = new Chart(canvasEl.getContext('2d'), {

      type: 'line',

      data: {

        labels: [],

        datasets: [

          {

            label: 'معدل RX',

            data: [],

            borderColor: primary,

            backgroundColor: 'rgba(13, 110, 253, 0.08)',

            tension: 0.22,

            fill: true,

            pointRadius: 0,

            borderWidth: 2,

          },

          {

            label: 'معدل TX',

            data: [],

            borderColor: txCol,

            backgroundColor: 'rgba(234, 88, 12, 0.06)',

            tension: 0.22,

            fill: true,

            pointRadius: 0,

            borderWidth: 2,

          },

        ],

      },

      options: {

        responsive: true,

        maintainAspectRatio: false,

        animation: {

          duration: 380,

          easing: 'easeOutQuart',

        },

        interaction: { mode: 'index', intersect: false },

        plugins: {

          legend: {

            display: true,

            position: 'bottom',

            labels: { boxWidth: 12, usePointStyle: true, padding: 14 },

          },

          tooltip: {

            callbacks: {

              label: function (ctx) {

                var v = typeof ctx.parsed.y === 'number' ? ctx.parsed.y : 0

                var formatter = ctx.chart && ctx.chart.$tmTooltipFormatter
                var rendered = typeof formatter === 'function' ? formatter(v) : v
                return (ctx.dataset.label || '') + ': ' + rendered

              },

            },

          },

        },

        scales: {

          y: {

            beginAtZero: true,

            title: { display: true, text: 'Mbps' },

            ticks: { precision: 2 },

          },

          x: {

            ticks: {

              maxRotation: 0,

              autoSkip: true,

              maxTicksLimit: 10,

            },

          },

        },

      },

    })

    chart.$tmTooltipFormatter = function (v) { return (typeof v === 'number' ? v.toFixed(3) : '0.000') + ' Mbps' }
    return chart

  }


  function createSingleMetricChart(canvasEl, label, yAxisTitle, color, formatter) {

    return new Chart(canvasEl.getContext('2d'), {
      type: 'line',
      data: {
        labels: [],
        datasets: [
          {
            label: label,
            data: [],
            borderColor: color,
            backgroundColor: 'rgba(13, 110, 253, 0.06)',
            tension: 0.22,
            fill: true,
            pointRadius: 0,
            borderWidth: 2,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 380, easing: 'easeOutQuart' },
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: true, position: 'bottom', labels: { boxWidth: 12, usePointStyle: true, padding: 14 } },
          tooltip: {
            callbacks: {
              label: function (ctx) {
                var v = typeof ctx.parsed.y === 'number' ? ctx.parsed.y : 0
                return (ctx.dataset.label || '') + ': ' + (typeof formatter === 'function' ? formatter(v) : v)
              },
            },
          },
        },
        scales: {
          y: { beginAtZero: true, title: { display: true, text: yAxisTitle } },
          x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 10 } },
        },
      },
    })
  }


  function createDualMetricChart(canvasEl, leftLabel, rightLabel, yAxisTitle, leftColor, rightColor, formatter) {

    var chart = new Chart(canvasEl.getContext('2d'), {
      type: 'line',
      data: {
        labels: [],
        datasets: [
          {
            label: leftLabel,
            data: [],
            borderColor: leftColor,
            backgroundColor: 'rgba(13, 110, 253, 0.08)',
            tension: 0.22,
            fill: true,
            pointRadius: 0,
            borderWidth: 2,
          },
          {
            label: rightLabel,
            data: [],
            borderColor: rightColor,
            backgroundColor: 'rgba(234, 88, 12, 0.06)',
            tension: 0.22,
            fill: true,
            pointRadius: 0,
            borderWidth: 2,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 380, easing: 'easeOutQuart' },
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: true, position: 'bottom', labels: { boxWidth: 12, usePointStyle: true, padding: 14 } },
          tooltip: {
            callbacks: {
              label: function (ctx) {
                var v = typeof ctx.parsed.y === 'number' ? ctx.parsed.y : 0
                return (ctx.dataset.label || '') + ': ' + (typeof formatter === 'function' ? formatter(v) : v)
              },
            },
          },
        },
        scales: {
          y: { beginAtZero: true, title: { display: true, text: yAxisTitle } },
          x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 10 } },
        },
      },
    })

    return chart
  }


  function resetChartSeries(chart, history) {

    history.labels = []

    history.left = []
    history.right = []

    if (!chart) return

    chart.data.labels = []

    chart.data.datasets[0].data = []
    if (chart.data.datasets.length > 1) {
      chart.data.datasets[1].data = []
    }

    chart.update('none')

  }



  function appendChartPoint(chart, history, utcIso, leftValue, rightValue) {

    var t = utcIso ? new Date(utcIso) : new Date()

    var label = t.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })

    history.labels.push(label)

    history.left.push(typeof leftValue === 'number' ? +leftValue.toFixed(4) : null)
    history.right.push(typeof rightValue === 'number' ? +rightValue.toFixed(4) : null)

    while (history.labels.length > MAX_CHART_POINTS) {

      history.labels.shift()

      history.left.shift()
      history.right.shift()

    }

    if (!chart) return

    chart.data.labels = history.labels.slice()

    chart.data.datasets[0].data = history.left.slice()
    if (chart.data.datasets.length > 1) {
      chart.data.datasets[1].data = history.right.slice()
    }

    chart.update()

  }



  window.initMikroTikTrafficMvc = function (options) {

    var opt = options || {}

    var isClientPortal = opt.mode === 'clientPortal'

    var hideIfaceUi = opt.hideIfaceUi === true

    var autoMatchDone = false

    var selectEl = document.getElementById('mikrotik-traffic-server')
    var selectAllServersBtnEl = document.getElementById('traffic-select-all-servers')
    var clearAllServersBtnEl = document.getElementById('traffic-clear-all-servers')

    var ifaceAllEl = document.getElementById('traffic-chart-iface-all')

    var ifaceListEl = document.getElementById('traffic-chart-iface-list')

    var ifaceHintEl = document.getElementById('traffic-chart-iface-hint')

    var ifaceSearchEl = document.getElementById('traffic-chart-iface-search')

    var chartOverlayEl = document.getElementById('traffic-chart-overlay')

    var canvasEl = document.getElementById('traffic-chart')

    var byteCanvasEl = document.getElementById('traffic-byte-chart')

    var packetCanvasEl = document.getElementById('traffic-packet-chart')
    var rateChartWrapEl = document.getElementById('traffic-rate-chart-wrap')
    var byteChartWrapEl = document.getElementById('traffic-byte-chart-wrap')
    var packetChartWrapEl = document.getElementById('traffic-packet-chart-wrap')
    var graphTogglesHostEl = document.getElementById('traffic-graph-toggles')

    var alertEl = document.getElementById('mikrotik-traffic-alert')

    var updatedEl = document.getElementById('mikrotik-traffic-updated')

    var liveRxEl = document.getElementById('traffic-live-rx')

    var liveTxEl = document.getElementById('traffic-live-tx')

    var liveRxBytesEl = document.getElementById('traffic-live-rx-bytes')

    var liveTxBytesEl = document.getElementById('traffic-live-tx-bytes')

    var chartHintEl = document.getElementById('traffic-chart-hint')

    var activeSourceNameEl = document.getElementById('traffic-active-source-name')

    var kpiTotalEl = document.getElementById('traffic-kpi-total')

    var kpiUpEl = document.getElementById('traffic-kpi-up')

    var kpiDownEl = document.getElementById('traffic-kpi-down')

    var kpiBridgeEl = document.getElementById('traffic-kpi-bridge')

    var insightsBoxEl = document.getElementById('traffic-traffic-insights')

    var insightsTextEl = document.getElementById('traffic-traffic-insights-text')

    var TRAFFIC_PREFS_KEY = 'radtik.mikrotikTrafficPrefs.v1'

    function normalizeGraphVisibility(v) {
      var input = v && typeof v === 'object' ? v : {}
      return {
        rate: input.rate !== false,
        byte: input.byte !== false,
        packet: input.packet !== false,
      }
    }

    function normalizeSourceSpec(spec) {

      if (!spec || typeof spec !== 'object') return null

      if (spec.type === 'all') return { type: 'all' }

      if (spec.type === 'names' && Array.isArray(spec.names)) {

        var names = spec.names

          .map(function (x) { return String(x || '').trim() })

          .filter(Boolean)

        if (names.length) return { type: 'names', names: names }

      }

      return null

    }

    function loadTrafficPrefs() {

      try {

        var raw = window.localStorage.getItem(TRAFFIC_PREFS_KEY)

        if (!raw) return { lastServerId: null, sourceByServer: {}, graphVisibility: normalizeGraphVisibility(null) }

        var parsed = JSON.parse(raw)

        if (!parsed || typeof parsed !== 'object') return { lastServerId: null, sourceByServer: {}, graphVisibility: normalizeGraphVisibility(null) }

        var sourceByServer = {}

        if (parsed.sourceByServer && typeof parsed.sourceByServer === 'object') {

          var keys = Object.keys(parsed.sourceByServer)

          for (var i = 0; i < keys.length; i++) {

            var serverKey = String(keys[i])

            var spec = normalizeSourceSpec(parsed.sourceByServer[serverKey])

            if (spec) sourceByServer[serverKey] = spec

          }

        }

        return {

          lastServerId: Number.isFinite(parsed.lastServerId) ? parsed.lastServerId : null,
          sourceByServer: sourceByServer,
          graphVisibility: normalizeGraphVisibility(parsed.graphVisibility),

        }

      } catch (e) {

        return { lastServerId: null, sourceByServer: {}, graphVisibility: normalizeGraphVisibility(null) }

      }

    }

    function saveTrafficPrefs(prefs) {

      try {

        window.localStorage.setItem(TRAFFIC_PREFS_KEY, JSON.stringify(prefs))

      } catch (e) {}

    }

    var trafficPrefs = loadTrafficPrefs()

    if (chartHintEl) chartHintEl.textContent = 'محور الزمن: وقت التحديث — القيم بـ Mbps'

    function setLiveValues(rateRxMbps, rateTxMbps, rxBytes, txBytes) {
      if (liveRxEl) liveRxEl.textContent = formatBps(rateRxMbps * 1e6)
      if (liveTxEl) liveTxEl.textContent = formatBps(rateTxMbps * 1e6)
      if (liveRxBytesEl) liveRxBytesEl.textContent = formatBytes(rxBytes)
      if (liveTxBytesEl) liveTxBytesEl.textContent = formatBytes(txBytes)
    }

    function clearLiveRates() {

      if (liveRxEl) liveRxEl.textContent = '—'

      if (liveTxEl) liveTxEl.textContent = '—'

      if (liveRxBytesEl) liveRxBytesEl.textContent = '—'

      if (liveTxBytesEl) liveTxBytesEl.textContent = '—'

    }

    function applyGraphVisibility() {
      if (!graphTogglesHostEl) return

      var buttonMap = {
        rate: graphTogglesHostEl.querySelector('[data-graph-toggle="rate"]'),
        byte: graphTogglesHostEl.querySelector('[data-graph-toggle="byte"]'),
        packet: graphTogglesHostEl.querySelector('[data-graph-toggle="packet"]'),
      }

      var prefsVisibility = normalizeGraphVisibility(trafficPrefs.graphVisibility)
      if (buttonMap.rate) buttonMap.rate.classList.toggle('is-active', prefsVisibility.rate)
      if (buttonMap.byte) buttonMap.byte.classList.toggle('is-active', prefsVisibility.byte)
      if (buttonMap.packet) buttonMap.packet.classList.toggle('is-active', prefsVisibility.packet)

      function isEnabled(key) {
        var btn = buttonMap[key]
        return !!(btn && btn.classList.contains('is-active'))
      }

      var showRate = isEnabled('rate')
      var showByte = isEnabled('byte')
      var showPacket = isEnabled('packet')

      if (rateChartWrapEl) rateChartWrapEl.classList.toggle('d-none', !showRate)
      if (byteChartWrapEl) byteChartWrapEl.classList.toggle('d-none', !showByte)
      if (packetChartWrapEl) packetChartWrapEl.classList.toggle('d-none', !showPacket)

      if (trafficChart && showRate) trafficChart.resize()
      if (byteChart && showByte) byteChart.resize()
      if (packetChart && showPacket) packetChart.resize()
    }

    function updateInterfaceKpis(interfaces) {

      var list = interfaces || []

      var total = list.length

      var up = 0

      var bridge = 0

      for (var i = 0; i < list.length; i++) {

        if (list[i].running) up++

        if (list[i].isBridge || list[i].memberOfBridge) bridge++

      }

      var down = total - up

      if (kpiTotalEl) kpiTotalEl.textContent = String(total)

      if (kpiUpEl) kpiUpEl.textContent = String(up)

      if (kpiDownEl) kpiDownEl.textContent = String(down >= 0 ? down : 0)

      if (kpiBridgeEl) kpiBridgeEl.textContent = String(bridge)

    }



    function resetInsightsNeutral() {

      if (!opt.showInsights || !insightsTextEl) return

      if (insightsBoxEl) {

        insightsBoxEl.classList.remove(

          'alert-success',

          'alert-warning',

          'alert-danger',

          'alert-info',

          'alert-secondary'

        )

        insightsBoxEl.classList.add('alert-light', 'border')

      }

      insightsTextEl.textContent = 'بانتظار بيانات الترافك لتحليل الحالة…'

    }



    function updateTrafficInsights(rxMbps, txMbps, chartActiveForInsight) {

      if (!opt.showInsights || !insightsTextEl) return

      if (!chartActiveForInsight) {

        insightsTextEl.textContent = hideIfaceUi

          ? 'بانتظار بيانات الترافك لعرض الملاحظات…'

          : 'حدّد مصدر المخطط (واجهة أو مجموع) لعرض ملاحظات تتعلق بحركة الترافك.'

        if (insightsBoxEl) {

          insightsBoxEl.classList.remove('alert-success', 'alert-warning', 'alert-danger', 'alert-info', 'alert-secondary')

          insightsBoxEl.classList.add('alert-light', 'border')

        }

        return

      }

      var rx = typeof rxMbps === 'number' && isFinite(rxMbps) ? rxMbps : 0

      var tx = typeof txMbps === 'number' && isFinite(txMbps) ? txMbps : 0

      var total = rx + tx

      if (insightsBoxEl) {

        insightsBoxEl.classList.remove(

          'alert-light',

          'alert-success',

          'alert-warning',

          'alert-danger',

          'alert-info',

          'alert-secondary'

        )

      }

      var msg = ''

      var alertCls = 'alert-info'

      if (total < 0.05) {

        msg =

          'تدفق ضئيل جداً أو لا يوجد نشاط ملحوظ وفق الواجهة المختارة — قد يعني عدم استخدام فعلي أو واجهة لا تحمل جلسة الاشتراك.'

        alertCls = 'alert-secondary'

      } else if (total < 2) {

        msg = 'استخدام خفيف: مناسب للتصفح والاستخدام اليومي العادي على الرابط.'

        alertCls = 'alert-success'

      } else if (total < 20) {

        msg =

          'استخدام متوسط إلى مرتفع نسبياً. إذا لاحظت بطئاً، راجع الأجهزة المتصلة أو التحميلات الكبيرة.'

        alertCls = 'alert-info'

      } else if (total < 80) {

        msg =

          'تحميل مرتفع على الرابط. قد يؤثر على السرعة إذا كان اشتراكك محدود السرعة أو الشبكة مشغولة.'

        alertCls = 'alert-warning'

      } else {

        msg =

          'تحميل مرتفع جداً — غالباً عدة أجهزة أو بث/تحميل ثقيل. راقب الاستخدام أو تواصل مع الدعم إذا كان الأداء غير مقبول.'

        alertCls = 'alert-danger'

      }

      insightsTextEl.textContent = msg

      if (insightsBoxEl) insightsBoxEl.classList.add(alertCls, 'border')

    }

    if (!selectEl || typeof Chart === 'undefined') {

      return

    }

    var chartHistory = { labels: [], left: [], right: [] }
    var byteChartHistory = { labels: [], left: [], right: [] }
    var packetChartHistory = { labels: [], left: [], right: [] }

    var trafficChart = canvasEl ? createTrafficChart(canvasEl) : null
    var byteChart = byteCanvasEl
      ? createDualMetricChart(byteCanvasEl, 'RX Byte', 'TX Byte', 'GB', 'rgb(13, 110, 253)', 'rgb(234, 88, 12)', formatGbValue)
      : null
    var packetChart = packetCanvasEl
      ? createDualMetricChart(packetCanvasEl, 'RX Packet', 'TX Packet', 'MPkt', 'rgb(13, 110, 253)', 'rgb(234, 88, 12)', formatMegaPacketsValue)
      : null

    function updateActiveSourceName(spec) {
      if (!activeSourceNameEl) return
      if (!spec) {
        activeSourceNameEl.textContent = ''
        return
      }

      if (spec.type === 'all') {
        activeSourceNameEl.textContent = 'المصدر النشط: مجموع كل الواجهات'
        return
      }

      if (spec.type === 'names' && spec.names && spec.names.length === 1) {
        var selectedKey = spec.names[0]
        var selectedLabel = selectedKey
        if (ifaceListEl) {
          var boxes = ifaceListEl.querySelectorAll('input[type="checkbox"][data-iface]')
          for (var bi = 0; bi < boxes.length; bi++) {
            if (boxes[bi].getAttribute('data-iface') !== selectedKey) continue
            var lbl = boxes[bi].nextElementSibling
            selectedLabel = lbl ? String(lbl.textContent || '').trim() : selectedKey
            break
          }
        }
        activeSourceNameEl.textContent = 'الواجهة النشطة: ' + selectedLabel
        return
      }

      if (spec.type === 'names' && spec.names && spec.names.length > 1) {
        activeSourceNameEl.textContent = 'المصدر النشط: ' + spec.names.length + ' واجهات محددة'
        return
      }

      activeSourceNameEl.textContent = ''
    }



    var connection = null

    var currentTargets = []

    var latestSnapshots = {}

    var lastInterfaces = []

    function saveCurrentServerPreference(meta) {

      trafficPrefs.lastServerId = meta && meta.id ? meta.id : null

      saveTrafficPrefs(trafficPrefs)

    }

    function getSavedSourceForServer(serverId) {

      if (!serverId) return null

      var key = String(serverId)

      return normalizeSourceSpec(trafficPrefs.sourceByServer[key]) || null

    }

    function saveCurrentSourcePreference() {

      if (hideIfaceUi) return

      var spec = getSourceSpecFromUI(ifaceAllEl, ifaceListEl)
      var metas = getSelectedMetas(selectEl)
      if (!metas.length) return

      for (var i = 0; i < metas.length; i++) {
        var key = String(metas[i].id)
        if (!spec) {
          delete trafficPrefs.sourceByServer[key]
        } else {
          trafficPrefs.sourceByServer[key] = normalizeSourceSpec(spec)
        }
      }

      saveTrafficPrefs(trafficPrefs)

    }

    function applySavedSourceToUi(savedSpec) {

      if (hideIfaceUi || !savedSpec || !ifaceAllEl || !ifaceListEl) return false

      var boxes = ifaceListEl.querySelectorAll('input[type="checkbox"][data-iface]')

      if (!boxes || !boxes.length) return false

      if (savedSpec.type === 'all') {

        ifaceAllEl.checked = true

        for (var i = 0; i < boxes.length; i++) boxes[i].checked = false

        return true

      }

      if (savedSpec.type === 'names' && savedSpec.names && savedSpec.names.length) {

        var wanted = {}

        for (var j = 0; j < savedSpec.names.length; j++) wanted[savedSpec.names[j]] = true

        var matched = 0

        for (var k = 0; k < boxes.length; k++) {

          var nm = boxes[k].getAttribute('data-iface')

          var isMatch = !!wanted[nm]

          boxes[k].checked = isMatch

          if (isMatch) matched++

        }

        ifaceAllEl.checked = false

        return matched > 0

      }

      return false

    }



    function buildAutoMatchSpec(interfaces, userName) {

      if (!interfaces || !interfaces.length) return null

      var raw = String(userName || '').trim()

      if (!raw) return { type: 'all' }

      var uname = raw.toLowerCase()

      for (var i = 0; i < interfaces.length; i++) {

        var n = interfaces[i].name

        if (!n) continue

        if (n.toLowerCase() === uname) return { type: 'names', names: [n] }

      }

      for (var j = 0; j < interfaces.length; j++) {

        var n2 = interfaces[j].name

        if (!n2) continue

        var nm = n2.toLowerCase()

        if (nm.indexOf(uname) >= 0 || uname.indexOf(nm) >= 0) return { type: 'names', names: [n2] }

      }

      return { type: 'all' }

    }



    function getEffectiveSourceSpec() {

      if (hideIfaceUi) return buildAutoMatchSpec(lastInterfaces, opt.autoMatchUserName)

      return getSourceSpecFromUI(ifaceAllEl, ifaceListEl)

    }



    function preserveIfaceSelection() {

      if (hideIfaceUi) return { all: false, names: [] }

      var spec = getSourceSpecFromUI(ifaceAllEl, ifaceListEl)

      if (!spec) return { all: false, names: [] }

      if (spec.type === 'all') return { all: true, names: [] }

      return { all: false, names: spec.names.slice() }

    }



    function updateChartOverlay() {

      if (!chartOverlayEl) return

      var hasServer = getSelectedMetas(selectEl).length > 0

      var spec = getEffectiveSourceSpec()

      var ready = hasServer && isChartSourceReady(spec)

      chartOverlayEl.classList.toggle('d-none', ready)

      var span = chartOverlayEl.querySelector('span')

      if (span && !ready) {

        if (hideIfaceUi) {

          span.textContent = hasServer

            ? 'جاري استقبال بيانات الترافك من الخادم…'

            : 'اختر الخادم ثم انتظر التحديث.'

        } else {

          span.textContent = hasServer

            ? 'حدّد مصدر المخطط: واجهة أو أكثر، أو «مجموع كل الواجهات».'

            : 'اختر الخادم ثم حدّد مصدر المخطط (واجهة أو أكثر، أو مجموع كل الواجهات).'

        }

      }

    }



    function clearTrafficUi() {

      resetChartSeries(trafficChart, chartHistory)
      resetChartSeries(byteChart, byteChartHistory)
      resetChartSeries(packetChart, packetChartHistory)

      clearLiveRates()

      if (activeSourceNameEl) activeSourceNameEl.textContent = ''

      lastInterfaces = []

      if (ifaceAllEl) {

        ifaceAllEl.checked = false

        ifaceAllEl.disabled = true

      }

      if (ifaceListEl) {

        ifaceListEl.innerHTML = ''

        if (ifaceHintEl) {

          ifaceHintEl.textContent = 'اختر خادماً أولاً ثم حدّد واجهة أو أكثر، أو «مجموع كل الواجهات».'

          ifaceHintEl.classList.remove('d-none')

        }

        setIfaceListEnabled(ifaceListEl, false)

      }

      updateInterfaceKpis([])

      updateChartOverlay()

      resetInsightsNeutral()

    }



    function showError(msg) {

      if (!alertEl) return

      alertEl.textContent = msg

      alertEl.classList.remove('d-none')

    }



    function clearError() {

      if (!alertEl) return

      alertEl.classList.add('d-none')

      alertEl.textContent = ''

    }



    function mergeInterfacesFromSnapshots() {
      var metas = getSelectedMetas(selectEl)
      if (!metas.length) return { utcIso: null, interfaces: [] }

      var selectedById = {}
      for (var i = 0; i < metas.length; i++) selectedById[String(metas[i].id)] = metas[i]

      var merged = []
      var latestIso = null
      var snapshotKeys = Object.keys(latestSnapshots)
      var hasMultiServer = metas.length > 1

      for (var s = 0; s < snapshotKeys.length; s++) {
        var key = snapshotKeys[s]
        var snap = latestSnapshots[key]
        if (!snap || !selectedById[String(snap.serverId)]) continue

        if (snap.utcIso && (!latestIso || new Date(snap.utcIso) > new Date(latestIso))) {
          latestIso = snap.utcIso
        }

        var serverTitle = String(snap.serverName || selectedById[String(snap.serverId)].serverName || ('Server ' + snap.serverId))
        var list = Array.isArray(snap.interfaces) ? snap.interfaces : []

        for (var j = 0; j < list.length; j++) {
          var row = list[j] || {}
          var ifaceName = String(row.name || '')
          if (!ifaceName) continue
          merged.push(Object.assign({}, row, {
            sourceKey: String(snap.serverId) + '::' + ifaceName,
            displayName: hasMultiServer ? (serverTitle + ' — ' + ifaceName) : ifaceName,
            serverId: snap.serverId,
            serverName: serverTitle,
          }))
        }
      }

      return { utcIso: latestIso, interfaces: merged }
    }


    function applyTraffic(isoUtc, interfaces) {

      var list = interfaces || []

      lastInterfaces = list

      updateInterfaceKpis(list)

      var preserved = preserveIfaceSelection()

      if (ifaceAllEl && ifaceListEl && !hideIfaceUi) {

        refreshIfaceCheckboxes(ifaceAllEl, ifaceListEl, ifaceHintEl, list, preserved)

        if (!preserved.all && !preserved.names.length) {

          var selectedMetas = getSelectedMetas(selectEl)
          var savedSpec = selectedMetas.length ? getSavedSourceForServer(selectedMetas[0].id) : null

          if (savedSpec) {

            applySavedSourceToUi(savedSpec)

          }

        }

        setIfaceListEnabled(ifaceListEl, getSelectedMetas(selectEl).length > 0)

      }



      if (

        !hideIfaceUi &&

        ifaceAllEl &&

        ifaceListEl &&

        isClientPortal &&

        opt.autoMatchUserName &&

        !autoMatchDone &&

        list.length

      ) {

        autoMatchDone = true

        var raw = String(opt.autoMatchUserName || '').trim()

        if (raw) {

          var uname = raw.toLowerCase()

          var boxes = ifaceListEl.querySelectorAll('input[type="checkbox"][data-iface]')

          var matchedEl = null

          for (var ai = 0; ai < boxes.length; ai++) {

            var nm = (boxes[ai].getAttribute('data-iface') || '').toLowerCase()

            if (nm === uname) {

              matchedEl = boxes[ai]

              break

            }

          }

          if (!matchedEl) {

            for (var aj = 0; aj < boxes.length; aj++) {

              var nm2 = (boxes[aj].getAttribute('data-iface') || '').toLowerCase()

              if (nm2.indexOf(uname) >= 0 || uname.indexOf(nm2) >= 0) {

                matchedEl = boxes[aj]

                break

              }

            }

          }

          if (matchedEl) {

            ifaceAllEl.checked = false

            for (var ak = 0; ak < boxes.length; ak++) {

              boxes[ak].checked = boxes[ak] === matchedEl

            }

          } else {

            ifaceAllEl.checked = true

          }

        } else {

          ifaceAllEl.checked = true

        }

      }

      updateChartOverlay()

      var spec = getEffectiveSourceSpec()
      updateActiveSourceName(spec)

      var chartActive = getSelectedMetas(selectEl).length > 0 && isChartSourceReady(spec)

      if (chartActive && list.length) {

        var agg = aggregateSelectedInterfaces(list, spec)
        var rxMbps = agg.rxBps / 1e6
        var txMbps = agg.txBps / 1e6
        setLiveValues(rxMbps, txMbps, agg.rxBytes, agg.txBytes)

        if (isoUtc) {

          appendChartPoint(trafficChart, chartHistory, isoUtc, rxMbps, txMbps)
          appendChartPoint(
            byteChart,
            byteChartHistory,
            isoUtc,
            agg.rxBytes / (1024 * 1024 * 1024),
            agg.txBytes / (1024 * 1024 * 1024)
          )
          appendChartPoint(
            packetChart,
            packetChartHistory,
            isoUtc,
            agg.rxPackets / 1000000,
            agg.txPackets / 1000000
          )

        }

        updateTrafficInsights(rxMbps, txMbps, true)

      } else {

        clearLiveRates()

        updateTrafficInsights(0, 0, false)

      }

    }



    function stopCurrent() {

      return Promise.resolve()

        .then(function () {

          if (connection && currentTargets.length) {
            var leaves = []
            for (var i = 0; i < currentTargets.length; i++) {
              (function (t) {
                leaves.push(connection.invoke('LeaveTraffic', t.networkId, t.id).catch(function () {}))
              })(currentTargets[i])
            }
            return Promise.all(leaves)

          }

        })

        .then(function () {

          if (connection) {

            return connection.stop().catch(function () {})

          }

        })

        .then(function () {

          connection = null

          currentTargets = []
          latestSnapshots = {}

        })

    }



    function startFor(metas) {

      return stopCurrent().then(function () {

        if (!metas || !metas.length) {

          clearTrafficUi()

          return

        }



        clearError()

        resetChartSeries(trafficChart, chartHistory)
        resetChartSeries(byteChart, byteChartHistory)
        resetChartSeries(packetChart, packetChartHistory)

        clearLiveRates()

        lastInterfaces = []

        if (ifaceAllEl) {

          ifaceAllEl.checked = false

          ifaceAllEl.disabled = true

        }

        if (ifaceListEl) {

          ifaceListEl.innerHTML = ''

          if (ifaceHintEl) {

            ifaceHintEl.textContent = 'جاري تحميل قائمة الواجهات…'

            ifaceHintEl.classList.remove('d-none')

          }

          setIfaceListEnabled(ifaceListEl, true)

        }

        updateChartOverlay()
        updateActiveSourceName(null)

        connection = new signalR.HubConnectionBuilder()

          .withUrl(hubUrl(), {

            withCredentials: true,

            transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,

          })

          .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])

          .configureLogging(signalR.LogLevel.Warning)

          .build()

        try {

          connection.serverTimeoutInMilliseconds = 120000

          connection.keepAliveIntervalInMilliseconds = 15000

        } catch (e) {}



        connection.on('trafficUpdate', function (payload) {

          clearError()

          if (payload && payload.serverId) {
            latestSnapshots[String(payload.serverId)] = {
              serverId: payload.serverId,
              serverName: payload.serverName || '',
              utcIso: payload.utcIso || '',
              interfaces: payload.interfaces || [],
            }
          }
          var merged = mergeInterfacesFromSnapshots()
          if (merged.utcIso && updatedEl) {
            updatedEl.textContent = 'آخر تحديث: ' + new Date(merged.utcIso).toLocaleString()
          }
          applyTraffic(merged.utcIso, merged.interfaces)

        })



        connection.on('trafficError', function (err) {

          showError((err && err.message) || 'حدث خطأ أثناء جلب الترافك من MikroTik.')

        })



        return connection

          .start()

          .then(function () {
            var joins = []
            for (var i = 0; i < metas.length; i++) {
              (function (t) {
                joins.push(connection.invoke('JoinTraffic', t.networkId, t.id))
              })(metas[i])
            }
            return Promise.all(joins)

          })

          .then(function () {

            currentTargets = metas.slice()

          })

          .catch(function () {

            showError(

              isClientPortal

                ? 'تعذر فتح الاتصال اللحظي. تأكد من تسجيل الدخول، وأن خادم MikroTik مفعّل لحسابك، وأن الراوتر يقبل الاتصال من خادمنا.'

                : 'تعذر فتح الاتصال اللحظي. تأكد من تسجيل الدخول كمدير شركة وأن الراوتر يقبل الاتصال من الخادم.'

            )

            return stopCurrent()

          })

      })

    }



    function onChartSourceChanged() {

      if (ifaceAllEl && ifaceAllEl.checked && ifaceListEl) {

        var boxes = ifaceListEl.querySelectorAll('input[type="checkbox"][data-iface]')

        for (var b = 0; b < boxes.length; b++) {

          boxes[b].checked = false

        }

      }

      updateChartOverlay()

      saveCurrentSourcePreference()

      resetChartSeries(trafficChart, chartHistory)

      var spec = getEffectiveSourceSpec()
      updateActiveSourceName(spec)

      if (!getSelectedMetas(selectEl).length || !isChartSourceReady(spec) || !lastInterfaces.length) {

        clearLiveRates()

        updateTrafficInsights(0, 0, false)

        return

      }

      var agg = aggregateSelectedInterfaces(lastInterfaces, spec)
      var rxMbps = agg.rxBps / 1e6
      var txMbps = agg.txBps / 1e6
      setLiveValues(rxMbps, txMbps, agg.rxBytes, agg.txBytes)

      appendChartPoint(trafficChart, chartHistory, new Date().toISOString(), rxMbps, txMbps)
      appendChartPoint(
        byteChart,
        byteChartHistory,
        new Date().toISOString(),
        agg.rxBytes / (1024 * 1024 * 1024),
        agg.txBytes / (1024 * 1024 * 1024)
      )
      appendChartPoint(
        packetChart,
        packetChartHistory,
        new Date().toISOString(),
        agg.rxPackets / 1000000,
        agg.txPackets / 1000000
      )

      updateTrafficInsights(rxMbps, txMbps, true)

    }



    if (ifaceAllEl && !hideIfaceUi) {

      ifaceAllEl.addEventListener('change', onChartSourceChanged)

    }

    if (ifaceListEl && !hideIfaceUi) {

      ifaceListEl.addEventListener('change', function (e) {

        var t = e.target

        if (t && t.getAttribute && t.getAttribute('data-iface') && ifaceAllEl) {

          ifaceAllEl.checked = false

        }

        onChartSourceChanged()

      })

    }

    if (ifaceSearchEl && !hideIfaceUi) {

      ifaceSearchEl.addEventListener('input', applyIfaceSearchFilter)

    }

    if (graphTogglesHostEl) {
      graphTogglesHostEl.addEventListener('click', function (e) {
        var target = e.target
        if (!target || !target.getAttribute) return
        var key = target.getAttribute('data-graph-toggle')
        if (!key) return
        var current = normalizeGraphVisibility(trafficPrefs.graphVisibility)
        current[key] = !current[key]
        trafficPrefs.graphVisibility = current
        saveTrafficPrefs(trafficPrefs)
        applyGraphVisibility()
      })
      applyGraphVisibility()
    }

    if (window.bootstrap && typeof window.bootstrap.Tooltip === 'function') {

      var tooltipTriggers = document.querySelectorAll('[data-bs-toggle="tooltip"]')

      for (var tt = 0; tt < tooltipTriggers.length; tt++) {

        window.bootstrap.Tooltip.getOrCreateInstance(tooltipTriggers[tt])

      }

    }

    // تم إلغاء ميزة إعادة التفضيلات والاختصار Shift+R من واجهة المدير.



    if (!isClientPortal) {

      selectEl.addEventListener('change', function () {

        var metas = getSelectedMetas(selectEl)
        saveCurrentServerPreference(metas.length ? metas[0] : null)
        void startFor(metas)

      })

    }

    function applyServerSelectionAndReload() {
      var metas = getSelectedMetas(selectEl)
      saveCurrentServerPreference(metas.length ? metas[0] : null)
      void startFor(metas)
    }

    if (selectAllServersBtnEl && selectEl && !isClientPortal) {
      selectAllServersBtnEl.addEventListener('click', function () {
        for (var i = 0; i < selectEl.options.length; i++) {
          selectEl.options[i].selected = true
        }
        applyServerSelectionAndReload()
      })
    }

    if (clearAllServersBtnEl && selectEl && !isClientPortal) {
      clearAllServersBtnEl.addEventListener('click', function () {
        for (var i = 0; i < selectEl.options.length; i++) {
          selectEl.options[i].selected = false
        }
        applyServerSelectionAndReload()
      })
    }



    if (opt.initialServerId != null) {

      selectEl.value = String(opt.initialServerId)

    } else if (trafficPrefs.lastServerId != null && !isClientPortal) {

      selectEl.value = String(trafficPrefs.lastServerId)

    } else if (!isClientPortal) {

      selectEl.value = ''

    }

    updateChartOverlay()
    updateActiveSourceName(getEffectiveSourceSpec())

    updateInterfaceKpis([])

    var initial = getSelectedMetas(selectEl)
    saveCurrentServerPreference(initial.length ? initial[0] : null)
    void startFor(initial)

  }

})()

