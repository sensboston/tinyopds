<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
	<xsl:param name="serverVersion" select="'TinyOPDS server'"/>
	<xsl:param name="libName" select="''"/>
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
			<head>
				<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
				<meta name="viewport" content="width=device-width, initial-scale=1.0" />
				<style>
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
					padding: 16px 0;
					border-bottom: 1px solid #eee;
					transition: all 0.3s ease;
					}

					.category-item:last-child {
					border-bottom: none;
					}

					.category-item:hover {
					background-color: #f8f9ff;
					padding-left: 8px;
					}

					.category-link {
					color: #667eea;
					text-decoration: none;
					font-weight: 500;
					font-size: 18px;
					transition: all 0.3s ease;
					}

					.category-link:hover {
					color: #764ba2;
					text-decoration: none;
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
					text-align: right;
					white-space: nowrap;
					flex-shrink: 0;
					background: #f8f9fa;
					padding: 4px 8px;
					border-radius: 4px;
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
					margin-top: 8px;
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
					margin-top: 12px;
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

					/* Mobile Responsive Design */
					@media (max-width: 768px) {
					body {
					padding-top: 140px;
					}

					.header-content {
					padding: 12px 16px;
					flex-direction: column;
					align-items: flex-start;
					gap: 12px;
					}

					.header-main {
					display: flex;
					align-items: center;
					gap: 16px;
					width: 100%;
					}

					.header-icon {
					width: 60px;
					height: 60px;
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
					font-size: 16px; /* Prevents zoom on iOS */
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
					/* Clearfix for float */
					overflow: hidden;
					}

					.book-content {
					/* Remove flex on mobile */
					display: block;
					position: relative;
					}

					.cover {
					width: 100px;
					height: 150px;
					margin-right: 0;
					margin-bottom: 8px;
					}

					.download-section {
					/* Float left for text wrapping */
					float: left;
					width: 100px;
					margin-right: 15px;
					margin-bottom: 10px;
					}

					.book-details {
					/* Remove flex properties, make it normal block */
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
					/* Keep the header above the floated section */
					margin-bottom: 10px;
					clear: none;
					}

					.book-title {
					font-size: 16px;
					margin-bottom: 6px;
					}

					.book-info {
					text-align: left;
					display: inline-block;
					margin-top: 4px;
					}

					.book-author {
					/* Author stays in normal flow */
					display: block;
					margin-bottom: 10px;
					clear: none;
					}

					.book-descr {
					/* Description will wrap around the floated download section */
					font-size: 13px;
					line-height: 1.5;
					margin: 0;
					text-align: justify;
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
				</style>
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
			</head>
			<body>
				<div class="fixed-header">
					<div class="header-content">
						<div class="header-main">
							<a href="/">
								<img src="/logo.png" alt="TinyOPDS" class="header-icon"/>
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
							<form method="get" action="/search" class="search-form">
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
									<a class="category-link">
										<xsl:attribute name="href">
											<xsl:value-of select="x:link/@href" />
										</xsl:attribute>
										<xsl:value-of select="x:title" />
									</a>
									<div class="category-descr">
										<xsl:value-of select="x:content"/>
									</div>
								</li>
							</xsl:for-each>
						</ul>
					</xsl:if>

					<xsl:if test="$id != 'tag:root' and (($icon='/genres.ico') or ($icon='/series.ico') or ($icon='/authors.ico') or ($icon='/favicon.ico'))">
						<h4>
							<xsl:value-of select="$title" />
						</h4>
						<ul>
							<xsl:for-each select="x:feed/x:entry">
								<li class="category-item">
									<a class="category-link">
										<xsl:attribute name="href">
											<xsl:value-of select="x:link/@href" />
										</xsl:attribute>
										<xsl:value-of select="x:title" />
									</a>
									<div class="category-descr">
										<xsl:value-of select="x:content"/>
									</div>
								</li>
							</xsl:for-each>
						</ul>
					</xsl:if>

					<xsl:if test="$icon = '/icons/books.ico'">
						<h4>
							<xsl:value-of select="$title" />
						</h4>
						<ul>
							<xsl:for-each select="x:feed/x:entry">
								<li class="book-item">
									<div class="book-content">
										<div class="download-section">
											<img class="cover">
												<xsl:attribute name="src">
													<xsl:value-of select="x:link[attribute::type='image/jpeg']/@href"/>
												</xsl:attribute>
											</img>

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
												<div class="book-info">
													<strong>
														<xsl:value-of select="$formatText"/>
													</strong>
													<xsl:text> </xsl:text>
													<xsl:value-of select="x:format"/>
													<br/>
													<strong>
														<xsl:value-of select="$sizeText"/>
													</strong>
													<xsl:text> </xsl:text>
													<xsl:value-of select="x:size"/>
												</div>
											</div>

											<a class="book-author">
												<xsl:attribute name="href">
													<xsl:value-of select="x:author/x:uri" />
												</xsl:attribute>
												<xsl:value-of select="x:author/x:name"/>
											</a>

											<div class="book-descr">
												<xsl:value-of select="x:content"/>
											</div>
										</div>
									</div>
								</li>
							</xsl:for-each>
						</ul>
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