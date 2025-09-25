/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Intelligent date parser with Russian language support
 * FIXED: Proper validation - NEVER allows Year <= 1
 * FIXED: Always returns valid date, never DateTime.MinValue
 * 
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Linq;

namespace TinyOPDS.Parsers
{
    public static class DateParser
    {
        // Russian month names mapping
        private static readonly Dictionary<string, int> RussianMonths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Full month names
            { "январь", 1 }, { "января", 1 }, { "февраль", 2 }, { "февраля", 2 }, { "март", 3 }, { "марта", 3 },
            { "апрель", 4 }, { "апреля", 4 }, { "май", 5 }, { "мая", 5 }, { "июнь", 6 }, { "июня", 6 },
            { "июль", 7 }, { "июля", 7 }, { "август", 8 }, { "августа", 8 }, { "сентябрь", 9 }, { "сентября", 9 },
            { "октябрь", 10 }, { "октября", 10 }, { "ноябрь", 11 }, { "ноября", 11 }, { "декабрь", 12 }, { "декабря", 12 },
            
            // Abbreviated month names
            { "янв", 1 }, { "фев", 2 }, { "мар", 3 }, { "апр", 4 }, { "июн", 6 }, { "июл", 7 }, { "авг", 8 },
            { "сен", 9 }, { "сент", 9 }, { "окт", 10 }, { "ноя", 11 }, { "нояб", 11 }, { "дек", 12 }
        };

        /// <summary>
        /// Parse date from various formats including Russian
        /// ENSURES that a valid date is always returned - NEVER DateTime.MinValue or Year <= 1
        /// </summary>
        /// <param name="dateString">Date string to parse</param>
        /// <param name="fallbackDate">Fallback date to use if parsing fails</param>
        /// <returns>Parsed date or fallback - NEVER DateTime.MinValue or Year <= 1</returns>
        public static DateTime ParseDate(string dateString, DateTime? fallbackDate = null)
        {
            // CRITICAL: Ensure we ALWAYS have a valid fallback
            DateTime validFallback = GetValidFallback(fallbackDate);

            if (string.IsNullOrWhiteSpace(dateString))
                return validFallback;

            dateString = dateString.Trim();

            // Try standard parsing first (for ISO dates, etc.)
            if (TryStandardParse(dateString, out DateTime standardDate))
                return ValidateDate(standardDate, validFallback);

            // Try parsing Russian date format "Июль 2002 г."
            if (TryParseRussianDate(dateString, out DateTime russianDate))
                return ValidateDate(russianDate, validFallback);

            // Try parsing just a year
            if (TryParseYearOnly(dateString, out DateTime yearDate))
                return ValidateDate(yearDate, validFallback);

            // Try parsing various other formats
            if (TryParseFlexibleFormat(dateString, out DateTime flexDate))
                return ValidateDate(flexDate, validFallback);

            // Return fallback if all parsing attempts failed
            Log.WriteLine(LogLevel.Warning, "Could not parse date: '{0}', using fallback", dateString);
            return validFallback;
        }

