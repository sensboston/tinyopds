// Format conversion utilities - FB2 and EPUB handling
// Separated as these might need tweaking for format compatibility

class FormatConverter {
    constructor() {
        this.images = {};
        this.chapters = [];
        this.detectedLanguage = 'en';
    }

    // Encoding detection methods
    detectEncoding(bytes) {
        // UTF-8 BOM
        if (bytes.length >= 3 && bytes[0] === 0xEF && bytes[1] === 0xBB && bytes[2] === 0xBF) {
            return 'utf-8';
        }
        // UTF-16 LE BOM
        if (bytes.length >= 2 && bytes[0] === 0xFF && bytes[1] === 0xFE) {
            return 'utf-16le';
        }
        // UTF-16 BE BOM
        if (bytes.length >= 2 && bytes[0] === 0xFE && bytes[1] === 0xFF) {
            return 'utf-16be';
        }

        const sampleSize = Math.min(bytes.length, 8192);
        const sample = bytes.slice(0, sampleSize);

        if (this.isValidUTF8(sample)) {
            const decoder = new TextDecoder('utf-8');
            const text = decoder.decode(sample);
            if (text.includes('<?xml') || text.includes('<FictionBook') || text.includes('encoding=')) {
                return 'utf-8';
            }

            let nonAsciiCount = 0;
            for (let i = 0; i < sample.length; i++) {
                if (sample[i] >= 128) nonAsciiCount++;
            }

            if (nonAsciiCount / sample.length < 0.05) {
                return 'utf-8';
            }
        }

        const highBytes = [];
        let totalHighBytes = 0;
        for (let i = 0; i < sample.length; i++) {
            if (sample[i] >= 128) {
                highBytes.push(sample[i]);
                totalHighBytes++;
            }
        }

        if (totalHighBytes === 0) {
            return 'utf-8';
        }

        let shouldCheckLegacy = !this.isValidUTF8(sample);
        if (!shouldCheckLegacy) {
            const suspiciousPatterns = totalHighBytes > sample.length * 0.3;
            shouldCheckLegacy = suspiciousPatterns;
        }

        if (!shouldCheckLegacy) {
            return 'utf-8';
        }

        const cp866Score = this.scoreCyrillicCP866(highBytes);
        const win1251Score = this.scoreCyrillicWin1251(highBytes);
        const koi8rScore = this.scoreCyrillicKOI8R(highBytes);
        const win1252Score = this.scoreWesternWin1252(highBytes);
        const iso88591Score = this.scoreWesternISO88591(highBytes);

        const scores = [
            { encoding: 'cp866', score: cp866Score },
            { encoding: 'windows-1251', score: win1251Score },
            { encoding: 'koi8-r', score: koi8rScore },
            { encoding: 'windows-1252', score: win1252Score },
            { encoding: 'iso-8859-1', score: iso88591Score }
        ];

        scores.sort((a, b) => b.score - a.score);

        if (scores[0].score > 0.6) {
            return scores[0].encoding;
        }

        return 'utf-8';
    }

    isValidUTF8(bytes) {
        let i = 0;
        while (i < bytes.length) {
            const byte = bytes[i];

            if (byte < 0x80) {
                i++;
            } else if ((byte >> 5) === 0x06) {
                if (i + 1 >= bytes.length || (bytes[i + 1] >> 6) !== 0x02) {
                    return false;
                }
                i += 2;
            } else if ((byte >> 4) === 0x0E) {
                if (i + 2 >= bytes.length ||
                    (bytes[i + 1] >> 6) !== 0x02 ||
                    (bytes[i + 2] >> 6) !== 0x02) {
                    return false;
                }
                i += 3;
            } else if ((byte >> 3) === 0x1E) {
                if (i + 3 >= bytes.length ||
                    (bytes[i + 1] >> 6) !== 0x02 ||
                    (bytes[i + 2] >> 6) !== 0x02 ||
                    (bytes[i + 3] >> 6) !== 0x02) {
                    return false;
                }
                i += 4;
            } else {
                return false;
            }
        }
        return true;
    }

    // Encoding scoring methods
    scoreCyrillicCP866(highBytes) {
        let score = 0;
        for (let byte of highBytes) {
            if ((byte >= 0x80 && byte <= 0xAF) || (byte >= 0xE0 && byte <= 0xEF)) {
                score++;
            } else if ((byte >= 0xB0 && byte <= 0xDF) || (byte >= 0xF0 && byte <= 0xFF)) {
                score -= 0.5;
            }
        }
        return score / Math.max(highBytes.length, 1);
    }

