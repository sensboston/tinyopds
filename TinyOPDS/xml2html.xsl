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
					background-color: #fafafa;
					line-height: 1.4;
					padding-top: 140px;
					}

					.fixed-header {
					position: fixed;
					top: 0;
					left: 0;
					right: 0;
					background-color: white;
					border-bottom: 2px solid #e0e0e0;
					z-index: 1000;
					box-shadow: 0 2px 4px rgba(0,0,0,0.1);
					}

					.header-content {
					margin: 0 auto;
					max-width: 500px;
					padding: 16px 20px;
					display: flex;
					align-items: center;
					position: relative;
					}

					.header-icon {
					width: 64px;
					height: 64px;
					margin-right: 0;
					flex-shrink: 0;
					}

					.header-text {
					flex-grow: 1;
					text-align: center;
					margin: 0;
					position: absolute;
					left: 50%;
					transform: translateX(-50%);
					width: calc(100% - 48px);
					top: 8px;
					}

					.server-title {
					color: #333;
					margin: 0 0 3px 0;
					font-size: 24px;
					font-weight: bold;
					cursor: pointer;
					text-decoration: none;
					}

					.server-title:hover {
					color: #007cba;
					}

					.library-name {
					color: #007cba;
					font-size: 14px;
					font-weight: bold;
					margin: 10px 0 0 0;
					}

					.search-section {
					margin-top: 5px;
					text-align: center;
					padding-top: 0px;
					}

					.search-form {
					display: inline-block;
					}

					.search-input {
					width: 430px;
					max-width: calc(100vw - 140px);
					padding: 6px 4px;
					font-size: 13px;
					border: 1px solid #ddd;
					border-radius: 4px;
					box-sizing: border-box;
					}

					.search-button {
					padding: 8px 4px;
					font-size: 13px;
					background-color: #007cba;
					color: white;
					border: none;
					border-radius: 4px;
					cursor: pointer;
					margin-left: 12px;
					}

					.search-button:hover {
					background-color: #005a87;
					}

					.main-content {
					margin: 0 auto;
					max-width: 500px;
					padding: 15px;
					}

					ul {
					list-style-type: none;
					padding-left: 0;
					margin: 0;
					}

					.category-item {
					padding: 12px 0;
					border-bottom: 1px solid #eee;
					}

					.category-item:last-child {
					border-bottom: none;
					}

					.category-link {
					color: #007cba;
					text-decoration: none;
					font-weight: 500;
					font-size: 16px;
					}

					.category-link:hover {
					text-decoration: underline;
					}

					.category-descr {
					margin-left: 20px;
					font-size: 13px;
					color: #666;
					margin-top: 5px;
					}

					.book-item {
					margin-bottom: 25px;
					background: white;
					padding: 15px;
					border-radius: 8px;
					box-shadow: 0 2px 4px rgba(0,0,0,0.1);
					position: relative;
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
					margin-right: 15px;
					border-radius: 4px;
					object-fit: cover;
					}

					.book-details {
					flex: 1;
					min-width: 0;
					}

					.book-header {
					display: flex;
					justify-content: space-between;
					align-items: flex-start;
					margin-bottom: 8px;
					flex-wrap: wrap;
					gap: 10px;
					}

					.book-title {
					font-weight: bold;
					color: #333;
					font-size: 16px;
					flex: 1;
					min-width: 0;
					}

					.book-info {
					font-size: 11px;
					color: #666;
					text-align: right;
					white-space: nowrap;
					flex-shrink: 0;
					}

					.book-author {
					font-size: 13px;
					color: #007cba;
					text-decoration: none;
					margin-bottom: 10px;
					display: block;
					}

					.book-author:hover {
					text-decoration: underline;
					}

					.book-descr {
					font-size: 13px;
					line-height: 1.3;
					color: #555;
					margin-bottom: 15px;
					}

					.download-section {
					display: flex;
					flex-direction: column;
					align-items: flex-start;
					width: 140px;
					flex-shrink: 0;
					}

					.download-links {
					display: flex;
					flex-direction: row;
					gap: 5px;
					margin-top: 10px;
					width: 100%;
					}

					.download-link {
					font-size: 11px;
					padding: 4px 8px;
					text-decoration: none;
					border-radius: 3px;
					text-align: center;
					font-weight: bold;
					display: block;
					width: 40%;
					box-sizing: border-box;
					}

					.download-fb2 {
					background-color: #28a745;
					color: white;
					}

					.download-fb2:hover {
					background-color: #218838;
					color: white;
					text-decoration: none;
					}

					.download-epub {
					background-color: #007cba;
					color: white;
					}

					.download-epub:hover {
					background-color: #005a87;
					color: white;
					text-decoration: none;
					}

					h4 {
					color: #333;
					margin-top: 0;
					margin-bottom: 20px;
					font-size: 20px;
					}

					@media (max-width: 768px) {
					body {
					padding-top: 120px;
					}

					.search-section {
					margin-top: 5px;
					padding-top: 5px;
					padding-left: 0;
					padding-right: 0;
					width: 100%;
					box-sizing: border-box;
					}

					.header-content {
					padding: 5px 10px;
					display: flex;
					align-items: center;
					position: relative;
					max-width: none;
					}

					.header-icon {
					width: 48px;
					height: 48px;
					margin-right: 0;
					flex-shrink: 0;
					}

					.header-text {
					flex-grow: 1;
					text-align: center;
					margin: 0;
					position: absolute;
					left: 50%;
					transform: translateX(-50%);
					width: calc(100% - 48px);
					top: 2px;
					}

					.server-title {
					font-size: 18px;
					margin: 0 0 1px 0;
					}

					.library-name {
					margin: 0 10px 0 0;
					font-size: 14px;
					}


					.main-content {
					padding: 10px;
					max-width: none;
					}
					}

					@media (max-width: 480px) {

					.cover {
					width: 120px;
					height: 170px;
					margin-right: 10px;
					}

					.book-header {
					flex-direction: column;
					align-items: flex-start;
					gap: 5px;
					}

					.book-info {
					text-align: left;
					}
					}
				</style>
			</head>
			<body>
				<div class="fixed-header">
					<div class="header-content">
						<img src="/logo.png" alt="TinyOPDS" class="header-icon"/>

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
								<xsl:value-of select="$libName"/>
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

											<div class="download-links">
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