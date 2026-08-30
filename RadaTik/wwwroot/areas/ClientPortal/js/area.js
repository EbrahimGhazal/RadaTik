/**
 * ClientPortal Area JS
 * Place ClientPortal-only behaviors here.
 */
(function () {
    'use strict';

    function updateUnreadBadge(count) {
        var badge = document.getElementById('clientPortalUnreadNotificationsBadge');
        if (!badge) {
            return;
        }

        if (!count || count <= 0) {
            badge.style.display = 'none';
            badge.textContent = '0';
            return;
        }

        badge.textContent = count > 99 ? '99+' : String(count);
        badge.style.display = 'inline-flex';
    }

    async function loadUnreadNotificationsCount() {
        try {
            var response = await fetch('/clientPortal/UnreadNotificationsCount', {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                return;
            }

            var data = await response.json();
            updateUnreadBadge(Number(data && data.count ? data.count : 0));
        } catch (error) {
            // Ignore transient errors and retry on next poll.
        }
    }

    if (document.getElementById('clientPortalUnreadNotificationsBadge')) {
        loadUnreadNotificationsCount();
        window.setInterval(loadUnreadNotificationsCount, 60000);
    }

    var capacitorApp = window.Capacitor && window.Capacitor.Plugins && window.Capacitor.Plugins.App;
    if (capacitorApp && capacitorApp.addListener) {
        capacitorApp.addListener('backButton', function (event) {
            if (event && event.canGoBack) {
                window.history.back();
                return;
            }
            if (capacitorApp.exitApp) {
                capacitorApp.exitApp();
            }
        });
    }
})();