    scoreCyrillicWin1251(highBytes) {
        let score = 0;
        for (let byte of highBytes) {
            if ((byte >= 0xC0 && byte <= 0xFF) || byte === 0xB8 || byte === 0xA8) {
                score++;
            } else if (byte >= 0x80 && byte <= 0xBF) {
                if (byte === 0xA0 || byte === 0xA9 || byte === 0xAE) {
                    score += 0.1;
                } else {
                    score -= 0.3;
                }
            }
        }
        return score / Math.max(highBytes.length, 1);
    }

    scoreCyrillicKOI8R(highBytes) {
        let score = 0;
        for (let byte of highBytes) {
            if ((byte >= 0xC1 && byte <= 0xDF) || (byte >= 0xE0 && byte <= 0xFF)) {
                score++;
            } else if (byte >= 0x80 && byte <= 0xC0) {
                score -= 0.5;
            }
        }
        return score / Math.max(highBytes.length, 1);
    }

    scoreWesternWin1252(highBytes) {
        let score = 0;
        const commonWin1252 = [0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8E,
            0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9E, 0x9F,
            0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF,
            0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF,
            0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF,
            0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF,
            0xE0, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF,
            0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF];

        for (let byte of highBytes) {
            if (commonWin1252.includes(byte)) {
                score += 0.5;
            }
        }
        return score / Math.max(highBytes.length, 1);
    }

    scoreWesternISO88591(highBytes) {
        let score = 0;
        for (let byte of highBytes) {
            if (byte >= 0xA0) {
                score += 0.3;
            }
        }
        return score / Math.max(highBytes.length, 1);
    }

    // Decoding methods
    decodeText(bytes, encoding) {
        try {
            if (['utf-8', 'utf-16le', 'utf-16be', 'iso-8859-1', 'windows-1252'].includes(encoding)) {
                const decoder = new TextDecoder(encoding);
                return decoder.decode(bytes);
            }

            if (encoding === 'windows-1251') {
                return this.decodeWindows1251(bytes);
            } else if (encoding === 'cp866') {
                return this.decodeCP866(bytes);
            } else if (encoding === 'koi8-r') {
                return this.decodeKOI8R(bytes);
            }

            const decoder = new TextDecoder('utf-8');
            return decoder.decode(bytes);
        } catch (e) {
            console.warn('Decoding failed, using UTF-8 fallback:', e);
            const decoder = new TextDecoder('utf-8');
            return decoder.decode(bytes);
        }
    }

    decodeWindows1251(bytes) {
        const win1251Map = {
            0x80: 0x0402, 0x81: 0x0403, 0x82: 0x201A, 0x83: 0x0453, 0x84: 0x201E, 0x85: 0x2026, 0x86: 0x2020, 0x87: 0x2021,
            0x88: 0x20AC, 0x89: 0x2030, 0x8A: 0x0409, 0x8B: 0x2039, 0x8C: 0x040A, 0x8D: 0x040C, 0x8E: 0x040B, 0x8F: 0x040F,
            0x90: 0x0452, 0x91: 0x2018, 0x92: 0x2019, 0x93: 0x201C, 0x94: 0x201D, 0x95: 0x2022, 0x96: 0x2013, 0x97: 0x2014,
            0x98: 0x0098, 0x99: 0x2122, 0x9A: 0x0459, 0x9B: 0x203A, 0x9C: 0x045A, 0x9D: 0x045C, 0x9E: 0x045B, 0x9F: 0x045F,
            0xA0: 0x00A0, 0xA1: 0x040E, 0xA2: 0x045E, 0xA3: 0x0408, 0xA4: 0x00A4, 0xA5: 0x0490, 0xA6: 0x00A6, 0xA7: 0x00A7,
            0xA8: 0x0401, 0xA9: 0x00A9, 0xAA: 0x0404, 0xAB: 0x00AB, 0xAC: 0x00AC, 0xAD: 0x00AD, 0xAE: 0x00AE, 0xAF: 0x0407,
            0xB0: 0x00B0, 0xB1: 0x00B1, 0xB2: 0x0406, 0xB3: 0x0456, 0xB4: 0x0491, 0xB5: 0x00B5, 0xB6: 0x00B6, 0xB7: 0x00B7,
            0xB8: 0x0451, 0xB9: 0x2116, 0xBA: 0x0454, 0xBB: 0x00BB, 0xBC: 0x0458, 0xBD: 0x0405, 0xBE: 0x0455, 0xBF: 0x0457
        };

        let result = '';
        for (let i = 0; i < bytes.length; i++) {
            const byte = bytes[i];
            if (byte < 0x80) {
                result += String.fromCharCode(byte);
            } else if (byte >= 0xC0) {
                result += String.fromCharCode(0x0410 + (byte - 0xC0));
            } else if (win1251Map[byte]) {
                result += String.fromCharCode(win1251Map[byte]);
            } else {
                result += String.fromCharCode(byte);
            }
        }
        return result;
    }

