// Main reader application logic - frequently modified for features

class UniversalReader {
    constructor() {
        // Initialize properties
        this.themes = ['light', 'dark', 'sepia'];
        this.currentThemeIndex = 0;
        this.fonts = ['font-serif', 'font-sans', 'font-mono'];
        this.fontClasses = ['serif', 'sans', 'mono'];
        this.currentFontIndex = 0;
        this.fontSize = 16;
        this.margins = 40;
        this.widthMode = 'standard';
        this.bookTitle = '';
        this.bookAuthor = '';
        this.menuVisible = false;
        this.tocVisible = false;
        this.images = {};
        this.chapters = [];
        this.currentChapterIndex = -1;
        this.isScrolling = false;
        this.detectedLanguage = 'en';

        // Create format converter instance
        this.formatConverter = new FormatConverter();

        // Localization strings
        this.strings = {
            tableOfContents: 'Table of Contents',
            openBook: 'Open Book',
            decreaseFont: 'Decrease Font',
            increaseFont: 'Increase Font',
            changeFont: 'Change Font',
            changeTheme: 'Change Theme',
            decreaseMargins: 'Decrease Margins',
            increaseMargins: 'Increase Margins',
            standardWidth: 'Standard Width',
            fullWidth: 'Full Width',
            fullscreen: 'Fullscreen',
            loading: 'Loading...',
            errorLoading: 'Error loading file',
            noTitle: 'Untitled',
            unknownAuthor: 'Unknown Author',
            noChapters: 'No chapters available'
        };

        this.initializeElements();
        this.loadLocalization();
        this.bindEvents();
        this.loadPreferences();
        this.checkMobile();
        this.applyLocalization();

        // Auto-load book if data is injected
        this.checkForInjectedBook();
    }

    initializeElements() {
        this.fileInput = document.getElementById('fileInput');
        this.bookContent = document.getElementById('bookContent');
        this.toolbar = document.getElementById('toolbar');
        this.menuToggle = document.getElementById('menuToggle');
        this.progressBar = document.getElementById('progressBar');
        this.readerContainer = document.getElementById('readerContainer');
        this.bookTitleEl = document.getElementById('bookTitle');
        this.bookAuthorEl = document.getElementById('bookAuthor');
        this.clickTop = document.getElementById('clickTop');
        this.clickBottom = document.getElementById('clickBottom');
        this.fontIndicator = document.getElementById('fontIndicator');
        this.tocOverlay = document.getElementById('tocOverlay');
        this.tocContent = document.getElementById('tocContent');
        this.tocClose = document.getElementById('tocClose');
    }

    loadLocalization() {
        try {
            const savedStrings = localStorage.getItem('tinyopds-localization');
            if (savedStrings) {
                const parsed = JSON.parse(savedStrings);
                this.strings = { ...this.strings, ...parsed };
            }
        } catch (e) {
            console.warn('Could not load localization:', e);
        }
    }

    applyLocalization() {
        const tooltips = {
            'tocButton': this.strings.tableOfContents,
            'openFile': this.strings.openBook,
            'decreaseFont': this.strings.decreaseFont,
            'increaseFont': this.strings.increaseFont,
            'fontToggle': this.strings.changeFont,
            'themeToggle': this.strings.changeTheme,
            'decreaseMargins': this.strings.decreaseMargins,
            'increaseMargins': this.strings.increaseMargins,
            'standardWidth': this.strings.standardWidth,
            'fullWidth': this.strings.fullWidth,
            'fullscreen': this.strings.fullscreen
        };

        for (let [id, text] of Object.entries(tooltips)) {
            const btn = document.getElementById(id);
            if (btn) {
                const tooltip = btn.querySelector('.tooltip');
                if (tooltip) {
                    tooltip.textContent = text;
                }
            }
        }

        const tocTitle = document.querySelector('.toc-title');
        if (tocTitle) {
            tocTitle.textContent = this.strings.tableOfContents;
        }
    }

