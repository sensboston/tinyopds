<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
	<xsl:param name="serverVersion" select="'TinyOPDS server'"/>

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

					.download-link {
					font-size: 12px;
					margin-top: 10px;
					padding: 5px 10px;
					background-color: #28a745;
					color: white;
					text-decoration: none;
					border-radius: 3px;
					display: inline-block;
					}

					.download-link:hover {
					background-color: #218838;
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

				<!-- Search box for all pages -->
				<div class="search-box">
					<form method="get" action="/search" class="search-form">
						<input type="text" name="searchTerm" placeholder="Поиск авторов или книг..." class="search-input" />
						<input type="submit" value="Поиск" class="search-button" />
					</form>
				</div>

				<!--home page-->
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

				<!--by authors, series, genres etc.-->
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

				<!--book list-->
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
											<strong>Формат: </strong>
											<xsl:value-of select="x:format"/>
											<br/>
											<strong>Размер: </strong>
											<xsl:value-of select="x:size"/>
										</div>

										<a class="download-link">
											<xsl:attribute name="href">
												<xsl:choose>
													<xsl:when test="x:link[attribute::type='application/epub+zip']">
														<xsl:value-of select="x:link[attribute::type='application/epub+zip']/@href" />
													</xsl:when>
													<xsl:otherwise>
														<xsl:value-of select="x:link[attribute::type='application/fb2+zip']/@href" />
													</xsl:otherwise>
												</xsl:choose>
											</xsl:attribute>
											Скачать
										</a>
									</div>
								</div>
							</li>
						</xsl:for-each>
					</ul>
				</xsl:if>
			</body>
		</html>
	</xsl:template>
</xsl:stylesheet>