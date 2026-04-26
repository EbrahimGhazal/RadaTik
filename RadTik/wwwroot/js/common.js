/**
 * RadTik - Common Utilities
 * وظائف مشتركة
 */

(function() {
    'use strict';

    const CommonUtils = {
        /**
         * Format date to Arabic format
         */
        formatDate: function(date, includeTime = false) {
            if (!date) return '';
            
            const d = new Date(date);
            const options = {
                year: 'numeric',
                month: 'long',
                day: 'numeric',
                calendar: 'gregory',
                numberingSystem: 'arab'
            };

            if (includeTime) {
                options.hour = '2-digit';
                options.minute = '2-digit';
            }

            return new Intl.DateTimeFormat('ar-SA', options).format(d);
        },

        /**
         * Format number with Arabic separators
         */
        formatNumber: function(number, decimals = 2) {
            if (number === null || number === undefined) return '0';
            return new Intl.NumberFormat('ar-SA', {
                minimumFractionDigits: decimals,
                maximumFractionDigits: decimals
            }).format(number);
        },

        /**
         * Show toast notification
         */
        showToast: function(message, type = 'info', duration = 3000) {
            const toast = document.createElement('div');
            toast.className = `toast toast-${type}`;
            toast.textContent = message;
            
            // Add styles
            Object.assign(toast.style, {
                position: 'fixed',
                top: '20px',
                left: '20px',
                padding: '12px 24px',
                borderRadius: '8px',
                color: 'white',
                zIndex: '10000',
                animation: 'slideIn 0.3s ease',
                boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
            });

            // Set background color based on type
            const colors = {
                success: '#00C853',
                error: '#F44336',
                warning: '#FF9800',
                info: '#2196F3'
            };
            toast.style.background = colors[type] || colors.info;

            document.body.appendChild(toast);

            setTimeout(() => {
                toast.style.animation = 'slideOut 0.3s ease';
                setTimeout(() => toast.remove(), 300);
            }, duration);
        },

        /**
         * Confirm dialog
         */
        confirm: function(message, callback) {
            if (window.confirm(message)) {
                callback();
            }
        },

        /**
         * Debounce function
         */
        debounce: function(func, wait) {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        },

        /**
         * Update pending requests count
         */
        updatePendingRequestsCount: function(url) {
            if (!url) return;

            fetch(url)
                .then(response => response.json())
                .then(data => {
                    const badge = document.getElementById('notificationBadge');
                    const sidebarBadge = document.getElementById('pendingRequestsBadge');
                    
                    if (badge) {
                        const notificationBtn = document.getElementById('notificationBtn');
                        if (data.total > 0) {
                            badge.textContent = data.total > 99 ? '99+' : String(data.total);
                            badge.style.display = 'flex';
                            if (notificationBtn) notificationBtn.classList.add('has-notifications');
                        } else {
                            badge.style.display = 'none';
                            if (notificationBtn) notificationBtn.classList.remove('has-notifications');
                        }
                    }
                    
                    if (sidebarBadge) {
                        if (data.total > 0) {
                            sidebarBadge.textContent = data.total > 99 ? '99+' : String(data.total);
                            sidebarBadge.style.display = 'inline-flex';
                        } else {
                            sidebarBadge.style.display = 'none';
                        }
                    }
                })
                .catch(error => console.log('Error fetching pending requests:', error));
        },

        /**
         * Initialize Select2 for all selects
         */
        initSelect2: function() {
            if (typeof $ !== 'undefined' && $.fn.select2) {
                $('select.form-select, select.form-control').not('.no-select2').each(function() {
                    if (!$(this).hasClass('select2-hidden-accessible')) {
                        $(this).select2({
                            theme: 'bootstrap-5',
                            dir: 'rtl',
                            width: '100%',
                            allowClear: true
                        });
                    }
                });
            }
        }
    };

    // Add CSS animations for toast
    if (!document.getElementById('toast-styles')) {
        const style = document.createElement('style');
        style.id = 'toast-styles';
        style.textContent = `
            @keyframes slideIn {
                from {
                    transform: translateX(-100%);
                    opacity: 0;
                }
                to {
                    transform: translateX(0);
                    opacity: 1;
                }
            }
            @keyframes slideOut {
                from {
                    transform: translateX(0);
                    opacity: 1;
                }
                to {
                    transform: translateX(-100%);
                    opacity: 0;
                }
            }
        `;
        document.head.appendChild(style);
    }

    // Export for global access
    window.CommonUtils = CommonUtils;

    // Auto-initialize Select2 when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            CommonUtils.initSelect2();
        });
    } else {
        CommonUtils.initSelect2();
    }
})();