    bindEvents() {
        this.menuToggle.onclick = () => this.toggleMenu();

        this.clickTop.onclick = () => this.scrollPage(-1);
        this.clickBottom.onclick = () => this.scrollPage(1);

        const tocBtn = document.getElementById('tocButton');
        if (tocBtn) {
            tocBtn.onclick = () => this.showTOC();
        }

        this.tocClose.onclick = () => this.hideTOC();

        this.tocOverlay.onclick = (e) => {
            if (e.target === this.tocOverlay) {
                this.hideTOC();
            }
        };

        // Handle browser back button for TOC and menu
        window.addEventListener('popstate', (e) => {
            if (this.tocVisible) {
                this.hideTOC();
            } else if (this.menuVisible) {
                this.toggleMenu();
            }
        });

        this.fileInput.onchange = (e) => this.handleFileSelect(e.target.files[0]);

        document.getElementById('decreaseFont').onclick = () => this.changeFontSize(-2);
        document.getElementById('increaseFont').onclick = () => this.changeFontSize(2);
        document.getElementById('fontToggle').onclick = () => this.toggleFont();

        document.getElementById('themeToggle').onclick = () => this.toggleTheme();

        document.getElementById('decreaseMargins').onclick = () => this.changeMargins(-10);
        document.getElementById('increaseMargins').onclick = () => this.changeMargins(10);

        document.getElementById('standardWidth').onclick = () => this.setWidthMode('standard');
        document.getElementById('fullWidth').onclick = () => this.setWidthMode('full-width');

        document.getElementById('fullscreen').onclick = () => this.toggleFullscreen();

        window.onscroll = () => {
            this.updateProgress();
            if (!this.isScrolling) {
                this.updateCurrentChapter();
            }
        };

        document.onkeydown = (e) => this.handleKeyboard(e);

        document.onclick = (e) => {
            if (!this.toolbar.contains(e.target) &&
                !this.menuToggle.contains(e.target) &&
                this.menuVisible) {
                this.toggleMenu();
            }
        };
    }

    checkForInjectedBook() {
        // Check if book data was injected by TinyOPDS
        if (window.tinyOPDSBook) {
            setTimeout(() => {
                const dataUrl = window.tinyOPDSBook.data;
                fetch(dataUrl)
                    .then(res => res.blob())
                    .then(blob => {
                        const file = new File([blob], window.tinyOPDSBook.fileName, {
                            type: blob.type
                        });
                        this.handleFileSelect(file);
                    })
                    .catch(err => console.error('Error loading injected book:', err));
            }, 500);
        }
    }

    toggleMenu() {
        this.menuVisible = !this.menuVisible;
        this.toolbar.classList.toggle('visible', this.menuVisible);
        this.menuToggle.classList.toggle('active', this.menuVisible);
        this.menuToggle.textContent = this.menuVisible ? '✕' : '☰';
    }

    showTOC() {
        if (this.chapters.length === 0) {
            return;
        }

        this.tocVisible = true;
        this.tocOverlay.classList.add('visible');
        this.renderTOC();

        // Add history state for TOC
        history.pushState({ tocOpen: true }, '');

        if (this.menuVisible) {
            this.toggleMenu();
        }
    }

    hideTOC() {
        if (!this.tocVisible) return;  // Prevent double closing

        this.tocVisible = false;
        this.tocOverlay.classList.remove('visible');
    }

    // New method to render tree-structured TOC
    renderTOC() {
        if (this.chapters.length === 0) {
            this.tocContent.innerHTML = `<div class="toc-empty">${this.strings.noChapters}</div>`;
            return;
        }

        // Helper function to render tree structure
        const renderTOCItems = (items, level = 0) => {
            let html = '';

            for (const item of items) {
                const isCurrent = this.isCurrentChapter(item.id);
                const indent = level * 20; // Indentation for nested items

                html += `
                    <div class="toc-item ${isCurrent ? 'current' : ''}"
                         style="padding-left: ${indent}px;"
                         data-chapter-id="${item.id}"
                         data-level="${level}">
                        <span class="toc-item-text">${item.title}</span>
                    </div>
                `;

                // Render children if they exist
                if (item.children && item.children.length > 0) {
                    html += renderTOCItems(item.children, level + 1);
                }
            }

            return html;
        };

        // Render the TOC tree
        this.tocContent.innerHTML = renderTOCItems(this.chapters);

        // Add click handlers
        this.tocContent.querySelectorAll('.toc-item').forEach(item => {
            item.onclick = () => {
                const chapterId = item.getAttribute('data-chapter-id');
                this.navigateToChapter(chapterId);
            };
        });
    }

    // Helper method to check if a chapter is current
    isCurrentChapter(chapterId) {
        // This is a simplified check - you might want to enhance this
        const element = document.getElementById(chapterId);
        if (element) {
            const scrollTop = window.scrollY;
            const elementTop = element.offsetTop;
            const elementBottom = elementTop + element.offsetHeight;
            return scrollTop >= elementTop - 150 && scrollTop < elementBottom;
        }
        return false;
    }