    decodeCP866(bytes) {
        let result = '';
        for (let i = 0; i < bytes.length; i++) {
            const byte = bytes[i];
            if (byte < 0x80) {
                result += String.fromCharCode(byte);
            } else if (byte >= 0x80 && byte <= 0x9F) {
                result += String.fromCharCode(0x0410 + (byte - 0x80));
            } else if (byte >= 0xA0 && byte <= 0xAF) {
                result += String.fromCharCode(0x0420 + (byte - 0xA0));
            } else if (byte >= 0xE0 && byte <= 0xEF) {
                result += String.fromCharCode(0x0430 + (byte - 0xE0));
            } else if (byte >= 0xF0 && byte <= 0xFF) {
                if (byte <= 0xF5) {
                    result += String.fromCharCode(0x0440 + (byte - 0xF0));
                } else if (byte === 0xF6) {
                    result += String.fromCharCode(0x0451);
                } else {
                    result += String.fromCharCode(byte);
                }
            } else {
                result += String.fromCharCode(byte);
            }
        }
        return result;
    }

    decodeKOI8R(bytes) {
        let result = '';
        for (let i = 0; i < bytes.length; i++) {
            const byte = bytes[i];
            if (byte < 0x80) {
                result += String.fromCharCode(byte);
            } else if (byte >= 0xC1 && byte <= 0xDF) {
                const offset = byte - 0xC1;
                if (offset < 16) {
                    result += String.fromCharCode(0x0430 + offset);
                } else {
                    result += String.fromCharCode(0x0430 + offset + 1);
                }
            } else if (byte >= 0xE0 && byte <= 0xFF) {
                const offset = byte - 0xE0;
                if (offset < 16) {
                    result += String.fromCharCode(0x0410 + offset);
                } else {
                    result += String.fromCharCode(0x0410 + offset + 1);
                }
            } else if (byte === 0xB3) {
                result += String.fromCharCode(0x0451);
            } else if (byte === 0xA3) {
                result += String.fromCharCode(0x0401);
            } else {
                result += String.fromCharCode(byte);
            }
        }
        return result;
    }

    // XML escaping
    escapeXml(text) {
        if (!text) return '';
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&apos;');
    }

