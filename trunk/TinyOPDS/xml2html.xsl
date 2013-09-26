<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:x="http://www.w3.org/2005/Atom">

<xsl:template match="/">
<html>
  <head>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
  </head>
  <body>
  <h2>TinyOPDS</h2>
  <table border="1" width="100%">
    <tr bgcolor="#9acd32">
      <th>Catalog/Item</th>
      <th>Description</th>
    </tr>
    <xsl:for-each select="x:feed/x:entry">
    <tr>
      <td height="120%">
         <a>
            <xsl:attribute name="href"><xsl:value-of select="x:link/@href" /></xsl:attribute>
            <xsl:value-of select="x:title" />
         </a>
      </td>
      <td><xsl:value-of select="x:content"/></td>
    </tr>
    </xsl:for-each>
  </table>
  </body>
</html>
</xsl:template>

</xsl:stylesheet> 