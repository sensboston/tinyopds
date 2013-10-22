<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">
  <xsl:template match="/">
    <html>
      <head>
        <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
        <link rel="stylesheet" type="text/css" href="Stylesheet1.css" />
        <style>
          ul
          {
          list-style-type: none;
          }
          .right
          {
          float:right;
          margin-right: 20px
          }

        </style>
      </head>
      <body style="margin: 0 auto;max-width: 700px; font-family: Century Gothic; padding-top: 40px;">
        <h1>TinyOPDS</h1>
        <xsl:variable name="title">
          <xsl:value-of select="x:feed/x:title"/>
        </xsl:variable>
        <xsl:variable name="icon">
          <xsl:value-of select="x:feed/x:icon"/>
        </xsl:variable>
        <xsl:if test="starts-with($title,'Моя домашняя библиотека')">
          <h4>Моя домашняя библиотека</h4>
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
        <xsl:if test="starts-with($title,'Books by authors')">
          <h4>Books by authors</h4>
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
        <xsl:if test="starts-with($title,'Books by genres')">
          <h4>Books by genres</h4>
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

        <xsl:if test="starts-with($title,'Book series')">
          <h4>Book series</h4>
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
        <xsl:if test="starts-with($icon,'/icons/books.ico')">
          <h4>
            <xsl:value-of select="x:feed/x:title" />
          </h4>
          <ul style="list-style-type: none;">
            <xsl:for-each select="x:feed/x:entry">
              <li>
                <div >
                  <img style="width: 192px;float:left;margin-right:20px;">
                    <xsl:attribute name="src" >
                      <xsl:value-of select="x:link[attribute::type='image/jpeg']/@href"  />
                    </xsl:attribute>
                  </img>

                  <div style="clear:right;">

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
                            <xsl:value-of select="x:author/x:uri" />
                          </xsl:attribute>
                          <xsl:value-of select="x:author/x:name"/>
                        </a>
                      </div>

                    </div>

                    <div  style="margin-top:10px; font-size:8pt;">
                      <xsl:value-of select="x:content"/>
                    </div>

                    <a style="font-size: 8pt;margin-top:10px" class="right">
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
        <!--<table border="1" width="100%">
          <tr>
            <th></th>
            <th>Catalog/Item</th>
            <th>Description</th>
          </tr>
          <xsl:for-each select="x:feed/x:entry">
            <tr>
              <td>
                <xsl:value-of select="x:id" />
              </td>
              <td height="120%">
                <a>
                  <xsl:attribute name="href">
                    <xsl:value-of select="x:link/@href" />
                  </xsl:attribute>
                  <xsl:value-of select="x:title" />
                </a>
              </td>
              <td>
                <xsl:value-of select="x:content"/>
              </td>
            </tr>
          </xsl:for-each>
        </table>-->
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
