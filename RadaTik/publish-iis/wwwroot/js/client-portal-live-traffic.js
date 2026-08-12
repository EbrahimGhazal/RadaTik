/* global signalR */
(function () {
  'use strict'

  function hubUrl() {
    return window.location.origin + '/hubs/traffic'
  }

  function formatBps(bps) {
    if (typeof bps !== 'number' || !isFinite(bps) || bps < 0) return '—'
    if (bps < 1000) return Math.round(bps) + ' bps'
    if (bps < 1e6) return (bps / 1e3).toFixed(1) + ' Kbps'
    return (bps / 1e6).toFixed(2) + ' Mbps'
  }

  function computeMbpsForClient(interfaces, pppUser) {
    if (!interfaces || !interfaces.length) return { rx: 0, tx: 0 }
    var u = String(pppUser || '')
      .trim()
      .toLowerCase()
    if (!u) return { rx: 0, tx: 0 }
    var j
    for (j = 0; j < interfaces.length; j++) {
      var nm = (interfaces[j].name || '').toLowerCase()
      if (nm === u) {
        return {
          rx: (interfaces[j].rxBps || 0) / 1e6,
          tx: (interfaces[j].txBps || 0) / 1e6,
        }
      }
    }
    for (j = 0; j < interfaces.length; j++) {
      var nm2 = (interfaces[j].name || '').toLowerCase()
      if (nm2.indexOf(u) >= 0 || u.indexOf(nm2) >= 0) {
        return {
          rx: (interfaces[j].rxBps || 0) / 1e6,
          tx: (interfaces[j].txBps || 0) / 1e6,
        }
      }
    }
    return { rx: 0, tx: 0 }
  }

  /**
   * @param {{
   *   networkId: number,
   *   serverId: number,
   *   pppUser: string,
   *   rxId?: string,
   *   txId?: string,
   *   statusId?: string
   * }} cfg
   */
  window.initClientPortalLiveTraffic = function (cfg) {
    if (!cfg || !cfg.networkId || !cfg.serverId) return

    var rxEl = document.getElementById(cfg.rxId || 'portal-live-rx')
    var txEl = document.getElementById(cfg.txId || 'portal-live-tx')
    var statusEl = document.getElementById(cfg.statusId || 'portal-live-status')

    function setStatus(text, ok) {
      if (!statusEl) return
      statusEl.textContent = text
      statusEl.classList.toggle('text-success', !!ok)
      statusEl.classList.toggle('text-warning', ok === false)
    }

    var connection = new signalR.HubConnectionBuilder()
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
      var list = payload && payload.interfaces ? payload.interfaces : []
      var m = computeMbpsForClient(list, cfg.pppUser)
      if (rxEl) rxEl.textContent = formatBps(m.rx * 1e6)
      if (txEl) txEl.textContent = formatBps(m.tx * 1e6)
      setStatus('متصل — تحديث لحظي', true)
    })

    connection.on('trafficError', function (err) {
      setStatus((err && err.message) || 'خطأ في بيانات الترافك', false)
    })

    connection.onreconnecting(function () {
      setStatus('إعادة الاتصال…', false)
    })

    connection.onreconnected(function () {
      setStatus('متصل', true)
    })

    connection.onclose(function () {
      setStatus('انقطع الاتصال — سيتم إعادة المحاولة تلقائياً', false)
    })

    connection
      .start()
      .then(function () {
        return connection.invoke('JoinTraffic', cfg.networkId, cfg.serverId)
      })
      .then(function () {
        setStatus('متصل — بانتظار البيانات…', true)
      })
      .catch(function () {
        setStatus('تعذر الاتصال بالخادم. حدّث الصفحة أو جرّب لاحقاً.', false)
      })
  }
})()
