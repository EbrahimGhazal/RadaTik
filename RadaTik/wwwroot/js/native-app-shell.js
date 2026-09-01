/**
 * Capacitor shell: keep the session, and surface unread alerts on the app icon.
 */
(function () {
    'use strict';

    var capacitor = window.Capacitor;
    if (!capacitor || !capacitor.Plugins) {
        return;
    }

    var App = capacitor.Plugins.App;
    var LocalNotifications = capacitor.Plugins.LocalNotifications;
    var path = (window.location.pathname || '').toLowerCase();
    var countUrl = null;
    var openUrl = null;
    var lastCountKey = 'rt_native_unread_count';

    if (path.indexOf('/clientportal') === 0) {
        countUrl = '/clientPortal/UnreadNotificationsCount';
        openUrl = '/clientPortal/Notifications';
    } else if (path.indexOf('/employee') === 0 || path.indexOf('/companyemployee') === 0) {
        countUrl = '/employee/notifications/UnreadCount';
        openUrl = '/employee/notifications';
    } else if (path.indexOf('/collectionpoint') === 0) {
        countUrl = '/collectionPoint/UnreadNotificationsCount';
        openUrl = '/collectionPoint/dashboard';
    } else if (path.indexOf('/networkmanager') === 0 || path.indexOf('/companyadmin') === 0) {
        countUrl = '/networkManager/notifications/UnreadCount';
        openUrl = '/networkManager/notifications';
    }

    if (App && App.addListener) {
        App.addListener('backButton', function (event) {
            if (event && event.canGoBack) {
                window.history.back();
                return;
            }

            if (App.minimizeApp) {
                App.minimizeApp();
                return;
            }

            if (App.exitApp) {
                App.exitApp();
            }
        });
    }

    if (!countUrl || !LocalNotifications) {
        return;
    }

    function readLastCount() {
        var raw = window.localStorage.getItem(lastCountKey);
        var value = Number(raw);
        return Number.isFinite(value) ? value : 0;
    }

    async function applyIconBadge(count) {
        try {
            if (count <= 0) {
                await LocalNotifications.cancel({ notifications: [{ id: 7101 }] });
                return;
            }

            await LocalNotifications.schedule({
                notifications: [{
                    id: 7101,
                    title: 'RadaTik',
                    body: count === 1 ? 'لديك تنبيه جديد' : 'لديك ' + count + ' تنبيهات غير مقروءة',
                    extra: { openUrl: openUrl },
                    smallIcon: 'ic_notification',
                    iconColor: '#0f766e',
                    badge: count,
                    autoCancel: false,
                    ongoing: false
                }]
            });
        } catch (error) {
            // Permission or plugin gaps should not break the portal.
        }
    }

    async function syncUnread() {
        try {
            var response = await fetch(countUrl, {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!response.ok) {
                return;
            }

            var data = await response.json();
            var count = Number(data && data.count ? data.count : 0);
            if (!Number.isFinite(count) || count < 0) {
                count = 0;
            }

            var previous = readLastCount();
            window.localStorage.setItem(lastCountKey, String(count));
            if (count > 0 || previous > 0) {
                await applyIconBadge(count);
            }
        } catch (error) {
            // Retry on the next interval.
        }
    }

    async function start() {
        try {
            if (LocalNotifications.requestPermissions) {
                await LocalNotifications.requestPermissions();
            }
        } catch (error) {
            return;
        }

        await syncUnread();
        window.setInterval(syncUnread, 30000);

        if (App && App.addListener) {
            App.addListener('appStateChange', function (state) {
                if (state && state.isActive) {
                    syncUnread();
                }
            });
        }

        if (LocalNotifications.addListener) {
            LocalNotifications.addListener('localNotificationActionPerformed', function (event) {
                var target = event && event.notification && event.notification.extra && event.notification.extra.openUrl;
                if (target && window.location.pathname.toLowerCase().indexOf(target.toLowerCase()) !== 0) {
                    window.location.assign(target);
                }
            });
        }
    }

    start();
})();
