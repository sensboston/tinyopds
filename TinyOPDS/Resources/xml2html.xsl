<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
	<xsl:param name="serverVersion" select="'TinyOPDS server'"/>
	<xsl:param name="libName" select="''"/>
	<xsl:param name="darkTheme" select="''"/>
	<xsl:param name="searchPlaceholder" select="'Search authors or books...'"/>
	<xsl:param name="searchButtonText" select="'Search'"/>
	<xsl:param name="formatText" select="'Format:'"/>
	<xsl:param name="sizeText" select="'Size:'"/>
	<xsl:param name="downloadText" select="'Download'"/>
	<xsl:param name="downloadEpubText" select="'Download EPUB'"/>
	<xsl:param name="readText" select="'Read'"/>

	<!-- Reader localization parameters -->
	<xsl:param name="readerTableOfContents" select="'Table of Contents'"/>
	<xsl:param name="readerOpenBook" select="'Open Book'"/>
	<xsl:param name="readerDecreaseFont" select="'Decrease Font'"/>
	<xsl:param name="readerIncreaseFont" select="'Increase Font'"/>
	<xsl:param name="readerChangeFont" select="'Change Font'"/>
	<xsl:param name="readerChangeTheme" select="'Change Theme'"/>
	<xsl:param name="readerDecreaseMargins" select="'Decrease Margins'"/>
	<xsl:param name="readerIncreaseMargins" select="'Increase Margins'"/>
	<xsl:param name="readerStandardWidth" select="'Standard Width'"/>
	<xsl:param name="readerFullWidth" select="'Full Width'"/>
	<xsl:param name="readerFullscreen" select="'Fullscreen'"/>
	<xsl:param name="readerLoading" select="'Loading...'"/>
	<xsl:param name="readerErrorLoading" select="'Error loading file'"/>
	<xsl:param name="readerNoTitle" select="'Untitled'"/>
	<xsl:param name="readerUnknownAuthor" select="'Unknown Author'"/>
	<xsl:param name="readerNoChapters" select="'No chapters available'"/>
	<xsl:param name="faviconIco" select="'/favicon.ico'"/>

	<xsl:template match="/">
		<xsl:variable name="id">
			<xsl:value-of select="x:feed/x:id"/>
		</xsl:variable>
		<xsl:variable name="title">
			<xsl:value-of select="x:feed/x:title"/>
		</xsl:variable>
		<xsl:variable name="icon">
			<xsl:value-of select="x:feed/x:icon"/>
		</xsl:variable>

		<html>
			<xsl:if test="$darkTheme">
				<xsl:attribute name="class">
					<xsl:value-of select="$darkTheme"/>
				</xsl:attribute>
			</xsl:if>
			<head>
				<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
				<meta name="viewport" content="width=device-width, initial-scale=1.0" />
				<link rel="icon" type="image/x-icon" href="{$faviconIco}?v=1"/>
				<style>
					html {
					scrollbar-gutter: stable;
					}

					body {
					margin: 0;
					padding: 0;
					font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
					background-color: #ffffff;
					line-height: 1.4;
					padding-top: 140px;
					}

					.fixed-header {
					position: fixed;
					top: 0;
					left: 0;
					right: 0;
					background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
					z-index: 1000;
					box-shadow: 0 4px 12px rgba(0,0,0,0.15);
					}

					.header-content {
					margin: 0 auto;
					max-width: 1200px;
					padding: 20px;
					display: flex;
					align-items: center;
					gap: 20px;
					}

					.header-icon {
					width: 80px;
					height: 80px;
					flex-shrink: 0;
					border-radius: 12px;
					box-shadow: 0 2px 8px rgba(0,0,0,0.2);
					background: rgba(255,255,255,0.1);
					padding: 8px;
					box-sizing: border-box;
					}

					.header-text {
					flex-grow: 1;
					color: white;
					}

					.server-title {
					color: white;
					margin: 0 0 8px 0;
					font-size: 28px;
					font-weight: 700;
					cursor: pointer;
					text-decoration: none;
					text-shadow: 0 2px 4px rgba(0,0,0,0.3);
					transition: all 0.3s ease;
					}

					.server-title:hover {
					color: #fff;
					text-shadow: 0 0 8px rgba(255,255,255,0.5);
					transform: translateY(-1px);
					}

					.library-name {
					color: rgba(255,255,255,0.9);
					font-size: 16px;
					font-weight: 500;
					margin: 0;
					text-shadow: 0 1px 2px rgba(0,0,0,0.2);
					}

					.search-section {
					width: 100%;
					margin-top: 20px;
					}

					.search-form {
					display: flex;
					width: 100%;
					gap: 12px;
					}

					.search-input {
					flex: 1;
					padding: 12px 16px;
					font-size: 16px;
					border: none;
					border-radius: 8px;
					background: rgba(255,255,255,0.95);
					backdrop-filter: blur(10px);
					box-shadow: 0 2px 8px rgba(0,0,0,0.1);
					transition: all 0.3s ease;
					outline: none;
					}

					.search-input:focus {
					background: white;
					box-shadow: 0 4px 16px rgba(0,0,0,0.2);
					transform: translateY(-1px);
					}

					.search-input::placeholder {
					color: #666;
					}

					.search-button {
					padding: 12px 24px;
					font-size: 16px;
					font-weight: 600;
					background: rgba(255,255,255,0.9);
					color: #764ba2;
					border: none;
					border-radius: 8px;
					cursor: pointer;
					box-shadow: 0 2px 8px rgba(0,0,0,0.1);
					transition: all 0.3s ease;
					flex-shrink: 0;
					}

					.search-button:hover {
					background: white;
					box-shadow: 0 4px 16px rgba(0,0,0,0.2);
					transform: translateY(-1px);
					}

					.search-button:active {
					transform: translateY(0);
					}

					.main-content {
					margin: 0 auto;
					max-width: 1200px;
					padding: 30px 20px;
					}

					ul {
					list-style-type: none;
					padding-left: 0;
					margin: 0;
					}

					.category-item {
					position: relative;
					padding: 8px 0;
					border-bottom: 1px solid #eee;
					transition: all 0.3s ease;
					}

					.category-item:last-child {
					border-bottom: none;
					}

					.category-item::after {
					content: '';
					display: block;
					clear: both;
					}

					.category-item:hover {
					background-color: #f8f9ff;
					padding-left: 8px;
					}

					.category-link {
					text-decoration: none;
					font-weight: 500;
					font-size: 18px;
					transition: all 0.3s ease;
					display: block;
					padding: 0;
					-webkit-tap-highlight-color: transparent;
					}

					.category-descr {
					margin-left: 20px;
					font-size: 14px;
					color: #666;
					margin-top: 8px;
					}

					.book-item {
					margin-bottom: 30px;
					background: white;
					padding: 20px;
					border-radius: 12px;
					box-shadow: 0 4px 12px rgba(0,0,0,0.08);
					position: relative;
					transition: all 0.3s ease;
					border: 1px solid #f0f0f0;
					}

					.download-date {
					position: absolute;
					top: 12px;
					right: 12px;
					font-size: 11px;
					color: #888;
					background: rgba(255,255,255,0.95);
					padding: 4px 8px;
					border-radius: 6px;
					box-shadow: 0 1px 4px rgba(0,0,0,0.1);
					font-weight: 500;
					z-index: 1;
					}

					.book-item:hover {
					box-shadow: 0 8px 24px rgba(0,0,0,0.12);
					transform: translateY(-2px);
					}

					.book-content {
					display: flex;
					position: relative;
					}

					.cover {
					width: 120px;
					height: 190px;
					max-width: 120px;
					flex-shrink: 0;
					margin-right: 0;
					border-radius: 8px;
					object-fit: cover;
					box-shadow: 0 2px 8px rgba(0,0,0,0.15);
					background: url('/book_cover.jpg') center/cover no-repeat;
					background-color: #f0f0f0;
					}

					.book-details {
					flex: 1;
					min-width: 0;
					}

					.book-header {
					display: flex;
					justify-content: space-between;
					align-items: flex-start;
					margin-bottom: 12px;
					flex-wrap: wrap;
					gap: 10px;
					}

					.book-title {
					font-weight: 600;
					color: #333;
					font-size: 18px;
					flex: 1;
					min-width: 0;
					line-height: 1.3;
					}

					.book-info {
					font-size: 12px;
					color: #666;
					background: #f8f9fa;
					padding: 6px 8px;
					border-radius: 6px;
					margin-top: 3px;
					margin-bottom: 2px;
					line-height: 1.4;
					width: 100%;
					box-sizing: border-box;
					}

					.book-info .info-line {
					margin-bottom: 2px;
					display: flex;
					justify-content: space-between;
					align-items: center;
					}

					.book-info .info-line:last-child {
					margin-bottom: 0;
					}

					.book-info .info-line strong {
					font-weight: 600;
					}

					.book-info .info-line span {
					text-align: right;
					font-weight: 600;
					}

					.book-author {
					font-size: 14px;
					color: #667eea;
					text-decoration: none;
					margin-bottom: 12px;
					display: block;
					font-weight: 500;
					transition: all 0.3s ease;
					}

					.book-author:hover {
					color: #764ba2;
					text-decoration: none;
					}

					.book-descr {
					font-size: 14px;
					line-height: 1.4;
					color: #555;
					margin-bottom: 15px;
					text-align: justify;
					hyphens: auto !important;
					-webkit-hyphens: auto !important;
					-moz-hyphens: auto !important;
					-ms-hyphens: auto !important;
					word-wrap: break-word;
					overflow-wrap: break-word;
					white-space: pre-line;
					}

					.download-section {
					display: flex;
					flex-direction: column;
					align-items: flex-start;
					width: 120px;
					flex-shrink: 0;
					margin-right: 25px;
					}

					.read-button {
					font-size: 12px;
					padding: 8px 12px;
					text-decoration: none;
					border-radius: 6px;
					text-align: center;
					font-weight: 600;
					display: block;
					width: 100%;
					box-sizing: border-box;
					background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
					color: white;
					margin-top: 2px;
					transition: all 0.3s ease;
					box-shadow: 0 2px 6px rgba(102, 126, 234, 0.3);
					}

					.read-button:hover {
					background: linear-gradient(135deg, #5a67d8 0%, #6b46c1 100%);
					color: white;
					text-decoration: none;
					transform: translateY(-1px);
					box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
					}

					.download-links {
					display: flex;
					flex-direction: row;
					gap: 4px;
					margin-top: 6px;
					width: 120px;
					}

					.download-link {
					font-size: 11px;
					padding: 6px 4px;
					text-decoration: none;
					border-radius: 6px;
					text-align: center;
					font-weight: 600;
					display: block;
					width: 58px;
					box-sizing: border-box;
					transition: all 0.3s ease;
					}

					.download-fb2 {
					background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
					color: white;
					box-shadow: 0 2px 6px rgba(72, 187, 120, 0.3);
					}

					.download-fb2:hover {
					background: linear-gradient(135deg, #38a169 0%, #2f855a 100%);
					color: white;
					text-decoration: none;
					transform: translateY(-1px);
					box-shadow: 0 4px 12px rgba(72, 187, 120, 0.4);
					}

					.download-epub {
					background: linear-gradient(135deg, #4299e1 0%, #3182ce 100%);
					color: white;
					box-shadow: 0 2px 6px rgba(66, 153, 225, 0.3);
					}

					.download-epub:hover {
					background: linear-gradient(135deg, #3182ce 0%, #2c5282 100%);
					color: white;
					text-decoration: none;
					transform: translateY(-1px);
					box-shadow: 0 4px 12px rgba(66, 153, 225, 0.4);
					}

					h4 {
					color: #333;
					margin-top: 0;
					margin-bottom: 25px;
					font-size: 24px;
					font-weight: 600;
					}

					.loading-indicator {
					text-align: center;
					padding: 40px 20px;
					display: none;
					}

					.loading-indicator.active {
					display: block;
					}

					.loading-spinner {
					display: inline-block;
					width: 40px;
					height: 40px;
					border: 4px solid #f3f3f3;
					border-top: 4px solid #667eea;
					border-radius: 50%;
					animation: spin 1s linear infinite;
					margin-bottom: 12px;
					}

					@keyframes spin {
					0% { transform: rotate(0deg); }
					100% { transform: rotate(360deg); }
					}

					.loading-text {
					color: #666;
					font-size: 14px;
					font-weight: 500;
					}

					.end-message {
					text-align: center;
					padding: 40px 20px;
					color: #888;
					font-size: 14px;
					display: none;
					}

					.end-message.active {
					display: block;
					}

					.error-message {
					text-align: center;
					padding: 20px;
					background: #fee;
					color: #c33;
					border-radius: 8px;
					margin: 20px 0;
					display: none;
					}

					.error-message.active {
					display: block;
					}

					@media (min-width: 769px) and (max-width: 1100px) {
					.cover { width: 160px; height: 240px; }
					.download-section { width: 160px; }
					.download-links { width: 100%; }
					.download-link { width: calc(50% - 4px); }
					}

					/* Mobile Responsive Design */
					@media (max-width: 768px) {
					body {
					padding-top: 130px;
					}

					.header-content {
					padding: 10px 16px;
					flex-direction: column;
					align-items: flex-start;
					gap: 10px;
					}

					.header-main {
					display: flex;
					align-items: center;
					gap: 14px;
					width: 100%;
					}

					.header-icon {
					width: 56px;
					height: 56px;
					}

					.server-title {
					font-size: 22px;
					margin-bottom: 4px;
					}

					.library-name {
					font-size: 14px;
					}

					.search-section {
					width: 100%;
					margin-top: 0;
					padding-right: 18px;
					}

					.search-form {
					flex-direction: row;
					gap: 10px;
					}

					.search-input {
					flex: 1;
					font-size: 16px;
					max-width: calc(100% - 90px);
					}

					.search-button {
					flex-shrink: 0;
					padding: 12px 16px;
					}

					.main-content {
					padding: 20px 16px;
					}

					.book-item {
					padding: 16px;
					overflow: hidden;
					}

					.download-date {
					position: static;
					float: right;
					font-size: 10px;
					padding: 3px 6px;
					margin-left: 10px;
					margin-bottom: 5px;
					}

					.book-content {
					display: block;
					position: relative;
					}

					.cover {
					width: 100px;
					height: 150px;
					margin-right: 0;
					margin-bottom: 4px;
					background: url('/book_cover.jpg') center/cover no-repeat;
					background-color: #f0f0f0;
					}

					.download-section {
					float: left;
					width: 100px;
					margin-right: 15px;
					margin-bottom: 10px;
					}

					.book-details {
					display: block;
					margin: 0;
					}

					.download-links {
					width: 100px;
					}

					.download-link {
					width: 48px;
					}

					.book-header {
					margin-bottom: 10px;
					clear: none;
					}

					.book-title {
					font-size: 16px;
					margin-bottom: 6px;
					}

					.book-info {
					font-size: 11px;
					padding: 4px 6px;
					margin-top: 2px;
					margin-bottom: 2px;
					width: 100%;
					box-sizing: border-box;
					}

					.book-info .info-line {
					display: flex;
					justify-content: space-between;
					margin-bottom: 2px;
					}

					.book-info .info-line:last-child {
					margin-bottom: 0;
					}

					.book-info .info-line span {
					font-weight: 600;
					}

					.read-button {
					margin-top: 2px;
					}

					.download-links {
					margin-top: 6px;
					}

					.book-author {
					display: block;
					margin-bottom: 10px;
					clear: none;
					}

					.book-descr {
					font-size: 13px;
					line-height: 1.5;
					margin: 0;
					text-align: justify;
					hyphens: auto;
					-webkit-hyphens: auto;
					-moz-hyphens: auto;
					-ms-hyphens: auto;
					word-wrap: break-word;
					overflow-wrap: break-word;
					white-space: pre-line;
					}
					}

					@media (min-width: 769px) {
					.header-content {
					flex-direction: row;
					align-items: center;
					}

					.header-main {
					display: flex;
					align-items: center;
					gap: 20px;
					flex: 1;
					}

					.search-section {
					width: 400px;
					margin-top: 0;
					}
					}
					.dark body {
					background-color: #121212;
					color: #ddd;
					}

					.dark .fixed-header {
					background: linear-gradient(135deg, #252e57 0%, #3f1f5f 100%);
					box-shadow: 0 4px 12px rgba(0, 0, 0, 0.8);
					}

					.dark .header-icon {
					box-shadow: 0 2px 8px rgba(0, 0, 0, 0.8);
					background: rgba(0, 0, 0, 0.3);
					}

					.dark .header-text {
					color: #eee;
					}

					.dark .server-title {
					color: #eee;
					text-shadow: 0 2px 4px rgba(0, 0, 0, 0.8);
					}

					.dark .server-title:hover {
					color: #fff;
					text-shadow: 0 0 12px rgba(200, 200, 255, 0.8);
					}

					.dark .library-name {
					color: rgba(230, 230, 230, 0.9);
					}

					.dark .search-input {
					background: rgba(255, 255, 255, 0.1);
					color: #eee;
					box-shadow: 0 2px 8px rgba(255, 255, 255, 0.05);
					}

					.dark .search-input:focus {
					background: rgba(255, 255, 255, 0.15);
					box-shadow: 0 4px 16px rgba(255, 255, 255, 0.2);
					}

					.dark .search-input::placeholder {
					color: #aaa;
					}

					.dark .search-button {
					background: rgba(102, 126, 234, 0.9);
					color: white;
					box-shadow: 0 2px 8px rgba(102, 126, 234, 0.5);
					}

					.dark .search-button:hover {
					background: rgba(102, 126, 234, 1);
					box-shadow: 0 4px 16px rgba(102, 126, 234, 0.7);
					}

					.dark .main-content {
					color: #ddd;
					}

					.dark .category-item {
					border-bottom: 1px solid #333;
					}

					.dark .category-item:hover {
					background-color: #222530;
					}

					.dark .category-link {
					color: #ccc;
					}

					.dark .category-descr {
					color: #999;
					}

					.dark .book-item {
					background: #1c1c1c;
					box-shadow: 0 4px 12px rgba(0, 0, 0, 0.8);
					border: 1px solid #333;
					color: #ddd;
					}

					.dark .download-date {
					color: #aaa;
					background: rgba(30, 30, 30, 0.95);
					box-shadow: 0 1px 4px rgba(0, 0, 0, 0.8);
					}

					.dark .book-item:hover {
					box-shadow: 0 8px 24px rgba(102, 126, 234, 0.8);
					}

					.dark .cover {
					box-shadow: 0 2px 8px rgba(102, 126, 234, 0.6);
					background-color: #222;
					}

					.dark .book-title {
					color: #ddd;
					}

					.dark .book-info {
					color: #bbb;
					background: #2a2a2a;
					}

					.dark .book-info .info-line strong {
					color: #ddd;
					}

					.dark .book-info .info-line span {
					color: #ddd;
					}

					.dark .book-author {
					color: #7b8bee;
					}

					.dark .book-author:hover {
					color: #9f85d9;
					}

					.dark .book-descr {
					color: #aaa;
					}

					.dark .read-button {
					background: linear-gradient(135deg, #4a4e70 0%, #3b2c5a 100%);
					color: white;
					box-shadow: 0 2px 6px rgba(74, 78, 112, 0.6);
					}

					.dark .read-button:hover {
					background: linear-gradient(135deg, #3c3f59 0%, #2e2844 100%);
					color: white;
					box-shadow: 0 4px 12px rgba(74, 78, 112, 0.8);
					}

					.dark .download-link {
					color: white;
					}

					.dark .download-fb2 {
					background: linear-gradient(135deg, #2e6f3a 0%, #25582e 100%);
					color: white;
					box-shadow: 0 2px 6px rgba(46, 111, 58, 0.6);
					}

					.dark .download-fb2:hover {
					background: linear-gradient(135deg, #25582e 0%, #1d4324 100%);
					color: white;
					box-shadow: 0 4px 12px rgba(46, 111, 58, 0.8);
					}

					.dark .download-epub {
					background: linear-gradient(135deg, #2a5c93 0%, #234872 100%);
					color: white;
					box-shadow: 0 2px 6px rgba(42, 92, 147, 0.6);
					}

					.dark .download-epub:hover {
					background: linear-gradient(135deg, #234872 0%, #1b3553 100%);
					color: white;
					box-shadow: 0 4px 12px rgba(42, 92, 147, 0.8);
					}

					.dark h4 {
					color: #ddd;
					}

					.dark .loading-spinner {
					border: 4px solid #2a2a2a;
					border-top: 4px solid #667eea;
					}

					.dark .loading-text {
					color: #aaa;
					}

					.dark .end-message {
					color: #666;
					}

					.dark .error-message {
					background: #330000;
					color: #ff6b6b;
					}

					.book-descr-wrap{position:relative}
					.descr-toggle{position:absolute;right:-8px;top:-20px;width:18px;height:18px;border-radius:4px;border:1px solid #ccd;display:none;align-items:center;justify-content:center;font-size:16px;line-height:1;cursor:pointer;background:transparent}
					@media (max-width:768px){
					.descr-toggle{display:flex}
					.book-descr.collapsed{overflow:hidden}
					.book-descr.expanded{max-height:none}
					}
					.dark .descr-toggle{background:transparent;border-color:#444;color:#eee}
				</style>
				<script src="/smart-header.js">
					<xsl:text> </xsl:text>
				</script>
				<script src="/infinite-scroll.js">
					<xsl:text> </xsl:text>
				</script>
				<script>
					// Store reader localization strings in localStorage for reader.html
					(function() {
					var readerLocalization = {
					'tableOfContents': '<xsl:value-of select="$readerTableOfContents"/>',
					'openBook': '<xsl:value-of select="$readerOpenBook"/>',
					'decreaseFont': '<xsl:value-of select="$readerDecreaseFont"/>',
					'increaseFont': '<xsl:value-of select="$readerIncreaseFont"/>',
					'changeFont': '<xsl:value-of select="$readerChangeFont"/>',
					'changeTheme': '<xsl:value-of select="$readerChangeTheme"/>',
					'decreaseMargins': '<xsl:value-of select="$readerDecreaseMargins"/>',
					'increaseMargins': '<xsl:value-of select="$readerIncreaseMargins"/>',
					'standardWidth': '<xsl:value-of select="$readerStandardWidth"/>',
					'fullWidth': '<xsl:value-of select="$readerFullWidth"/>',
					'fullscreen': '<xsl:value-of select="$readerFullscreen"/>',
					'loading': '<xsl:value-of select="$readerLoading"/>',
					'errorLoading': '<xsl:value-of select="$readerErrorLoading"/>',
					'noTitle': '<xsl:value-of select="$readerNoTitle"/>',
					'unknownAuthor': '<xsl:value-of select="$readerUnknownAuthor"/>',
					'noChapters': '<xsl:value-of select="$readerNoChapters"/>'
					};

					try {
					localStorage.setItem('tinyopds-localization', JSON.stringify(readerLocalization));
					} catch(e) {
					// localStorage might not be available
					console.warn('Could not save reader localization to localStorage:', e);
					}
					})();
				</script>

				<script>
					(function(){
					function setup(){
					if(!window.matchMedia || !window.matchMedia('(max-width: 768px)').matches) return;
					document.querySelectorAll('.book-item').forEach(function(item){
					var wrap = item.querySelector('.book-descr-wrap');
					var descr = item.querySelector('.book-descr');
					var toggle = item.querySelector('.descr-toggle');
					var left = item.querySelector('.download-section');
					if(!wrap || !descr || !toggle || !left) return;

					var h = left.getBoundingClientRect().height / 2;

					// Check if this item was already initialized
					var alreadyInitialized = toggle.hasAttribute('data-initialized');

					if(!alreadyInitialized){
					// First time initialization - set as collapsed
					descr.style.maxHeight = Math.max(0, Math.round(h)) + 'px';
					descr.classList.add('collapsed');
					toggle.setAttribute('aria-expanded','false');
					} else {
					// Already initialized - only update height if collapsed
					if(descr.classList.contains('collapsed')){
					descr.style.maxHeight = Math.max(0, Math.round(h)) + 'px';
					}
					// If expanded, don't touch it to avoid collapsing during scroll
					}

					// Add click handler only once
					if(!alreadyInitialized){
					toggle.setAttribute('data-initialized', 'true');
					toggle.addEventListener('click', function(){
					var isCollapsed = descr.classList.contains('collapsed');
					if(isCollapsed){
					descr.classList.remove('collapsed');
					descr.classList.add('expanded');
					descr.style.maxHeight = 'none';
					toggle.textContent = '−';
					toggle.setAttribute('aria-expanded','true');
					toggle.title = '-';
					} else {
					var h2 = left.getBoundingClientRect().height / 2;
					descr.style.maxHeight = Math.max(0, Math.round(h2)) + 'px';
					descr.classList.remove('expanded');
					descr.classList.add('collapsed');
					toggle.textContent = '+';
					toggle.setAttribute('aria-expanded','false');
					toggle.title = '+';
					}
					});
					}
					});
					}
					if(document.readyState === 'loading'){ document.addEventListener('DOMContentLoaded', setup); } else { setup(); }
					window.addEventListener('resize', function(){ setup(); });
					})();
				</script>				
				
			</head>
			<body>
				<div class="fixed-header">
					<div class="header-content">
						<div class="header-main">
							<a href="/">
								<img alt="TinyOPDS" class="header-icon">
									<xsl:attribute name="src">
										data:image/png;base64, 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
									</xsl:attribute>
								</img>
							</a>

							<div class="header-text">
								<div>
									<a href="/" class="server-title">
										<xsl:choose>
											<xsl:when test="contains($serverVersion, 'version ')">
												<xsl:value-of select="concat(substring-before($serverVersion, 'version '), 'v. ', substring-after($serverVersion, 'version '))"/>
											</xsl:when>
											<xsl:otherwise>
												<xsl:value-of select="$serverVersion"/>
											</xsl:otherwise>
										</xsl:choose>
									</a>
								</div>
								<div class="library-name">
									<a href="/" style="color: inherit; text-decoration: none;">
										<xsl:value-of select="$libName"/>
									</a>
								</div>
							</div>
						</div>

						<div class="search-section">
							<!-- Added onsubmit handler to prevent empty search submission -->
							<form method="get" action="/search" class="search-form" onsubmit="return this.searchTerm.value.trim() !== '';">
								<input type="text" name="searchTerm" class="search-input">
									<xsl:attribute name="placeholder">
										<xsl:value-of select="$searchPlaceholder"/>
									</xsl:attribute>
								</input>
								<input type="submit" class="search-button">
									<xsl:attribute name="value">
										<xsl:value-of select="$searchButtonText"/>
									</xsl:attribute>
								</input>
							</form>
						</div>
					</div>
				</div>

				<div class="main-content">
					<xsl:if test="$id = 'tag:root'">
						<ul>
							<xsl:for-each select="x:feed/x:entry">
								<li class="category-item">
									<a class="category-link" href="{x:link/@href}">
										<span class="category-title">
											<xsl:value-of select="x:title"/>
										</span>
										<div class="category-descr">
											<xsl:value-of select="x:content"/>
										</div>
									</a>
								</li>
							</xsl:for-each>
						</ul>
					</xsl:if>

					<xsl:if test="$id != 'tag:root' and (($icon='/genres.ico') or ($icon='/series.ico') or ($icon='/authors.ico') or ($icon='/library.ico'))">
						<h4>
							<xsl:value-of select="$title" />
						</h4>
						<ul>
							<xsl:for-each select="x:feed/x:entry">
								<li class="category-item">
									<a class="category-link" href="{x:link/@href}">
										<span class="category-title">
											<xsl:value-of select="x:title"/>
										</span>
										<div class="category-descr">
											<xsl:value-of select="x:content"/>
										</div>
									</a>
								</li>
							</xsl:for-each>
						</ul>
					</xsl:if>

					<xsl:if test="$icon = '/icons/books.ico'">
						<h4>
							<xsl:value-of select="$title" />
						</h4>

						<!-- Store navigation data and feed type -->
						<xsl:variable name="nextPageUrl">
							<xsl:value-of select="x:feed/x:link[@rel='next']/@href"/>
						</xsl:variable>

						<!-- Extract current page info from title -->
						<xsl:variable name="pageInfo">
							<xsl:if test="contains($title, 'Page ')">
								<xsl:value-of select="substring-after($title, 'Page ')"/>
							</xsl:if>
						</xsl:variable>

						<!-- Books container with navigation data attributes -->
						<ul id="books-container">
							<xsl:attribute name="data-next-page">
								<xsl:value-of select="$nextPageUrl"/>
							</xsl:attribute>
							<xsl:attribute name="data-page-info">
								<xsl:value-of select="$pageInfo"/>
							</xsl:attribute>
							<xsl:attribute name="data-feed-id">
								<xsl:value-of select="$id"/>
							</xsl:attribute>

							<xsl:for-each select="x:feed/x:entry">
								<li class="book-item">
									<!-- Display download date if available -->
									<xsl:if test="x:lastDownload">
										<div class="download-date">
											<xsl:value-of select="x:lastDownload"/>
										</div>
									</xsl:if>
									<div class="book-content">
										<div class="download-section">
											<img class="cover">
												<xsl:attribute name="src">
													<xsl:value-of select="x:link[attribute::type='image/jpeg']/@href"/>
												</xsl:attribute>
											</img>

											<!-- Book info moved here from header -->
											<div class="book-info">
												<div class="info-line">
													<strong>
														<xsl:value-of select="$formatText"/>
													</strong>
													<span>
														<xsl:value-of select="x:format"/>
													</span>
												</div>
												<div class="info-line">
													<strong>
														<xsl:value-of select="$sizeText"/>
													</strong>
													<span>
														<xsl:value-of select="x:size"/>
													</span>
												</div>
											</div>

											<!-- Extract book ID from download links -->
											<xsl:variable name="bookId">
												<xsl:choose>
													<xsl:when test="x:link[attribute::type='application/fb2+zip']">
														<xsl:call-template name="extract-book-id">
															<xsl:with-param name="href" select="x:link[attribute::type='application/fb2+zip']/@href"/>
														</xsl:call-template>
													</xsl:when>
													<xsl:when test="x:link[attribute::type='application/epub+zip']">
														<xsl:call-template name="extract-book-id">
															<xsl:with-param name="href" select="x:link[attribute::type='application/epub+zip']/@href"/>
														</xsl:call-template>
													</xsl:when>
												</xsl:choose>
											</xsl:variable>

											<!-- Read button -->
											<xsl:if test="$bookId != ''">
												<a class="read-button">
													<xsl:attribute name="href">
														<xsl:text>/reader/</xsl:text>
														<xsl:value-of select="$bookId"/>
													</xsl:attribute>
													<xsl:value-of select="$readText"/>
												</a>
											</xsl:if>

											<div class="download-links">
												<!-- FB2 Download button -->
												<xsl:if test="x:link[attribute::type='application/fb2+zip'] or x:format = 'fb2'">
													<a class="download-link download-fb2">
														<xsl:attribute name="href">
															<xsl:text>/download/</xsl:text>
															<xsl:value-of select="$bookId"/>
															<xsl:text>/fb2</xsl:text>
														</xsl:attribute>
														FB2
													</a>
												</xsl:if>

												<!-- EPUB Download button -->
												<a class="download-link download-epub">
													<xsl:attribute name="href">
														<xsl:text>/download/</xsl:text>
														<xsl:value-of select="$bookId"/>
														<xsl:text>/epub</xsl:text>
													</xsl:attribute>
													ePub
												</a>
											</div>
										</div>

										<div class="book-details">
											<div class="book-header">
												<div class="book-title">
													<xsl:value-of select="x:title" />
												</div>
												<!-- Book info removed from here -->
											</div>

											<a class="book-author">
												<xsl:attribute name="href">
													<xsl:value-of select="x:author/x:uri" />
												</xsl:attribute>
												<xsl:value-of select="x:author/x:name"/>
											</a>

											<div class="book-descr-wrap">
												<div class="book-descr" lang="ru">
													<xsl:value-of select="x:content"/>
												</div>
												<button class="descr-toggle" type="button" aria-expanded="false" title="+">+</button>
											</div>
										</div>
									</div>
								</li>
							</xsl:for-each>
						</ul>

						<!-- Loading indicator -->
						<div id="loading-indicator" class="loading-indicator">
							<div class="loading-spinner"></div>
							<div class="loading-text">Loading more books...</div>
						</div>

						<!-- End of list message -->
						<div id="end-message" class="end-message">
							No more books to load
						</div>

						<!-- Error message -->
						<div id="error-message" class="error-message">
							Error loading books. Please try again.
						</div>
					</xsl:if>
				</div>
			</body>
		</html>
	</xsl:template>

	<!-- Template to extract book ID from existing download links -->
	<xsl:template name="extract-book-id">
		<xsl:param name="href"/>
		<xsl:choose>
			<!-- Handle old format: /fb2/{guid}/filename.fb2.zip -->
			<xsl:when test="contains($href, '/fb2/')">
				<xsl:variable name="afterFb2" select="substring-after($href, '/fb2/')"/>
				<xsl:choose>
					<xsl:when test="contains($afterFb2, '/')">
						<xsl:value-of select="substring-before($afterFb2, '/')"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$afterFb2"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:when>
			<!-- Handle old format: /epub/{guid}/filename.epub -->
			<xsl:when test="contains($href, '/epub/')">
				<xsl:variable name="afterEpub" select="substring-after($href, '/epub/')"/>
				<xsl:choose>
					<xsl:when test="contains($afterEpub, '/')">
						<xsl:value-of select="substring-before($afterEpub, '/')"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$afterEpub"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:when>
			<!-- Handle new format: /download/{guid}/fb2 or /download/{guid}/epub -->
			<xsl:when test="contains($href, '/download/')">
				<xsl:variable name="afterDownload" select="substring-after($href, '/download/')"/>
				<xsl:choose>
					<xsl:when test="contains($afterDownload, '/')">
						<xsl:value-of select="substring-before($afterDownload, '/')"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$afterDownload"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$href"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
</xsl:stylesheet>