        /// <summary>
        /// Parse date from FB2 XML element
        /// </summary>
        /// <param name="dateElement">XElement containing date information</param>
        /// <param name="fileName">File name for fallback date</param>
        /// <returns>Parsed date - NEVER returns invalid date</returns>
        public static DateTime ParseFB2Date(XElement dateElement, string fileName)
        {
            DateTime fileDate = GetFileDate(fileName);

            if (dateElement == null)
                return fileDate;

            try
            {
                // Try value attribute first (standard FB2 format)
                var valueAttr = dateElement.Attribute("value")?.Value;
                if (!string.IsNullOrEmpty(valueAttr))
                {
                    // FB2 date value is usually in ISO format
                    if (DateTime.TryParse(valueAttr, out DateTime dateFromValue))
                    {
                        return ValidateDate(dateFromValue, fileDate);
                    }
                }

                // Try element text content
                string dateText = dateElement.Value;
                if (!string.IsNullOrEmpty(dateText))
                {
                    return ParseDate(dateText, fileDate);
                }

                // No date found, use file date
                return fileDate;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Could not parse date from FB2 element for {0}: {1}",
                    fileName, ex.Message);
                return fileDate;
            }
        }

        /// <summary>
        /// Get valid fallback date - NEVER returns invalid date
        /// </summary>
        private static DateTime GetValidFallback(DateTime? fallbackDate)
        {
            // Check if provided fallback is valid
            if (fallbackDate.HasValue)
            {
                var fb = fallbackDate.Value;
                // Check for all invalid conditions
                if (fb != DateTime.MinValue &&
                    fb != default(DateTime) &&
                    fb.Year > 1800 &&
                    fb.Year <= DateTime.Now.Year + 10)
                {
                    return fb;
                }
            }

            // If no valid fallback provided, use current date
            return DateTime.Now;
        }

        /// <summary>
        /// Try standard DateTime.Parse with multiple cultures
        /// </summary>
        private static bool TryStandardParse(string dateString, out DateTime result)
        {
            // Try invariant culture
            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out result))
                return true;

            // Try Russian culture
            try
            {
                var ruCulture = new CultureInfo("ru-RU");
                if (DateTime.TryParse(dateString, ruCulture,
                    DateTimeStyles.AllowWhiteSpaces, out result))
                    return true;
            }
            catch { }

            // Try US culture
            try
            {
                var usCulture = new CultureInfo("en-US");
                if (DateTime.TryParse(dateString, usCulture,
                    DateTimeStyles.AllowWhiteSpaces, out result))
                    return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Parse Russian date format like "Июль 2002 г." or "15 июля 2002"
        /// </summary>
        private static bool TryParseRussianDate(string dateString, out DateTime result)
        {
            result = default(DateTime);

            // Remove common suffixes
            dateString = dateString.Replace("г.", "").Replace("год", "").Replace("года", "").Trim();

            // Pattern 1: "Июль 2002" (Month Year)
            var monthYearMatch = Regex.Match(dateString, @"^(\S+)\s+(\d{4})$", RegexOptions.IgnoreCase);
            if (monthYearMatch.Success)
            {
                string monthStr = monthYearMatch.Groups[1].Value.ToLower();
                string yearStr = monthYearMatch.Groups[2].Value;

                if (RussianMonths.TryGetValue(monthStr, out int month) &&
                    int.TryParse(yearStr, out int year))
                {
                    if (year >= 1 && year <= 9999)
                    {
                        try
                        {
                            result = new DateTime(year, month, 1);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            // Pattern 2: "15 июля 2002" (Day Month Year)
            var dayMonthYearMatch = Regex.Match(dateString, @"^(\d{1,2})\s+(\S+)\s+(\d{4})$", RegexOptions.IgnoreCase);
            if (dayMonthYearMatch.Success)
            {
                string dayStr = dayMonthYearMatch.Groups[1].Value;
                string monthStr = dayMonthYearMatch.Groups[2].Value.ToLower();
                string yearStr = dayMonthYearMatch.Groups[3].Value;

                if (int.TryParse(dayStr, out int day) &&
                    RussianMonths.TryGetValue(monthStr, out int month) &&
                    int.TryParse(yearStr, out int year))
                {
                    if (year >= 1 && year <= 9999 && day >= 1 && day <= 31)
                    {
                        try
                        {
                            result = new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
                            return true;
                        }
                        catch { }
                    }
                }
            }

            // Pattern 3: "2002, июль" (Year, Month)
            var yearMonthMatch = Regex.Match(dateString, @"^(\d{4})[,\s]+(\S+)$", RegexOptions.IgnoreCase);
            if (yearMonthMatch.Success)
            {
                string yearStr = yearMonthMatch.Groups[1].Value;
                string monthStr = yearMonthMatch.Groups[2].Value.ToLower();

                if (int.TryParse(yearStr, out int year) &&
                    RussianMonths.TryGetValue(monthStr, out int month))
                {
                    if (year >= 1 && year <= 9999)
                    {
                        try
                        {
                            result = new DateTime(year, month, 1);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to parse just a year
        /// </summary>
        private static bool TryParseYearOnly(string dateString, out DateTime result)
        {
            result = default(DateTime);

            // Extract 4-digit year
            var yearMatch = Regex.Match(dateString, @"\b(\d{4})\b");
            if (yearMatch.Success)
            {
                if (int.TryParse(yearMatch.Groups[1].Value, out int year))
                {
                    if (year >= 1 && year <= 9999)
                    {
                        try
                        {
                            result = new DateTime(year, 1, 1);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            // Also try to parse single digit or 2-3 digit years (common in broken files)
            var shortYearMatch = Regex.Match(dateString, @"^\d{1,3}$");
            if (shortYearMatch.Success)
            {
                if (int.TryParse(shortYearMatch.Value, out int year))
                {
                    // These are almost certainly invalid years
                    // Return false to trigger fallback
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Try various flexible date formats
        /// </summary>
        private static bool TryParseFlexibleFormat(string dateString, out DateTime result)
        {
            result = default(DateTime);

            // Common date patterns
            string[] patterns = new[]
            {
                "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy/MM/dd",
                "yyyyMMdd", "dd MMM yyyy", "MMM dd, yyyy", "MMMM dd, yyyy", "dd MMMM yyyy",
                "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss"
            };

            foreach (var pattern in patterns)
            {
                if (DateTime.TryParseExact(dateString, pattern, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out result))
                    return true;

                // Try with Russian culture too
                try
                {
                    var ruCulture = new CultureInfo("ru-RU");
                    if (DateTime.TryParseExact(dateString, pattern, ruCulture,
                        DateTimeStyles.AllowWhiteSpaces, out result))
                        return true;
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Validate date is within reasonable bounds
        /// CRITICAL FIX: Now properly rejects Year <= 1
        /// </summary>
        /// <param name="date">Date to validate</param>
        /// <param name="fallback">Fallback date if validation fails</param>
        /// <returns>Valid date - either the input date if valid, or fallback</returns>
        private static DateTime ValidateDate(DateTime date, DateTime fallback)
        {
            // CRITICAL FIX: Check for Year <= 1, not just < 1
            if (date.Year <= 1 || date.Year > 9999)
            {
                Log.WriteLine(LogLevel.Warning, "Date year invalid: {0}, using fallback {1}", date, fallback);
                return fallback;
            }

            // Check for DateTime.MinValue and default
            if (date == DateTime.MinValue || date == default(DateTime))
            {
                Log.WriteLine(LogLevel.Warning, "Date is MinValue/default, using fallback");
                return fallback;
            }

            // Check for very old dates (before 1800)
            // For fiction books, dates before 1800 are suspicious
            if (date.Year < 1800)
            {
                Log.WriteLine(LogLevel.Warning, "Suspicious old date detected: {0}, using fallback", date);
                return fallback;
            }

            // Check for future dates (more than 10 years ahead)
            if (date > DateTime.Now.AddYears(10))
            {
                Log.WriteLine(LogLevel.Warning, "Future date detected: {0}, using fallback", date);
                return fallback;
            }

            return date;
        }

        /// <summary>
        /// Get file date from ZIP entry path as fallback
        /// Always returns a valid date
        /// </summary>
        public static DateTime GetFileDate(string filePath)
        {
            try
            {
                // If it's a file in a ZIP (contains @), try to get ZIP file date
                if (filePath.Contains("@"))
                {
                    string zipPath = filePath.Substring(0, filePath.IndexOf('@'));
                    if (File.Exists(zipPath))
                    {
                        DateTime zipDate = File.GetLastWriteTime(zipPath);
                        // Validate that the file date is reasonable
                        if (zipDate.Year > 1980 && zipDate <= DateTime.Now)
                            return zipDate;
                    }
                }

                // Try to get regular file date
                if (File.Exists(filePath))
                {
                    DateTime fileDate = File.GetLastWriteTime(filePath);
                    // Validate that the file date is reasonable
                    if (fileDate.Year > 1980 && fileDate <= DateTime.Now)
                        return fileDate;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Could not get file date for {0}: {1}", filePath, ex.Message);
            }

            // Last resort - return current date
            return DateTime.Now;
        }
    }
}