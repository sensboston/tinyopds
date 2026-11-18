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
        this.notes = {};
        this.currentNoteId = null;
        this.noteAnchor = null;
        this.noteMode = 'toast';
        this.noteTimer = null;
        this.noteScrollY = 0;
        // Scroll position to return to after "More" of a note took the reader to the notes
        this.noteReturn = null;

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
            noChapters: 'No chapters available',
            notes: 'Notes',
            more: 'More',
            backToText: 'Back to text'
        };

        this.initializeElements();
        this.loadLocalization();
        this.bindEvents();
        this.loadPreferences();
        this.checkMobile();
        this.applyLocalization();
        this.watchTranslation();

        // Auto-load book if data is injected
        this.checkForInjectedBook();
    }

    // The browser hyphenates by the lang attribute: when the page is translated (Google Translate marks
    // <html> with the translated-ltr/rtl class and sets its lang to the target language), the book text
    // gets the target language, otherwise the translated text would be hyphenated as the original one
    watchTranslation() {
        const html = document.documentElement;
        const update = () => {
            if (!this.bookContent) return;
            const translated = /\btranslated-(ltr|rtl)\b/.test(html.className);
            let lang = this.detectedLanguage;
            if (translated) {
                lang = html.lang && html.lang !== this.detectedLanguage
                    ? html.lang
                    : (navigator.language || 'en').split('-')[0];
            }
            if (this.bookContent.getAttribute('lang') !== lang) {
                this.bookContent.setAttribute('lang', lang);
            }
        };
        new MutationObserver(update).observe(html, { attributes: true, attributeFilter: ['class', 'lang'] });
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
        this.noteOverlay = document.getElementById('noteOverlay');
        this.noteSheet = document.getElementById('noteSheet');
        this.noteText = document.getElementById('noteText');
        this.noteMore = document.getElementById('noteMore');
        this.noteBack = document.getElementById('noteBack');
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

        this.noteMore.textContent = this.strings.more;
        this.noteBack.textContent = '\u2190 ' + this.strings.backToText;
    }

    bindEvents() {
        this.menuToggle.onclick = () => this.toggleMenu();

        // The click areas cover the text: a tap on a note mark under them opens the note instead of turning the page
        this.clickTop.onclick = (e) => { if (!this.openNoteAt(e.clientX, e.clientY)) this.scrollPage(-1); };
        this.clickBottom.onclick = (e) => { if (!this.openNoteAt(e.clientX, e.clientY)) this.scrollPage(1); };

        this.bookContent.addEventListener('click', (e) => {
            const ref = e.target.closest && e.target.closest('[data-note]');
            if (ref) {
                e.preventDefault();
                this.showNote(ref.getAttribute('data-note'), ref);
            }
        });
        // While a note is open, a click elsewhere closes it and does nothing else (doesn't turn the page);
        // a click on another note mark opens that note
        window.addEventListener('click', (e) => {
            if (this.currentNoteId === null || this.noteSheet.contains(e.target)) return;
            e.stopPropagation();
            e.preventDefault();
            this.hideNote();
            this.openNoteAt(e.clientX, e.clientY);
        }, true);
        this.noteSheet.onclick = (e) => {
            e.stopPropagation();
            const ref = e.target.closest && e.target.closest('[data-note]');
            if (ref) {
                // A note linking to another note
                e.preventDefault();
                this.showNote(ref.getAttribute('data-note'), this.noteAnchor);
            } else if (this.noteMode === 'toast') {
                // A tap on the note of a phone: the whole note if it's cut, closed otherwise
                if (this.noteText.classList.contains('clamped')) this.goToNote();
                else this.hideNote();
            }
        };
        // Scrolling (or a page turned by keys) closes the note
        window.addEventListener('scroll', () => {
            if (this.currentNoteId !== null && Math.abs(window.scrollY - this.noteScrollY) > 40) this.hideNote();
        }, { passive: true });
        this.noteMore.onclick = (e) => {
            e.stopPropagation();
            this.goToNote();
        };
        this.noteBack.onclick = (e) => {
            e.stopPropagation();
            if (history.state && history.state.noteReturn) history.back();
            else this.returnToText();
        };

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
            if (this.noteReturn !== null) {
                this.returnToText();
            } else if (this.tocVisible) {
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
                         data-chapter-id="${this.formatConverter.escapeXml(item.id)}"
                         data-level="${level}">
                        <span class="toc-item-text">${this.formatConverter.escapeXml(item.title)}</span>
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

            // Remove history manipulation - just scroll
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
            // Both values come from the book and end up in src="..." attribute
            let contentType = node.getAttribute('content-type') || 'image/jpeg';
            if (!/^image\/[a-z0-9.+-]+$/i.test(contentType)) contentType = 'image/jpeg';
            const base64Data = node.textContent.replace(/[^A-Za-z0-9+/=]/g, '');
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

        // Notes before the text: the links to them are converted to note marks
        this.formatConverter.strings = this.strings;
        this.notes = this.formatConverter.extractFB2Notes(xmlDoc);

        // THEN Convert to HTML using corrected body selection logic
        const bodyNodes = xmlDoc.querySelectorAll('body');
        const escape = (text) => this.formatConverter.escapeXml(text);
        let htmlContent = `<h1>${escape(title)}</h1><p class="author">${escape(author)}</p>${coverHtml}`;

        // FIXED: Use first body element as main body per FB2 standard
        // In FB2 format, the first body is always the main text content
        if (bodyNodes.length > 0) {
            htmlContent += this.formatConverter.convertFB2ToHTML(bodyNodes[0]);
            // The notes after the text, as in a printed book: "More" of a long note leads there
            htmlContent += this.formatConverter.notesHtml;
        } else {
            console.warn('No body elements found in FB2 document');
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
        this.hideNote();
        this.noteReturn = null;
        this.noteBack.classList.remove('visible');

        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    // The note mark nearest to the point, if the point is within a finger's reach of it: a raised "1" is
    // a few pixels wide
    openNoteAt(x, y) {
        const reachX = 14, reachY = 12;
        let best = null, bestDistance = Infinity;
        for (const ref of this.bookContent.querySelectorAll('[data-note]')) {
            for (const r of ref.getClientRects()) {
                if (r.bottom < -reachY || r.top > window.innerHeight + reachY) continue;
                if (x < r.left - reachX || x > r.right + reachX || y < r.top - reachY || y > r.bottom + reachY) continue;
                const dx = Math.max(r.left - x, 0, x - r.right), dy = Math.max(r.top - y, 0, y - r.bottom);
                const distance = dx * dx + dy * dy;
                if (distance < bestDistance) {
                    best = ref;
                    bestDistance = distance;
                }
            }
        }
        if (!best) return false;
        this.showNote(best.getAttribute('data-note'), best);
        return true;
    }

    // A phone shows the note at the bottom for a few seconds (as FBReader does), a computer with a mouse in
    // a popup at the note mark until a click elsewhere
    showNote(id, anchor) {
        const note = this.notes[id];
        if (!note) return;
        clearTimeout(this.noteTimer);
        this.currentNoteId = id;
        this.noteAnchor = anchor || null;
        this.noteScrollY = window.scrollY;
        this.noteMode = window.matchMedia('(hover: none)').matches || !anchor ? 'toast' : 'popup';

        this.noteText.innerHTML = note.html || (note.label ? `<p>${this.formatConverter.escapeXml(note.label)}</p>` : '');
        // The font of the book, the size a bit smaller
        this.noteText.style.fontFamily = getComputedStyle(this.bookContent).fontFamily;
        this.noteText.style.fontSize = Math.max(13, this.fontSize - 1) + 'px';
        this.noteText.scrollTop = 0;
        this.noteText.classList.remove('clamped');
        this.noteText.style.maxHeight = '';
        this.noteSheet.removeAttribute('style');
        this.noteOverlay.className = 'note-overlay visible ' + this.noteMode;

        if (this.noteMode === 'popup') {
            this.placePopup(anchor);
            this.noteMore.style.display = 'none';
        } else {
            const long = this.noteText.scrollHeight > this.noteText.clientHeight + 4;
            this.noteText.classList.toggle('clamped', long);
            this.noteMore.style.display = long ? 'block' : 'none';
            // Time to read it: about 1 s for 15 words, 4 to 12 s
            const words = this.noteText.textContent.split(/\s+/).length;
            this.noteTimer = setTimeout(() => this.hideNote(), Math.min(12000, Math.max(4000, 2000 + words * 250)));
        }
        if (this.menuVisible) this.toggleMenu();
    }

    // The popup below the mark, or above it if there is more room there; a long note scrolls inside it
    placePopup(anchor) {
        const margin = 12, gap = 8;
        const r = anchor.getBoundingClientRect();
        const width = Math.min(460, window.innerWidth - 2 * margin);
        const left = Math.min(Math.max(r.left + r.width / 2 - width / 2, margin), window.innerWidth - width - margin);
        const below = window.innerHeight - r.bottom - gap - margin;
        const above = r.top - gap - margin;
        const sheet = this.noteSheet.style;
        sheet.width = width + 'px';
        sheet.left = left + 'px';
        if (below >= 160 || below >= above) {
            sheet.top = (r.bottom + gap) + 'px';
            this.noteText.style.maxHeight = Math.max(80, Math.min(below, window.innerHeight * 0.5) - 28) + 'px';
        } else {
            sheet.bottom = (window.innerHeight - r.top + gap) + 'px';
            this.noteText.style.maxHeight = Math.max(80, Math.min(above, window.innerHeight * 0.5) - 28) + 'px';
        }
    }

    hideNote() {
        clearTimeout(this.noteTimer);
        this.currentNoteId = null;
        this.noteOverlay.classList.remove('visible');
    }

    goToNote() {
        const id = this.currentNoteId;
        this.hideNote();
        const element = id !== null && document.getElementById(this.formatConverter.noteElementId(id));
        if (!element) return;

        if (this.noteReturn === null) {
            this.noteReturn = window.scrollY;
            // The back button of a phone returns to the text too
            history.pushState({ noteReturn: true }, '');
        }
        window.scrollTo({ top: element.getBoundingClientRect().top + window.scrollY - 20 });
        element.classList.add('highlight');
        setTimeout(() => element.classList.remove('highlight'), 1500);
        this.noteBack.classList.add('visible');
    }

    returnToText() {
        if (this.noteReturn === null) return;
        window.scrollTo({ top: this.noteReturn });
        this.noteReturn = null;
        this.noteBack.classList.remove('visible');
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
        if (e.key === 'Escape' && this.currentNoteId !== null) {
            e.preventDefault();
            this.hideNote();
            return;
        }

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