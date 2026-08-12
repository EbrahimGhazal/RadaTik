/**
 * RadaTik - Theme Management
 * إدارة الوضع الداكن/الفاتح
 */

(function() {
    'use strict';

    const ThemeManager = {
        init: function() {
            this.themeToggle = document.getElementById('themeToggle');
            this.themeIcon = document.getElementById('themeIcon');
            this.html = document.documentElement;

            /* يُطبَّق دائمًا ليتوافق مع _ThemeHeadBootstrap ويحدّث الأيقونة عند وجودها */
            this.applyTheme(this.getThemePreference());

            if (this.themeToggle) {
                this.themeToggle.addEventListener('click', () => this.toggleTheme());
            }
        },

        getThemePreference: function() {
            const saved = localStorage.getItem('theme');
            if (saved) return saved;
            
            if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                return 'dark';
            }
            
            return 'light';
        },

        applyTheme: function(theme) {
            this.html.setAttribute('data-theme', theme);
            localStorage.setItem('theme', theme);
            
            if (this.themeIcon) {
                this.themeIcon.className = theme === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
            }
        },

        toggleTheme: function() {
            const currentTheme = this.html.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            this.applyTheme(newTheme);
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => ThemeManager.init());
    } else {
        ThemeManager.init();
    }

    // Export for global access if needed
    window.ThemeManager = ThemeManager;
})();
