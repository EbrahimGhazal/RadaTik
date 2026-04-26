/**
 * RadTik - Layout Scripts
 * سكريبتات التخطيط العام
 */

(function() {
    'use strict';

    const LayoutManager = {
        init: function() {
            this.setupSidebar();
            this.setupSidebarCollapse();
            this.setupMobileMenu();
            this.setupNavDropdowns();
            this.setupSectionDropdowns();
            this.setupSectionStateReset();
            this.setupSystemAdminNotifications();
            this.setupDropdowns();
            this.setupNetworkSelector();
            this.setupResponsiveTables();
        },

        setupSidebar: function() {
            const sidebar = document.getElementById('sidebar');
            const mainContent = document.querySelector('.main-content');
            if (!sidebar || !mainContent) return;
            try {
                const stored = localStorage.getItem('sidebarCollapsed');
                if (stored === 'true' && window.innerWidth > 992) {
                    sidebar.classList.add('collapsed');
                    mainContent.classList.add('sidebar-collapsed');
                }
            } catch (_) {}
        },

        setupSidebarCollapse: function() {
            const btn = document.getElementById('sidebarCollapseBtn');
            const icon = document.getElementById('sidebarCollapseIcon');
            const sidebar = document.getElementById('sidebar');
            const mainContent = document.querySelector('.main-content');

            function updateIcon() {
                if (!icon) return;
                const collapsed = sidebar && sidebar.classList.contains('collapsed');
                icon.classList.remove('fa-chevron-right', 'fa-chevron-left');
                icon.classList.add(collapsed ? 'fa-chevron-left' : 'fa-chevron-right');
            }

            if (btn && sidebar && mainContent) {
                updateIcon();
                btn.addEventListener('click', (e) => {
                    e.preventDefault();
                    // On tablets/mobiles we keep sidebar expanded (no mini mode)
                    if (window.innerWidth <= 992) return;
                    sidebar.classList.toggle('collapsed');
                    mainContent.classList.toggle('sidebar-collapsed');
                    updateIcon();
                    try {
                        localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
                    } catch (_) {}
                });
            }
        },

        setupMobileMenu: function() {
            const sidebarToggle = document.getElementById('sidebarToggle');
            const sidebar = document.getElementById('sidebar');

            if (sidebarToggle && sidebar) {
                sidebarToggle.addEventListener('click', (e) => {
                    e.preventDefault();
                    if (window.innerWidth <= 992) {
                        sidebar.classList.toggle('active');
                    } else {
                        sidebar.classList.toggle('collapsed');
                        const main = document.querySelector('.main-content');
                        if (main) main.classList.toggle('sidebar-collapsed');
                    }
                });

                document.addEventListener('click', (e) => {
                    if (window.innerWidth <= 992) {
                        if (!sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                            sidebar.classList.remove('active');
                        }
                    }
                });
            }
        },

        setupNavDropdowns: function() {
            document.querySelectorAll('.nav-item.has-dropdown').forEach(function(item) {
                const toggle = item.querySelector('.nav-link-toggle');
                const sublist = item.querySelector('.nav-sublist');
                if (!toggle || !sublist) return;

                const hasActive = sublist.querySelector('.nav-link-sub.active');
                if (hasActive) item.classList.add('open');

                toggle.addEventListener('click', function(e) {
                    e.preventDefault();
                    const collapsed = document.getElementById('sidebar')?.classList.contains('collapsed');
                    if (collapsed) return;
                    item.classList.toggle('open');
                });
            });
        },

        // Make every sidebar section collapsible (dropdown-like)
        setupSectionDropdowns: function() {
            const sidebar = document.getElementById('sidebar');
            const navRoot = sidebar ? sidebar.querySelector('.sidebar-nav') : null;
            const isSingleOpenMode = navRoot?.dataset?.singleOpen === 'true';

            document.querySelectorAll('.nav-section').forEach(function(section) {
                const toggle = section.querySelector('.nav-section-title.nav-section-toggle');
                const list = section.querySelector('ul.nav-list');
                if (!toggle || !list) return;

                // Persist section open/close state per area (scope) using localStorage.
                // Default behavior: collapse all sections, then auto-open only the one containing the active link.
                const getScope = () => {
                    try {
                        const seg = (window.location.pathname || '').split('/').filter(Boolean)[0];
                        return seg || 'root';
                    } catch (_) {
                        return 'root';
                    }
                };

                const getSectionKey = () => {
                    const explicit = section.getAttribute('data-section-key');
                    if (explicit) return explicit;
                    const titleSpan = toggle.querySelector('span');
                    const title = (titleSpan?.textContent || toggle.textContent || '').trim();
                    const normalizedTitle = title.replace(/\s+/g, ' ');
                    const firstLink = list.querySelector('a.nav-link, a.nav-link-sub');
                    const firstHref = (firstLink?.getAttribute('href') || '').trim();
                    return firstHref ? `${normalizedTitle}::${firstHref}` : normalizedTitle;
                };

                const scope = getScope();
                const sectionKey = getSectionKey();
                const storageKey = `sidebarSectionState:${scope}:${sectionKey}`;

                const hasActive = !!list.querySelector('.nav-link.active, .nav-link-sub.active');

                // In single-open mode (e.g., SystemAdmin), only the active section starts open.
                if (isSingleOpenMode) {
                    if (hasActive) {
                        section.classList.remove('section-collapsed');
                    } else {
                        section.classList.add('section-collapsed');
                    }
                } else {
                    // Initial state: stored state wins, but active section is always opened for discoverability.
                    try {
                        const stored = localStorage.getItem(storageKey); // 'open' | 'collapsed' | null
                        if (stored === 'open') {
                            section.classList.remove('section-collapsed');
                        } else if (stored === 'collapsed') {
                            section.classList.add('section-collapsed');
                        } else {
                            section.classList.add('section-collapsed');
                        }
                    } catch (_) {
                        section.classList.add('section-collapsed');
                    }

                    if (hasActive) section.classList.remove('section-collapsed');
                }

                const setAria = () => {
                    toggle.setAttribute('aria-expanded', (!section.classList.contains('section-collapsed')).toString());
                };
                setAria();

                const doToggle = (e) => {
                    if (e) e.preventDefault();
                    // When sidebar is collapsed (mini), don't toggle sections
                    if (sidebar && sidebar.classList.contains('collapsed')) return;
                    const willOpen = section.classList.contains('section-collapsed');

                    if (isSingleOpenMode && willOpen) {
                        document.querySelectorAll('.nav-section').forEach((otherSection) => {
                            if (otherSection === section) return;
                            otherSection.classList.add('section-collapsed');
                            const otherToggle = otherSection.querySelector('.nav-section-title.nav-section-toggle');
                            if (otherToggle) otherToggle.setAttribute('aria-expanded', 'false');
                        });
                    }

                    section.classList.toggle('section-collapsed');
                    setAria();
                    if (!isSingleOpenMode) {
                        try {
                            localStorage.setItem(storageKey, section.classList.contains('section-collapsed') ? 'collapsed' : 'open');
                        } catch (_) {}
                    }
                };

                toggle.addEventListener('click', doToggle);
                toggle.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        doToggle(e);
                    }
                });
            });
        },

        // Reset saved open/close state for sections (localStorage)
        setupSectionStateReset: function() {
            document.querySelectorAll('.sidebar-sections-reset-btn').forEach(function(btn) {
                btn.addEventListener('click', function(e) {
                    e.preventDefault();

                    const ok = window.confirm('هل تريد إعادة ضبط الأقسام في القائمة الجانبية؟');
                    if (!ok) return;

                    let scope = 'root';
                    try {
                        const seg = (window.location.pathname || '').split('/').filter(Boolean)[0];
                        scope = seg || 'root';
                    } catch (_) {}

                    const prefix = `sidebarSectionState:${scope}:`;
                    const keysToRemove = [];
                    try {
                        for (let i = 0; i < localStorage.length; i++) {
                            const k = localStorage.key(i);
                            if (!k) continue;
                            if (k.startsWith(prefix)) keysToRemove.push(k);
                        }
                        keysToRemove.forEach(k => localStorage.removeItem(k));
                    } catch (_) {
                        // Fallback: remove all section state keys
                        try {
                            for (let i = 0; i < localStorage.length; i++) {
                                const k = localStorage.key(i);
                                if (!k) continue;
                                if (k.startsWith('sidebarSectionState:')) keysToRemove.push(k);
                            }
                            keysToRemove.forEach(k => localStorage.removeItem(k));
                        } catch (_) {}
                    }

                    window.location.reload();
                });
            });
        },

        // SystemAdmin: update bell + sidebar badges for pending requests
        setupSystemAdminNotifications: function() {
            const btn = document.getElementById('notificationBtn');
            const badge = document.getElementById('notificationBadge');
            const dropdown = document.getElementById('notificationDropdown');
            const dropdownBody = document.getElementById('notificationDropdownBody');
            const viewAllLink = document.querySelector('.notification-dropdown-view-all');
            const url = btn?.dataset?.pendingCountsUrl;
            if (!url) return;

            const setBadge = (el, value) => {
                if (!el) return;
                const isHeaderNotificationBadge = el.id === 'notificationBadge';
                const headerNotificationBtn = isHeaderNotificationBadge ? document.getElementById('notificationBtn') : null;
                if (value > 0) {
                    el.textContent = value > 99 ? '99+' : String(value);
                    el.style.display = 'inline-flex';
                    if (headerNotificationBtn) headerNotificationBtn.classList.add('has-notifications');
                } else {
                    el.style.display = 'none';
                    if (headerNotificationBtn) headerNotificationBtn.classList.remove('has-notifications');
                }
            };

            const renderDropdown = (latest) => {
                if (!dropdownBody) return;
                const items = Array.isArray(latest) ? latest : [];
                if (items.length === 0) {
                    dropdownBody.innerHTML = '<div class="notification-dropdown-empty">لا توجد إشعارات جديدة</div>';
                    return;
                }

                dropdownBody.innerHTML = items.map((item) => {
                    const safeCount = Number(item.count || 0);
                    const countLabel = safeCount > 99 ? '99+' : String(safeCount);
                    const href = item.url || '#';
                    const title = item.title || 'إشعار';
                    const icon = item.icon || 'fa-bell';
                    return `
                        <a class="notification-dropdown-item" href="${href}">
                            <span class="notification-dropdown-icon"><i class="fas ${icon}"></i></span>
                            <span class="notification-dropdown-content">
                                <span class="notification-dropdown-title">${title}</span>
                                <span class="notification-dropdown-meta">قيد الانتظار: ${countLabel}</span>
                            </span>
                            <span class="notification-dropdown-count">${countLabel}</span>
                        </a>
                    `;
                }).join('');
            };

            const markDropdownItemsViewed = () => {
                if (!dropdownBody) return;
                dropdownBody.querySelectorAll('.notification-dropdown-item').forEach((item) => {
                    item.classList.add('is-viewed');
                });
            };

            const update = () => {
                fetch(url, { headers: { 'Accept': 'application/json' } })
                    .then(r => r.ok ? r.json() : Promise.reject(new Error('Bad response')))
                    .then(data => {
                        setBadge(badge, data?.total || 0);
                        setBadge(document.getElementById('sysJoinRequestsBadge'), data?.joinRequests || 0);
                        setBadge(document.getElementById('sysServiceRequestsBadge'), data?.serviceRequests || 0);
                        // consolidated funding requests (companies + collection points)
                        const companyTopUps = data?.topUpRequests || 0;
                        const pointTopUps = data?.collectionPointTopUps || 0;
                        setBadge(document.getElementById('sysFundingRequestsBadge'), companyTopUps + pointTopUps);
                        setBadge(document.getElementById('sysPasswordResetRequestsBadge'), data?.passwordResetRequests || 0);
                        // Section-level badges (show request counts on tab headers too)
                        setBadge(document.getElementById('sysOpsSectionBadge'), data?.operationsTotal || 0);
                        setBadge(document.getElementById('sysFinanceSectionBadge'), data?.financeTotal || 0);
                        setBadge(document.getElementById('sysServicesSectionBadge'), data?.servicesTotal || 0);
                        setBadge(document.getElementById('sysCompaniesSectionBadge'), data?.companiesTotal || 0);

                        const servicesNewBadge = document.getElementById('sysServicesNewBadge');
                        const newServicesToPrice = Number(data?.newServicesToPrice || 0);
                        if (servicesNewBadge) {
                            if (newServicesToPrice > 0) {
                                servicesNewBadge.textContent = 'جديد';
                                servicesNewBadge.style.display = 'inline-flex';
                                servicesNewBadge.classList.add('nav-badge-new');
                                servicesNewBadge.title = `يوجد ${newServicesToPrice} خدمة جديدة تحتاج تسعير`;
                                servicesNewBadge.setAttribute('aria-label', `يوجد ${newServicesToPrice} خدمة جديدة تحتاج تسعير`);
                            } else {
                                servicesNewBadge.style.display = 'none';
                                servicesNewBadge.classList.remove('nav-badge-new');
                                servicesNewBadge.title = '';
                                servicesNewBadge.removeAttribute('aria-label');
                            }
                        }
                        renderDropdown(data?.latest);
                        if (viewAllLink && btn?.dataset?.viewAllUrl) {
                            viewAllLink.setAttribute('href', btn.dataset.viewAllUrl);
                        }
                    })
                    .catch(() => { /* ignore */ });
            };

            document.addEventListener('DOMContentLoaded', update);
            update();
            window.setInterval(update, 30000);

            if (dropdown) {
                const observer = new MutationObserver(() => {
                    if (dropdown.classList.contains('show')) {
                        markDropdownItemsViewed();
                    }
                });
                observer.observe(dropdown, { attributes: true, attributeFilter: ['class'] });
            }
        },

        setupDropdowns: function() {
            // قائمة المستخدم: Bootstrap 5 (data-bs-toggle) + Popper — لا تُفتح يدوياً هنا.

            // إشعارات مدير النظام: قائمة مخصّصة (بدون data-bs-toggle) حتى لا يتعارض مع Bootstrap
            const dropdowns = document.querySelectorAll('.header-notification-wrapper .dropdown-toggle');

            dropdowns.forEach((dropdown) => {
                dropdown.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    const menu = dropdown.nextElementSibling;
                    if (!menu || !menu.classList.contains('dropdown-menu')) return;

                    // أغلق قائمة المستخدم (Bootstrap) إن وُجدت
                    document.querySelectorAll('.header [data-bs-toggle="dropdown"]').forEach((t) => {
                        try {
                            const inst = window.bootstrap?.Dropdown?.getInstance(t);
                            if (inst) inst.hide();
                        } catch (_) {}
                    });

                    document.querySelectorAll('.header-notification-wrapper .dropdown-menu').forEach((m) => {
                        if (m !== menu) {
                            m.style.display = 'none';
                            m.classList.remove('show');
                        }
                    });

                    const isVisible = menu.classList.contains('show');
                    if (isVisible) {
                        menu.style.display = 'none';
                        menu.classList.remove('show');
                    } else {
                        menu.style.display = 'block';
                        menu.classList.add('show');
                    }
                });
            });

            document.addEventListener('click', (e) => {
                if (!e.target.closest('.header-notification-wrapper')) {
                    const notificationDropdown = document.getElementById('notificationDropdown');
                    if (notificationDropdown) {
                        notificationDropdown.style.display = 'none';
                        notificationDropdown.classList.remove('show');
                    }
                }
            });

            const userToggle = document.getElementById('userDropdownToggle');
            if (userToggle) {
                userToggle.addEventListener('show.bs.dropdown', () => {
                    const notificationDropdown = document.getElementById('notificationDropdown');
                    if (notificationDropdown) {
                        notificationDropdown.style.display = 'none';
                        notificationDropdown.classList.remove('show');
                    }
                });
            }
        },

        setupNetworkSelector: function() {
            const networkSelector = document.getElementById('networkSelector');
            
            if (networkSelector) {
                networkSelector.addEventListener('change', function() {
                    const networkId = this.value;
                    if (networkId) {
                        // Store in session via AJAX
                        fetch('/Network/SetCurrentNetwork', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                            },
                            body: JSON.stringify({ networkId: parseInt(networkId) })
                        })
                        .then(response => {
                            if (response.ok) {
                                window.location.reload();
                            }
                        })
                        .catch(error => {
                            console.error('Error setting network:', error);
                        });
                    }
                });
            }
        },

        setupResponsiveTables: function() {
            const decorateTable = (table) => {
                if (!table || table.dataset.responsiveReady === '1') return;
                table.dataset.responsiveReady = '1';
                table.classList.add('table-responsive-stack');

                const headerCells = Array.from(table.querySelectorAll('thead th'));
                if (!headerCells.length) return;

                const headerLabels = headerCells.map((th, idx) => {
                    const explicit = th.getAttribute('data-label');
                    const text = (explicit || th.textContent || '').trim();
                    return text || `#${idx + 1}`;
                });

                table.querySelectorAll('tbody tr').forEach((row) => {
                    Array.from(row.children).forEach((cell, index) => {
                        if (!cell.getAttribute('data-label')) {
                            cell.setAttribute('data-label', headerLabels[index] || '');
                        }
                    });
                });
            };

            document
                .querySelectorAll('table:not(.no-responsive-stack)')
                .forEach(decorateTable);
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => LayoutManager.init());
    } else {
        LayoutManager.init();
    }

    // Export for global access
    window.LayoutManager = LayoutManager;
})();