    // EPUB to FB2 conversion
    async convertEpubToFb2(file) {
        const zip = await JSZip.loadAsync(file);

        const containerXml = await zip.file('META-INF/container.xml').async('string');
        const containerDoc = new DOMParser().parseFromString(containerXml, 'text/xml');
        const opfPath = containerDoc.querySelector('rootfile').getAttribute('full-path');
        const opfDir = opfPath.substring(0, opfPath.lastIndexOf('/'));

        const opfContent = await zip.file(opfPath).async('string');
        const opfDoc = new DOMParser().parseFromString(opfContent, 'text/xml');

        const title = opfDoc.querySelector('metadata title')?.textContent || 'Untitled';
        const creator = opfDoc.querySelector('metadata creator')?.textContent || 'Unknown Author';

        const spine = opfDoc.querySelectorAll('spine itemref');
        const manifest = {};
        opfDoc.querySelectorAll('manifest item').forEach(item => {
            manifest[item.getAttribute('id')] = {
                href: item.getAttribute('href'),
                type: item.getAttribute('media-type')
            };
        });

        this.images = {};
        const imageMap = {};
        let imageIndex = 0;

        // Load images
        for (let id in manifest) {
            const item = manifest[id];
            if (item.type && item.type.startsWith('image/')) {
                const imagePath = opfDir ? `${opfDir}/${item.href}` : item.href;
                try {
                    const imageData = await zip.file(imagePath).async('base64');
                    const imageId = `image${imageIndex++}`;
                    this.images[imageId] = `data:${item.type};base64,${imageData}`;
                    imageMap[item.href] = imageId;
                    imageMap[imagePath] = imageId;
                } catch (e) {
                    console.warn('Could not load image:', imagePath);
                }
            }
        }

        let fb2Content = `<?xml version="1.0" encoding="UTF-8"?>
<FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0" xmlns:l="http://www.w3.org/1999/xlink">
<description>
    <title-info>
        <book-title>${this.escapeXml(title)}</book-title>
        <author>
            <first-name>${this.escapeXml(creator)}</first-name>
        </author>
    </title-info>
</description>
<body>`;

        this.chapters = [];
        let chapterIndex = 0;

        for (let itemRef of spine) {
            const id = itemRef.getAttribute('idref');
            if (!manifest[id]) continue;

            const href = manifest[id].href;
            const chapterPath = opfDir ? `${opfDir}/${href}` : href;

            try {
                const chapterHtml = await zip.file(chapterPath).async('string');
                const fb2Chapter = this.convertHtmlToFb2(chapterHtml, opfDir, imageMap, chapterIndex);
                fb2Content += fb2Chapter;

                const chapterDoc = new DOMParser().parseFromString(chapterHtml, 'text/html');
                const chapterTitle = chapterDoc.querySelector('h1, h2, h3, title')?.textContent || `Chapter ${chapterIndex + 1}`;
                this.chapters.push({ title: chapterTitle, id: `chapter_${chapterIndex}` });
                chapterIndex++;
            } catch (e) {
                console.warn('Could not process chapter:', chapterPath);
            }
        }

        fb2Content += '</body>';

        // Add binary images
        for (let imageId in this.images) {
            const base64Data = this.images[imageId].split(',')[1];
            const mimeType = this.images[imageId].match(/data:([^;]+);/)[1];
            fb2Content += `<binary id="${imageId}" content-type="${mimeType}">${base64Data}</binary>`;
        }

        fb2Content += '</FictionBook>';
        return fb2Content;
    }

