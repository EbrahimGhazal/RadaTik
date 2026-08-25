/**
 * RadaTik UI Kit — mobile tables, sidebar advanced toggle, data-label injection
 */
(function () {
    'use strict';

    function isMobileCardViewport() {
        return window.matchMedia && window.matchMedia('(max-width: 767.98px)').matches;
    }

    function enhanceMobileTables() {
        document.querySelectorAll('.radtk-data-table.radtk-data-table--cards, .radtk-surface table[data-radtk-mobile="cards"], .radtk-surface .radtk-auto-mobile-table').forEach(function (table) {
            if (!table.classList.contains('radtk-data-table')) {
                table.classList.add('radtk-data-table', 'radtk-data-table--cards');
            }
            var headers = [];
            table.querySelectorAll('thead th').forEach(function (th, index) {
                headers[index] = (th.textContent || '').trim();
            });
            table.querySelectorAll('tbody tr').forEach(function (row) {
                row.querySelectorAll('td').forEach(function (td, index) {
                    if (!td.getAttribute('data-label') && headers[index]) {
                        td.setAttribute('data-label', headers[index]);
                    }
                });
            });
        });
    }

    function neutralizeCardTableWidths() {
        if (!isMobileCardViewport()) {
            return;
        }

        document.querySelectorAll('.radtk-data-table--cards').forEach(function (table) {
            table.style.width = '100%';
            table.style.maxWidth = '100%';
            table.querySelectorAll('colgroup').forEach(function (group) {
                group.remove();
            });
            table.querySelectorAll('th, td, col').forEach(function (el) {
                el.style.width = '';
                el.style.minWidth = '';
                el.style.maxWidth = '';
            });

            var wrap = table.closest('.dataTables_wrapper, .dataTables_scroll, .table-responsive, .table-container, .radtk-data-table-wrap');
            if (wrap) {
                wrap.style.overflow = 'visible';
                wrap.style.maxWidth = '100%';
            }
        });
    }

    function enhanceSurfaceTables() {
        document.querySelectorAll('.radtk-surface .table-responsive table.table:not(.radtk-data-table)').forEach(function (table) {
            if (table.getAttribute('data-radtk-mobile') === 'cards' || table.classList.contains('radtk-auto-mobile-table')) {
                table.classList.add('radtk-data-table', 'radtk-data-table--cards');
            }
        });
    }

    function bindDataTablesHooks() {
        if (!window.jQuery) {
            return;
        }

        var $ = window.jQuery;
        $(document).on('init.dt draw.dt', function () {
            enhanceMobileTables();
            neutralizeCardTableWidths();
        });

        if ($.fn.dataTable) {
            $.extend(true, $.fn.dataTable.defaults, {
                autoWidth: false
            });
        }
    }

    function initAdvancedSidebarSections() {
        document.querySelectorAll('.nav-section-advanced .nav-section-title').forEach(function (title) {
            title.addEventListener('click', function () {
                var section = title.closest('.nav-section-advanced');
                if (!section) return;
                section.classList.toggle('is-expanded');
                var expanded = section.classList.contains('is-expanded');
                title.setAttribute('aria-expanded', expanded ? 'true' : 'false');
            });
        });
    }

    function initCollectionNetworkCards() {
        document.querySelectorAll('.radtk-collect-network-card[data-network-id]').forEach(function (card) {
            card.setAttribute('tabindex', '0');
            card.setAttribute('role', 'button');
            card.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    card.click();
                }
            });
        });
    }

    function init() {
        enhanceSurfaceTables();
        enhanceMobileTables();
        neutralizeCardTableWidths();
        bindDataTablesHooks();
        initAdvancedSidebarSections();
        initCollectionNetworkCards();
        window.addEventListener('resize', neutralizeCardTableWidths);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.RadtkUiKit = {
        enhanceMobileTables: enhanceMobileTables,
        enhanceSurfaceTables: enhanceSurfaceTables,
        neutralizeCardTableWidths: neutralizeCardTableWidths
    };
})();
