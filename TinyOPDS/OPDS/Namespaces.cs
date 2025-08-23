/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * OPDS xml namespaces
 *
 */

using System.Xml.Linq;

namespace TinyOPDS.OPDS
{
    internal class Namespaces
    {
        internal static XNamespace xmlns = XNamespace.Get("http://www.w3.org/2005/Atom");
        internal static XNamespace dc = XNamespace.Get("http://purl.org/dc/terms/");
        internal static XNamespace os = XNamespace.Get("http://a9.com/-/spec/opensearch/1.1/");
        internal static XNamespace opds = XNamespace.Get("http://opds-spec.org/2010/catalog");
    }
}
