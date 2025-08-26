<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
	<xsl:param name="serverVersion" select="'TinyOPDS server'"/>
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
					margin: 0 auto;
					max-width: 700px;
					font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
					padding: 20px;
					background-color: #fafafa;
					line-height: 1.4;
					}

					@media (max-width: 768px) {
					body {
					padding: 10px;
					max-width: 100%;
					}
					}

					ul {
					list-style-type: none;
					padding-left: 0;
					}

					.header {
					text-align: center;
					margin-bottom: 30px;
					padding-bottom: 20px;
					border-bottom: 2px solid #e0e0e0;
					}

					.server-title {
					color: #333;
					margin-bottom: 10px;
					font-size: 28px;
					}

					.library-name {
					color: #666;
					font-size: 18px;
					margin-bottom: 20px;
					}

					.search-box {
					margin-bottom: 20px;
					text-align: center;
					}

					.search-form {
					display: inline-block;
					}

					.search-input {
					width: 280px;
					max-width: calc(100vw - 120px);
					padding: 10px;
					font-size: 14px;
					border: 1px solid #ddd;
					border-radius: 4px;
					box-sizing: border-box;
					}

					.search-button {
					padding: 10px 20px;
					font-size: 14px;
					background-color: #007cba;
					color: white;
					border: none;
					border-radius: 4px;
					cursor: pointer;
					margin-left: 8px;
					}

					.search-button:hover {
					background-color: #005a87;
					}

					@media (max-width: 480px) {
					.search-input {
					width: calc(100vw - 40px);
					margin-bottom: 10px;
					}
					.search-button {
					margin-left: 0;
					width: 100px;
					}
					}

					.right {
					float: right;
					margin-right: 20px;
					}

					.category-descr {
					margin-left: 20px;
					font-size: 12px;
					color: #666;
					}

					.book-descr {
					margin-top: 10px;
					font-size: 13px;
					line-height: 1.3;
					}

					.download-links {
					margin-top: 15px;
					}

					.download-link {
					font-size: 12px;
					margin-top: 5px;
					margin-right: 10px;
					padding: 5px 12px;
					text-decoration: none;
					border-radius: 3px;
					display: inline-block;
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

					.cover {
					width: 120px;
					max-width: 120px;
					float: left;
					margin-right: 20px;
					border-radius: 4px;
					}

					@media (max-width: 480px) {
					.cover {
					width: 80px;
					margin-right: 15px;
					}
					}

					.book-item {
					margin-bottom: 30px;
					overflow: hidden;
					background: white;
					padding: 15px;
					border-radius: 8px;
					box-shadow: 0 2px 4px rgba(0,0,0,0.1);
					}

					.book-content {
					overflow: hidden;
					}

					.book-title {
					font-weight: bold;
					color: #333;
					margin-bottom: 5px;
					font-size: 16px;
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

					.book-info {
					font-size: 12px;
					color: #666;
					margin-top: 10px;
					}

					.category-item {
					padding: 10px 0;
					border-bottom: 1px solid #eee;
					}

					.category-item:last-child {
					border-bottom: none;
					}

					.category-link {
					color: #007cba;
					text-decoration: none;
					font-weight: 500;
					}

					.category-link:hover {
					text-decoration: underline;
					}

					h4 {
					color: #333;
					margin-top: 0;
					margin-bottom: 20px;
					}
				</style>
			</head>
			<body>
				<div class="header">
					<h1 class="server-title">
						<xsl:choose>
							<xsl:when test="contains($serverVersion, 'version ')">
								<xsl:value-of select="concat(substring-before($serverVersion, 'version '), 'v. ', substring-after($serverVersion, 'version '))"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:value-of select="$serverVersion"/>
							</xsl:otherwise>
						</xsl:choose>
					</h1>
					<xsl:if test="$id = 'tag:root'">
						<div class="library-name">
							<xsl:value-of select="$title"/>
						</div>
					</xsl:if>
				</div>

				<div class="search-box">
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
								<span class="category-descr">
									<xsl:value-of select="x:content"/>
								</span>
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
								<span class="category-descr">
									<xsl:value-of select="x:content"/>
								</span>
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
									<img class="cover">
										<xsl:attribute name="src">
											<xsl:value-of select="x:link[attribute::type='image/jpeg']/@href"/>
										</xsl:attribute>
									</img>

									<div>
										<div class="book-title">
											<xsl:value-of select="x:title" />
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

										<div class="book-info">
											<strong>
												<xsl:value-of select="$formatText"/>
											</strong>
											<xsl:value-of select="x:format"/>
											<br/>
											<strong>
												<xsl:value-of select="$sizeText"/>
											</strong>
											<xsl:value-of select="x:size"/>
										</div>

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
								</div>
							</li>
						</xsl:for-each>
					</ul>
				</xsl:if>
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