    navigateToChapter(chapterId) {
        const element = document.getElementById(chapterId);

        if (element) {
            this.isScrolling = true;

            const elementTop = element.offsetTop;
            const offset = 0;

            window.scrollTo({
                top: elementTop - offset,
                behavior: 'smooth'
            });

            setTimeout(() => {
                this.isScrolling = false;
                this.updateCurrentChapter();
            }, 500);

            this.hideTOC();
        } else {
            console.error('Chapter element not found:', chapterId);
        }
    }

    updateCurrentChapter() {
        if (this.chapters.length === 0) return;

        // Update current chapter highlighting in TOC if it's visible
        if (this.tocVisible) {
            this.renderTOC();
        }
    }

    scrollPage(direction) {
        const viewportHeight = window.innerHeight;
        const lineHeight = this.fontSize * 1.8;
        const scrollAmount = (viewportHeight * 0.9) + (lineHeight * 1.5);

        const oldScrollBehavior = document.body.style.scrollBehavior;
        document.body.style.scrollBehavior = 'smooth';

        window.scrollBy({
            top: scrollAmount * direction,
            behavior: 'smooth'
        });

        setTimeout(() => {
            document.body.style.scrollBehavior = oldScrollBehavior;
        }, 500);
    }

    checkMobile() {
        const isMobile = window.innerWidth <= 768;
        if (isMobile) {
            this.setWidthMode('full-width');
        }
    }

    async handleFileSelect(file) {
        if (!file) return;

        const fileName = file.name.toLowerCase();
        this.showLoading(true);

        try {
            let fb2Content;

            if (fileName.endsWith('.epub')) {
                fb2Content = await this.formatConverter.convertEpubToFb2(file);
                this.images = this.formatConverter.images;
                this.chapters = this.formatConverter.chapters;
            } else if (fileName.endsWith('.fb2.zip') || fileName.endsWith('.zip')) {
                fb2Content = await this.extractFB2FromZip(file);
            } else if (fileName.endsWith('.fb2')) {
                fb2Content = await this.readFile(file);
            } else {
                throw new Error('Unsupported file format');
            }

            const bookData = await this.parseFB2(fb2Content);
            this.displayBook(bookData);

            this.clickTop.style.display = 'block';
            this.clickBottom.style.display = 'block';

        } catch (error) {
            console.error('Error processing file:', error);
            this.showError(this.strings.errorLoading + ': ' + error.message);
        } finally {
            this.showLoading(false);
        }
    }

    async extractFB2FromZip(file) {
        try {
            const zip = await JSZip.loadAsync(file);

            let fb2File = null;
            for (let fileName in zip.files) {
                if (fileName.toLowerCase().endsWith('.fb2')) {
                    fb2File = zip.files[fileName];
                    break;
                }
            }

            if (!fb2File) {
                throw new Error('FB2 file not found in archive');
            }

            return await fb2File.async('string');
        } catch (error) {
            throw new Error('Error extracting archive: ' + error.message);
        }
    }