    // HTML to FB2 conversion - FIXED paragraph handling
    convertHtmlToFb2(html, baseDir, imageMap, chapterIndex) {
        const doc = new DOMParser().parseFromString(html, 'text/html');
        let fb2 = `<section id="chapter_${chapterIndex}">`;

        const processElement = (element) => {
            let result = '';

            for (let node of element.childNodes) {
                if (node.nodeType === Node.TEXT_NODE) {
                    const text = this.escapeXml(node.textContent);
                    // Only add text if it's not just whitespace
                    if (text.trim()) {
                        result += text;
                    }
                } else if (node.nodeType === Node.ELEMENT_NODE) {
                    const tag = node.tagName.toLowerCase();

                    switch (tag) {
                        case 'h1':
                        case 'h2':
                        case 'h3':
                        case 'h4':
                        case 'h5':
                        case 'h6':
                            result += `<title>${processElement(node)}</title>`;
                            break;
                        case 'p':
                            const content = processElement(node);
                            if (content.trim()) {
                                // Just add paragraph without extra spacing
                                result += `<p>${content}</p>`;
                            }
                            break;
                        case 'div':
                            // Process div content but check if it contains block elements
                            const divContent = processElement(node);
                            if (divContent.trim()) {
                                // Check if div contains its own paragraphs or just inline content
                                const hasBlockElements = Array.from(node.children).some(child =>
                                    ['p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'div', 'blockquote'].includes(child.tagName.toLowerCase())
                                );

                                if (!hasBlockElements && divContent.trim()) {
                                    // Treat as paragraph if div contains only inline content
                                    result += `<p>${divContent}</p>`;
                                } else {
                                    // Div contains block elements, just add the content
                                    result += divContent;
                                }
                            }
                            break;
                        case 'em':
                        case 'i':
                            result += `<emphasis>${processElement(node)}</emphasis>`;
                            break;
                        case 'strong':
                        case 'b':
                            result += `<strong>${processElement(node)}</strong>`;
                            break;
                        case 'br':
                            result += '<empty-line/>';
                            break;
                        case 'img':
                            let src = node.getAttribute('src');
                            if (src) {
                                src = src.replace(/^\.\.\//, '').replace(/^\.\//, '');
                                let imageId = null;
                                if (imageMap[src]) {
                                    imageId = imageMap[src];
                                } else {
                                    const fullPath = baseDir ? `${baseDir}/${src}` : src;
                                    if (imageMap[fullPath]) {
                                        imageId = imageMap[fullPath];
                                    }
                                }
                                if (imageId) {
                                    result += `<image l:href="#${imageId}"/>`;
                                }
                            }
                            break;
                        case 'a':
                            result += processElement(node);
                            break;
                        case 'blockquote':
                            result += `<cite>${processElement(node)}</cite>`;
                            break;
                        case 'pre':
                        case 'code':
                            const codeContent = node.textContent || '';
                            if (codeContent.trim()) {
                                result += `<p>${this.escapeXml(codeContent)}</p>`;
                            }
                            break;
                        case 'ul':
                        case 'ol':
                            for (let li of node.children) {
                                if (li.tagName && li.tagName.toLowerCase() === 'li') {
                                    result += `<p>• ${processElement(li)}</p>`;
                                }
                            }
                            break;
                        case 'hr':
                            result += '<empty-line/><p>* * *</p><empty-line/>';
                            needsSpacing = false;
                            break;
                        default:
                            // For any other tags, just process their content
                            result += processElement(node);
                    }
                }
            }

            return result;
        };

        fb2 += processElement(doc.body || doc.documentElement);
        fb2 += '</section>';

        return fb2;
    }

    // Extract FB2 TOC with tree structure
    extractFB2TOC(xmlDoc) {
        let sectionCounter = 0;

        // Helper function to extract title text from title element
        const getTitleText = (titleElement) => {
            if (!titleElement) return null;
            // Get text content, removing extra whitespace
            return titleElement.textContent.trim().replace(/\s+/g, ' ');
        };

        // Recursive function to process sections
        const processSections = (parentElement, level = 0) => {
            const sections = [];

            // Only process direct child elements
            for (let child of parentElement.children) {
                if (child.tagName.toLowerCase() === 'section') {
                    // Generate ID for this section
                    const sectionId = `section_${sectionCounter++}`;

                    // CRITICAL FIX: Set the ID as an attribute on the XML element
                    // This allows convertFB2ToHTML to find and use the same ID
                    child.setAttribute('data-toc-id', sectionId);

                    // Find direct child title element (not nested in subsections)
                    let titleElement = null;
                    for (let el of child.children) {
                        if (el.tagName.toLowerCase() === 'title') {
                            titleElement = el;
                            break;
                        }
                    }

                    const titleText = getTitleText(titleElement);

                    const sectionData = {
                        id: sectionId,
                        title: titleText || `Section ${sectionCounter}`,
                        level: level,
                        children: []
                    };

                    // Process nested sections recursively
                    const nestedSections = processSections(child, level + 1);
                    if (nestedSections.length > 0) {
                        sectionData.children = nestedSections;
                    }

                    sections.push(sectionData);
                }
            }

            return sections;
        };

        // Find the main body element
        const bodies = xmlDoc.getElementsByTagName('body');
        let mainBody = null;
        if (bodies.length > 0) {
            mainBody = bodies[0];
        }

        if (!mainBody) {
            console.warn('No main body found in FB2 document');
            return [];
        }

        // Extract TOC from main body only
        const toc = processSections(mainBody);
        return toc;
    }

    // FB2 to HTML conversion with TOC support and poetry tags
    convertFB2ToHTML(element) {
        let html = '';

        for (const child of element.childNodes) {
            if (child.nodeType === Node.TEXT_NODE) {
                html += child.textContent;
            } else if (child.nodeType === Node.ELEMENT_NODE) {
                switch (child.tagName.toLowerCase()) {
                    case 'title':
                        const level = Math.min(3, (child.parentNode.tagName === 'section' ? 2 : 1));
                        html += `<h${level}>${this.convertFB2ToHTML(child)}</h${level}>`;
                        break;
                    case 'p':
                        html += `<p>${this.convertFB2ToHTML(child)}</p>`;
                        break;
                    case 'emphasis':
                        html += `<em>${this.convertFB2ToHTML(child)}</em>`;
                        break;
                    case 'strong':
                        html += `<strong>${this.convertFB2ToHTML(child)}</strong>`;
                        break;
                    case 'section':
                        // Use the ID that was set by extractFB2TOC
                        const tocId = child.getAttribute('data-toc-id');
                        if (tocId) {
                            html += `<div id="${tocId}" class="section" style="scroll-margin-top: 3em;">${this.convertFB2ToHTML(child)}</div>`;
                        } else {
                            // Fallback if no TOC ID was set (shouldn't happen for main body sections)
                            html += `<div class="section" style="scroll-margin-top: 3em;">${this.convertFB2ToHTML(child)}</div>`;
                        }
                        break;
                    case 'image':
                        const href = child.getAttribute('l:href') || child.getAttribute('xlink:href');
                        const imageId = href?.replace('#', '');
                        if (imageId && this.images[imageId]) {
                            html += `<img src="${this.images[imageId]}" alt="Image">`;
                        }
                        break;
                    case 'empty-line':
                        html += '<br>';
                        break;

                    // Poetry tags support
                    case 'poem':
                        html += '<div class="poem">';
                        // Process poem title if exists
                        for (let poemChild of child.children) {
                            if (poemChild.tagName.toLowerCase() === 'title') {
                                html += `<h3 class="poem-title">${this.convertFB2ToHTML(poemChild)}</h3>`;
                                break;
                            }
                        }
                        html += this.convertFB2ToHTML(child);
                        html += '</div>';
                        break;

                    case 'stanza':
                        html += '<div class="stanza">';
                        html += this.convertFB2ToHTML(child);
                        html += '</div>';
                        break;

                    case 'v':
                        // Verse line
                        html += `<p class="verse">${this.convertFB2ToHTML(child)}</p>`;
                        break;

                    case 'text-author':
                        html += `<p class="text-author">${this.convertFB2ToHTML(child)}</p>`;
                        break;

                    case 'epigraph':
                        html += '<div class="epigraph">';
                        html += this.convertFB2ToHTML(child);
                        html += '</div>';
                        break;

                    case 'cite':
                        html += '<blockquote class="cite">';
                        html += this.convertFB2ToHTML(child);
                        html += '</blockquote>';
                        break;

                    case 'subtitle':
                        html += `<h3 class="subtitle">${this.convertFB2ToHTML(child)}</h3>`;
                        break;

                    case 'date':
                        html += `<p class="date">${this.convertFB2ToHTML(child)}</p>`;
                        break;

                    default:
                        html += this.convertFB2ToHTML(child);
                }
            }
        }

        return html;
    }

    // Generate HTML for tree-structured TOC
    generateTOCHTML(tocItems) {
        if (!tocItems || tocItems.length === 0) {
            return '';
        }

        let html = '<ul class="toc-tree">';

        for (const item of tocItems) {
            html += '<li>';
            html += `<a href="#${item.id}" class="toc-link">${this.escapeXml(item.title)}</a>`;

            // Add nested children if they exist
            if (item.children && item.children.length > 0) {
                html += this.generateTOCHTML(item.children);
            }

            html += '</li>';
        }

        html += '</ul>';
        return html;
    }

    // Language detection
    detectLanguage(text) {
        const cleanText = text.replace(/<[^>]*>/g, '');
        const sample = cleanText.substring(0, Math.min(2000, cleanText.length));

        const cyrillicPattern = /[\u0400-\u04FF]/g;
        const latinPattern = /[a-zA-Z]/g;

        const cyrillicMatches = (sample.match(cyrillicPattern) || []).length;
        const latinMatches = (sample.match(latinPattern) || []).length;

        if (cyrillicMatches > 0 && cyrillicMatches > latinMatches * 0.2) {
            return 'ru';
        }

        if (/[äöüßÄÖÜ]/.test(sample)) {
            return 'de';
        }

        if (/[éèêëàâçîôûÉÈÊËÀÂÇÎÔÛ]/.test(sample)) {
            return 'fr';
        }

        if (/[ąćęłńśźżĄĆĘŁŃŚŹŻ]/.test(sample)) {
            return 'pl';
        }

        if (/[ñáéíóúÑÁÉÍÓÚ]/.test(sample)) {
            return 'es';
        }

        if (/[àèéìòùÀÈÉÌÒÙ]/.test(sample)) {
            return 'it';
        }

        return 'en';
    }
}