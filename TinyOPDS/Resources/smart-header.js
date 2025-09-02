// Smart header behavior for mobile devices
(function () {
    'use strict';

    // Check if we're on mobile
    function isMobile() {
        return window.innerWidth <= 768;
    }

    // Initialize on DOM ready
    function init() {
        if (!isMobile()) return;

        var header = document.querySelector('.fixed-header');
        if (!header) return;

        var lastScrollTop = 0;
        var isHeaderHidden = false;
        var idleTimer = null;
        var touchStartY = 0;
        var scrollThreshold = 10;
        var hideThreshold = 30;

        // Add transition style if not present
        if (!header.style.transition) {
            header.style.transition = 'transform 0.1s ease-out';
        }

        // Function to show header
        function showHeader() {
            if (!isHeaderHidden) return;
            header.style.transform = 'translateY(0)';
            isHeaderHidden = false;
            resetIdleTimer();
        }

        // Function to hide header
        function hideHeader() {
            if (isHeaderHidden) return;
            header.style.transform = 'translateY(-100%)';
            isHeaderHidden = true;
        }

        // Reset idle timer
        function resetIdleTimer() {
            clearTimeout(idleTimer);
            idleTimer = setTimeout(function () {
                if (window.scrollY > hideThreshold) {
                    hideHeader();
                }
            }, 5000);
        }

        // Optimized scroll handler
        var ticking = false;
        function handleScroll() {
            var currentScroll = window.scrollY;

            // Skip small movements
            if (Math.abs(currentScroll - lastScrollTop) < scrollThreshold) {
                return;
            }

            if (currentScroll > lastScrollTop && currentScroll > hideThreshold) {
                // Scrolling down
                hideHeader();
            } else if (currentScroll < lastScrollTop || currentScroll < 20) {
                // Scrolling up or near top
                showHeader();
            }

            lastScrollTop = currentScroll <= 0 ? 0 : currentScroll;
        }

        // Add scroll listener
        window.addEventListener('scroll', function () {
            if (!ticking) {
                window.requestAnimationFrame(function () {
                    handleScroll();
                    ticking = false;
                });
                ticking = true;
            }
        }, { passive: true });

        // Touch handling
        document.addEventListener('touchstart', function (e) {
            if (e.touches && e.touches[0]) {
                touchStartY = e.touches[0].clientY;
            }
        }, { passive: true });

        document.addEventListener('touchend', function (e) {
            if (e.changedTouches && e.changedTouches[0]) {
                var touchEndY = e.changedTouches[0].clientY;
                // Quick tap shows header
                if (Math.abs(touchEndY - touchStartY) < 10 && isHeaderHidden) {
                    showHeader();
                }
            }
            resetIdleTimer();
        }, { passive: true });

        // Reset timer on activity
        document.addEventListener('touchmove', resetIdleTimer, { passive: true });

        // Click shows header
        document.addEventListener('click', function () {
            if (isHeaderHidden) showHeader();
        });

        // Handle orientation change
        window.addEventListener('orientationchange', function () {
            setTimeout(function () {
                if (!isMobile()) {
                    // Reset if no longer mobile
                    header.style.transform = '';
                    isHeaderHidden = false;
                    clearTimeout(idleTimer);
                }
            }, 100);
        });

        // Initialize
        resetIdleTimer();
        showHeader();
    }

    // Start when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();