    readFile(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                const arrayBuffer = e.target.result;
                const uint8Array = new Uint8Array(arrayBuffer);
                const encoding = this.formatConverter.detectEncoding(uint8Array);
                const text = this.formatConverter.decodeText(uint8Array, encoding);
                resolve(text);
            };
            reader.onerror = () => reject(new Error('Error reading file'));
            reader.readAsArrayBuffer(file);
        });
    }

    async parseFB2(content) {
        const parser = new DOMParser();
        content = content.replace(/<h[1-6]\s+xmlns=""[^>]*>.*?<\/h[1-6]>/gi, '');
        content = content.replace(/<(div|span|p)\s+xmlns=""[^>]*>.*?<\/\1>/gi, '');
        const xmlDoc = parser.parseFromString(content, 'text/xml');

        if (xmlDoc.getElementsByTagName('parsererror').length > 0) {
            throw new Error('Invalid FB2 file');
        }

        // Extract images
        const binaryNodes = xmlDoc.querySelectorAll('binary');
        binaryNodes.forEach(node => {
            const id = node.getAttribute('id');
            const contentType = node.getAttribute('content-type') || 'image/jpeg';
            const base64Data = node.textContent.replace(/\s/g, '');
            this.images[id] = `data:${contentType};base64,${base64Data}`;
        });

        // Store images in converter for HTML generation
        this.formatConverter.images = this.images;

        // Extract metadata
        const titleNode = xmlDoc.querySelector('title-info book-title');
        const title = titleNode ? titleNode.textContent : this.strings.noTitle;

        const authorNode = xmlDoc.querySelector('title-info author');
        let author = this.strings.unknownAuthor;
        if (authorNode) {
            const firstName = authorNode.querySelector('first-name')?.textContent || '';
            const lastName = authorNode.querySelector('last-name')?.textContent || '';
            const middleName = authorNode.querySelector('middle-name')?.textContent || '';
            author = `${firstName} ${middleName} ${lastName}`.replace(/\s+/g, ' ').trim();
        }

        // Extract cover
        let coverHtml = '';
        const coverpage = xmlDoc.querySelector('coverpage image');
        if (coverpage) {
            const href = coverpage.getAttribute('l:href') || coverpage.getAttribute('xlink:href');
            const imageId = href?.replace('#', '');
            if (imageId && this.images[imageId]) {
                coverHtml = `<img src="${this.images[imageId]}" alt="Cover" style="max-height: 400px;">`;
            }
        }

        // IMPORTANT: Extract TOC FIRST - this sets up the ID map
        this.chapters = this.formatConverter.extractFB2TOC(xmlDoc);

        // THEN Convert to HTML - using the same body element
        // Find the main body (without name attribute) - same logic as in extractFB2TOC
        const bodyNodes = xmlDoc.querySelectorAll('body');
        let htmlContent = `<h1>${title}</h1><p class="author">${author}</p>${coverHtml}`;

        for (let body of bodyNodes) {
            if (!body.hasAttribute('name')) {
                // This is the main body - same one processed by extractFB2TOC
                htmlContent += this.formatConverter.convertFB2ToHTML(body);
                break; // Only process the first body without name attribute
            }
        }

        return {
            title,
            author,
            content: htmlContent
        };
    }

    displayBook(bookData) {
        this.bookTitle = bookData.title;
        this.bookAuthor = bookData.author;
        this.bookTitleEl.textContent = this.bookTitle;
        this.bookAuthorEl.textContent = this.bookAuthor;
        this.bookContent.innerHTML = bookData.content;
        this.bookContent.style.display = 'block';

        this.detectedLanguage = this.formatConverter.detectLanguage(bookData.content);

        this.applyFontSize();
        this.applyTheme();
        this.applyFont();
        this.applyMargins();
        this.applyWidthMode();
        this.applyHyphenation();

        if (this.menuVisible) {
            this.toggleMenu();
        }

        this.currentChapterIndex = -1;
        this.isScrolling = false;

        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    applyHyphenation() {
        this.bookContent.classList.add('hyphens-enabled');
        this.bookContent.classList.remove('hyphens-disabled');
        this.bookContent.setAttribute('lang', this.detectedLanguage);
        document.documentElement.setAttribute('lang', this.detectedLanguage);

        // Force reflow
        this.bookContent.style.display = 'none';
        this.bookContent.offsetHeight;
        this.bookContent.style.display = 'block';
    }

    changeFontSize(delta) {
        this.fontSize = Math.max(12, Math.min(32, this.fontSize + delta));
        this.applyFontSize();
        this.savePreferences();
    }

    applyFontSize() {
        this.bookContent.style.fontSize = this.fontSize + 'px';
    }

    toggleFont() {
        this.currentFontIndex = (this.currentFontIndex + 1) % this.fonts.length;
        this.applyFont();
        this.savePreferences();
    }

    applyFont() {
        this.fonts.forEach(font => this.bookContent.classList.remove(font));
        this.bookContent.classList.add(this.fonts[this.currentFontIndex]);
        this.fontIndicator.className = `font-indicator ${this.fontClasses[this.currentFontIndex]}`;
    }

    toggleTheme() {
        this.currentThemeIndex = (this.currentThemeIndex + 1) % this.themes.length;
        this.applyTheme();
        this.savePreferences();
    }

    applyTheme() {
        const theme = this.themes[this.currentThemeIndex];

        this.bookContent.className = this.bookContent.className
            .replace(/\b(light|dark|sepia)\b/g, '')
            .trim() + ' ' + theme;

        document.body.className = 'theme-' + theme;
    }

    changeMargins(delta) {
        this.margins = Math.max(10, Math.min(80, this.margins + delta));
        this.applyMargins();
        this.savePreferences();
    }

    applyMargins() {
        this.bookContent.style.paddingLeft = this.margins + 'px';
        this.bookContent.style.paddingRight = this.margins + 'px';
    }

    setWidthMode(mode) {
        if (window.innerWidth <= 768) return;

        this.widthMode = mode;
        this.applyWidthMode();
        this.savePreferences();
    }

    applyWidthMode() {
        document.getElementById('standardWidth').classList.toggle('active', this.widthMode === 'standard');
        document.getElementById('fullWidth').classList.toggle('active', this.widthMode === 'full-width');

        this.readerContainer.className = this.readerContainer.className
            .replace(/\b(standard|full-width)\b/g, '')
            .trim() + ' ' + this.widthMode;
    }

    toggleFullscreen() {
        if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen();
        } else {
            document.exitFullscreen();
        }
    }

    updateProgress() {
        const scrollTop = window.scrollY;
        const scrollHeight = document.body.scrollHeight - window.innerHeight;
        const progress = (scrollTop / scrollHeight) * 100;
        this.progressBar.style.width = Math.min(100, Math.max(0, progress)) + '%';
    }

    handleKeyboard(e) {
        if (e.key === 'Escape' && this.tocVisible) {
            e.preventDefault();
            this.hideTOC();
            return;
        }

        switch (e.key) {
            case ' ':
                e.preventDefault();
                this.scrollPage(e.shiftKey ? -1 : 1);
                break;
            case 'ArrowUp':
                if (e.ctrlKey) {
                    e.preventDefault();
                    this.scrollPage(-1);
                }
                break;
            case 'ArrowDown':
                if (e.ctrlKey) {
                    e.preventDefault();
                    this.scrollPage(1);
                }
                break;
            case 'm':
            case 'M':
                e.preventDefault();
                this.toggleMenu();
                break;
            case 't':
            case 'T':
                e.preventDefault();
                if (this.chapters.length > 0) {
                    if (this.tocVisible) {
                        this.hideTOC();
                    } else {
                        this.showTOC();
                    }
                }
                break;
            case 'f':
            case 'F':
                if (e.ctrlKey) {
                    e.preventDefault();
                    this.toggleFullscreen();
                }
                break;
            case '+':
            case '=':
                if (e.ctrlKey) {
                    e.preventDefault();
                    this.changeFontSize(2);
                }
                break;
            case '-':
                if (e.ctrlKey) {
                    e.preventDefault();
                    this.changeFontSize(-2);
                }
                break;
        }
    }

    showLoading(show) {
        if (show) {
            this.bookContent.innerHTML = `<div class="loading">${this.strings.loading}</div>`;
            this.bookContent.style.display = 'block';
        }
    }

    showError(message) {
        this.bookContent.innerHTML = `<div class="error">${message}</div>`;
        this.bookContent.style.display = 'block';
    }

    savePreferences() {
        localStorage.setItem('reader-prefs', JSON.stringify({
            themeIndex: this.currentThemeIndex,
            fontIndex: this.currentFontIndex,
            fontSize: this.fontSize,
            margins: this.margins,
            widthMode: this.widthMode
        }));
    }

    loadPreferences() {
        try {
            const prefs = JSON.parse(localStorage.getItem('reader-prefs') || '{}');
            this.currentThemeIndex = prefs.themeIndex || 0;
            this.currentFontIndex = prefs.fontIndex || 0;
            this.fontSize = prefs.fontSize || 16;
            this.margins = prefs.margins || 40;
            this.widthMode = prefs.widthMode || 'standard';

            document.body.className = 'theme-' + this.themes[this.currentThemeIndex];
            this.bookContent.className = `book-content ${this.themes[this.currentThemeIndex]} ${this.fonts[this.currentFontIndex]} hyphens-enabled`;
            this.fontIndicator.className = `font-indicator ${this.fontClasses[this.currentFontIndex]}`;

            this.bookContent.style.paddingLeft = this.margins + 'px';
            this.bookContent.style.paddingRight = this.margins + 'px';

            const standardBtn = document.getElementById('standardWidth');
            const fullBtn = document.getElementById('fullWidth');
            if (standardBtn && fullBtn) {
                standardBtn.classList.toggle('active', this.widthMode === 'standard');
                fullBtn.classList.toggle('active', this.widthMode === 'full-width');
            }

            this.readerContainer.className = `reader-container ${this.widthMode}`;
        } catch (e) {
            console.warn('Could not load preferences:', e);
        }
    }
}

// Initialize reader when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.universalReader = new UniversalReader();
});