/**
 * CompanyEmployee Area JS
 * Place CompanyEmployee-only behaviors here.
 */
(function () {
    'use strict';

    function updateUnreadNotificationsBadge() {
        var badge = document.getElementById('employeeNotificationsUnreadBadge');
        if (!badge) return;

        fetch('/employee/Notifications/UnreadCount', {
            method: 'GET',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        })
            .then(function (response) {
                if (!response.ok) throw new Error('Failed to fetch unread notifications count');
                return response.json();
            })
            .then(function (data) {
                var count = Number(data && data.count ? data.count : 0);
                if (count > 0) {
                    badge.textContent = count > 99 ? '99+' : String(count);
                    badge.style.display = 'inline-flex';
                } else {
                    badge.style.display = 'none';
                }
            })
            .catch(function () {
                // keep sidebar stable on any transient request failure
            });
    }

    document.addEventListener('DOMContentLoaded', function () {
        updateUnreadNotificationsBadge();
        setInterval(updateUnreadNotificationsBadge, 30000);
    });
})();
