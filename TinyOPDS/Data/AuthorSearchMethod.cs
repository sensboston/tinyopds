/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Enum for tracking author search method
 *
 */

/// <summary>
/// Indicates which search method successfully found results
/// </summary>
public enum AuthorSearchMethod
{
    NotFound = 0,
    ExactMatch = 1,      // Exact match of full name
    PartialMatch = 2,    // Partial match on LastName or FirstName
    Transliteration = 3, // Found via transliteration
    Soundex = 4          // Found via Soundex (least precise)
}
