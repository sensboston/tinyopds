<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
  <xsl:template match="/">
    <html>
      <head>
        <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
        <!--<link rel="stylesheet" type="text/css" href="Stylesheet1.css" />-->
        <style>
			body{
			margin: 0 auto;
			max-width: 700px;
			font-family: Century Gothic;
			padding-top: 40px;
			}
			ul
			{
			list-style-type: none;
			}

			.right
			{
			float:right;
			margin-right: 20px
			}

			.category-descr
			{
			margin-left:20px;
			font-size:8pt
			}

			.book-descr
			{
			margin-top:10px; font-size:8pt;
			}

			.download-link
			{
			font-size: 8pt;margin-top:10px
			}

			.cover
			{
			width: 192px;float:left;margin-right:20px;
			}
		</style>
      </head>
      <body>
        <h1>TinyOPDS</h1>
        <xsl:variable name="title">
          <xsl:value-of select="x:feed/x:title"/>
        </xsl:variable>
        <xsl:variable name="id">
          <xsl:value-of select="x:feed/x:id"/>
        </xsl:variable>
        <xsl:variable name="icon">
          <xsl:value-of select="x:feed/x:icon"/>
        </xsl:variable>

        <!--home-->
        <xsl:if test="$id = 'tag:root'">
          <h4>
            <xsl:copy-of select="$title" />
          </h4>
          <ul>
            <xsl:for-each select="x:feed/x:entry">
              <li>
                <a>
                  <xsl:attribute name="href">
                    <xsl:value-of select="x:link/@href" />
                  </xsl:attribute>
                  <xsl:value-of select="x:title" />
                </a>
                <span  style="margin-left:20px;font-size:8pt">
                  <xsl:value-of select="x:content"/>
                </span>
              </li>
            </xsl:for-each>
          </ul>
        </xsl:if>

        <!--by authors, series, genres etc.-->
        <xsl:if test="($icon='/genres.ico') or ($icon='/series.ico')  or ($icon='/authors.ico') ">
          <h4>
            <xsl:copy-of select="$title" />
          </h4>
          <ul>
            <xsl:for-each select="x:feed/x:entry">
              <li>
                <a>
                  <xsl:attribute name="href">
                    <xsl:value-of select="x:link/@href" />
                  </xsl:attribute>
                  <xsl:value-of select="x:title" />
                </a>
                <span  class="category-descr">
                  <xsl:value-of select="x:content"/>
                </span>
              </li>
            </xsl:for-each>
          </ul>
        </xsl:if>

        <!--book list-->
        <xsl:if test="$icon = '/icons/books.ico'">
          <h4>
            <xsl:value-of select="x:feed/x:title" />
          </h4>
          <ul style="list-style-type: none;">
            <xsl:for-each select="x:feed/x:entry">
              <li>
                <div>
                  <img class="cover">
                    <xsl:attribute name="src" >
                      <xsl:value-of select="x:link[attribute::type='image/jpeg']/@href"/>
                    </xsl:attribute>
                  </img>

                  <div style="clear:right">
                    <div>
                      <div>
                        <b>
                          <xsl:value-of select="x:title" />
                        </b>
                        <span class="right" style="font-size:7pt">
                          <xsl:value-of select="x:updated" />
                        </span>
                      </div>
                      <div>
                        <a style="font-size: 9pt">
                          <xsl:attribute name="href">
                            <xsl:value-of select="concat('/lib', x:author/x:uri)" />
                          </xsl:attribute>
                          <xsl:value-of select="x:author/x:name"/>
                        </a>
                      </div>

                    </div>

                    <div  class="book-descr">
                      <xsl:value-of select="x:content"/>
                    </div>
					  
					<p>
						<div>
							<a style="font-size: 9pt">
								<b>Format: </b>
								<xsl:value-of select="x:format"/>
								<br/>
								<b>Size: </b>
								<xsl:value-of select="x:size"/>
							</a>
						</div>
					</p>

                    <a class="right download-link">
                      <xsl:attribute name="href">
                        <xsl:value-of select="x:link[attribute::type='application/fb2+zip']/@href" />
                      </xsl:attribute>
                      Download
                    </a>
                  </div>
                  <div style="clear:left">
                  </div>
                  <br/>
                  <br/>
                </div>
              </li>
            </xsl:for-each>
          </ul>
        </xsl:if>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
