// Infinite scroll implementation for TinyOPDS web interface
(function () {
    'use strict';

    // Configuration
    const SCROLL_THRESHOLD = 1600;  // pixels from bottom to trigger load
    const DEBOUNCE_DELAY = 100;     // ms to debounce scroll events

    // State management
    let isLoading = false;
    let hasMorePages = true;
    let currentRequest = null;
    let debounceTimer = null;

    // DOM elements
    let booksContainer;
    let loadingIndicator;
    let endMessage;
    let errorMessage;

    // Initialize when DOM is ready
    function init() {
        // Find containers
        booksContainer = document.getElementById('books-container');
        loadingIndicator = document.getElementById('loading-indicator');
        endMessage = document.getElementById('end-message');
        errorMessage = document.getElementById('error-message');

        // Only initialize if we have a books container with pagination
        if (!booksContainer) return;

        const nextPageUrl = booksContainer.getAttribute('data-next-page');
        if (!nextPageUrl || nextPageUrl === '') {
            // No next page, show end message if there are books
            if (booksContainer.children.length > 0) {
                showEndMessage();
            }
            return;
        }

        // Set up scroll listener
        window.addEventListener('scroll', handleScroll, { passive: true });

        // Also listen for resize to handle orientation changes
        window.addEventListener('resize', handleScroll, { passive: true });
    }

    // Handle scroll events with debouncing
    function handleScroll() {
        if (!hasMorePages || isLoading) return;

        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            checkScrollPosition();
        }, DEBOUNCE_DELAY);
    }

    // Check if we should load more content
    function checkScrollPosition() {
        const scrollPosition = window.scrollY + window.innerHeight;
        const documentHeight = document.documentElement.scrollHeight;

        if (documentHeight - scrollPosition < SCROLL_THRESHOLD) {
            loadNextPage();
        }
    }

    // Load the next page of books
    function loadNextPage() {
        if (isLoading || !hasMorePages) return;

        const nextPageUrl = booksContainer.getAttribute('data-next-page');

        if (!nextPageUrl || nextPageUrl === '') {
            hasMorePages = false;
            showEndMessage();
            return;
        }

        isLoading = true;
        hideErrorMessage();

        // Convert relative URL to absolute for OPDS endpoint
        let requestUrl = nextPageUrl;
        if (!requestUrl.startsWith('/opds/')) {
            requestUrl = '/opds' + requestUrl;
        }

        // Create AJAX request
        currentRequest = new XMLHttpRequest();
        currentRequest.open('GET', requestUrl, true);
        currentRequest.setRequestHeader('Accept', 'application/atom+xml');

        currentRequest.onload = function () {
            if (currentRequest.status >= 200 && currentRequest.status < 400) {
                try {
                    processResponse(currentRequest.responseXML || currentRequest.responseText);
                } catch (error) {
                    console.error('Error processing response:', error);
                    showErrorMessage();
                }
            } else {
                console.error('Request failed with status:', currentRequest.status);
                showErrorMessage();
            }
            isLoading = false;
            currentRequest = null;
        };

        currentRequest.onerror = function () {
            console.error('Network error occurred');
            showErrorMessage();
            isLoading = false;
            currentRequest = null;
        };

        currentRequest.send();
    }

    // Process the XML response and add books to the page
    function processResponse(response) {
        let xmlDoc;

        // Parse XML if it's a string
        if (typeof response === 'string') {
            const parser = new DOMParser();
            xmlDoc = parser.parseFromString(response, 'application/xml');
        } else {
            xmlDoc = response;
        }

        // Check for parser errors
        const parserError = xmlDoc.querySelector('parsererror');
        if (parserError) {
            throw new Error('XML parsing failed');
        }

        // Extract next page URL
        const nextLink = xmlDoc.querySelector('link[rel="next"]');
        if (nextLink) {
            const nextHref = nextLink.getAttribute('href');
            booksContainer.setAttribute('data-next-page', nextHref);
        } else {
            booksContainer.setAttribute('data-next-page', '');
            hasMorePages = false;
        }

        // Extract and add book entries
        const entries = xmlDoc.querySelectorAll('entry');
        if (entries.length === 0) {
            hasMorePages = false;
            showEndMessage();
            return;
        }

        entries.forEach(entry => {
            const bookElement = createBookElement(entry);
            if (bookElement) {
                booksContainer.appendChild(bookElement);
            }
        });

        // Check if we need to load more (in case viewport is tall)
        setTimeout(() => {
            checkScrollPosition();
        }, 100);
    }

    // Create a book element from XML entry
    function createBookElement(entry) {
        const li = document.createElement('li');
        li.className = 'book-item';

        // Extract data from XML
        const title = getTextContent(entry, 'title');
        const authorName = getTextContent(entry, 'author > name');
        const authorUri = getTextContent(entry, 'author > uri');
        const content = getTextContent(entry, 'content');
        const format = getTextContent(entry, 'format');
        const size = getTextContent(entry, 'size');
        const lastDownload = getTextContent(entry, 'lastDownload');

        // Extract cover image URL
        const coverLink = entry.querySelector('link[type="image/jpeg"]');
        const coverUrl = coverLink ? coverLink.getAttribute('href') : '/book_cover.jpg';

        // Extract book ID from download links
        const bookId = extractBookId(entry);

        // Build HTML structure
        let html = '';

        // Download date if available
        if (lastDownload) {
            html += `<div class="download-date">${lastDownload}</div>`;
        }

        html += '<div class="book-content">';
        html += '<div class="download-section">';

        // Cover image
        html += `<img class="cover" src="${escapeHtml(coverUrl)}">`;

        // Book info
        html += '<div class="book-info">';
        html += '<div class="info-line">';
        html += '<strong>Format:</strong>';
        html += `<span>${escapeHtml(format || 'Unknown')}</span>`;
        html += '</div>';
        html += '<div class="info-line">';
        html += '<strong>Size:</strong>';
        html += `<span>${escapeHtml(size || 'Unknown')}</span>`;
        html += '</div>';
        html += '</div>';

        // Read button
        if (bookId) {
            html += `<a class="read-button" href="/reader/${bookId}">Read</a>`;
        }

        // Download links
        html += '<div class="download-links">';

        // Check for FB2 format
        const hasFB2 = entry.querySelector('link[type="application/fb2+zip"]') || format === 'fb2';
        if (hasFB2 && bookId) {
            html += `<a class="download-link download-fb2" href="/download/${bookId}/fb2">FB2</a>`;
        }

        // EPUB download (always show if we have book ID)
        if (bookId) {
            html += `<a class="download-link download-epub" href="/download/${bookId}/epub">ePub</a>`;
        }

        html += '</div>'; // download-links
        html += '</div>'; // download-section

        // Book details
        html += '<div class="book-details">';
        html += '<div class="book-header">';
        html += `<div class="book-title">${escapeHtml(title)}</div>`;
        html += '</div>';

        // Author link
        if (authorName) {
            const authorHref = authorUri || `/author/${encodeURIComponent(authorName)}`;
            html += `<a class="book-author" href="${escapeHtml(authorHref)}">${escapeHtml(authorName)}</a>`;
        }

        // Description
        if (content) {
            html += `<div class="book-descr" lang="ru">${escapeHtml(content)}</div>`;
        }

        html += '</div>'; // book-details
        html += '</div>'; // book-content

        li.innerHTML = html;
        return li;
    }

    // Extract book ID from entry's download links
    function extractBookId(entry) {
        // Try FB2 link first
        let link = entry.querySelector('link[type="application/fb2+zip"]');
        if (!link) {
            // Try EPUB link
            link = entry.querySelector('link[type="application/epub+zip"]');
        }

        if (!link) return null;

        const href = link.getAttribute('href');
        if (!href) return null;

        // Handle new format: /download/{guid}/fb2 or /download/{guid}/epub
        if (href.includes('/download/')) {
            const parts = href.split('/');
            const downloadIndex = parts.indexOf('download');
            if (downloadIndex >= 0 && parts.length > downloadIndex + 1) {
                return parts[downloadIndex + 1];
            }
        }

        // Handle old format: /fb2/{guid}/filename or /epub/{guid}/filename
        if (href.includes('/fb2/')) {
            const match = href.match(/\/fb2\/([^\/]+)/);
            if (match) return match[1];
        }
        if (href.includes('/epub/')) {
            const match = href.match(/\/epub\/([^\/]+)/);
            if (match) return match[1];
        }

        return null;
    }

    // Helper function to get text content from XML
    function getTextContent(parent, selector) {
        const element = parent.querySelector(selector);
        return element ? element.textContent : '';
    }

    // Helper function to escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function showEndMessage() {
        if (endMessage) {
            endMessage.classList.add('active');
        }
        hasMorePages = false;
    }

    function showErrorMessage() {
        if (errorMessage) {
            errorMessage.classList.add('active');
        }
    }

    function hideErrorMessage() {
        if (errorMessage) {
            errorMessage.classList.remove('active');
        }
    }

    // Start when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();