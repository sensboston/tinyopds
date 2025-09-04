/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Intelligent date parser with Russian language support
 * 
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using FB2Library.Elements;

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
        /// </summary>
        /// <param name="dateString">Date string to parse</param>
        /// <param name="fallbackDate">Fallback date to use if parsing fails</param>
        /// <returns>Parsed date or fallback</returns>
        public static DateTime ParseDate(string dateString, DateTime? fallbackDate = null)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return fallbackDate ?? DateTime.Now;

            dateString = dateString.Trim();

            // Try standard parsing first (for ISO dates, etc.)
            if (TryStandardParse(dateString, out DateTime standardDate))
                return ValidateDate(standardDate, fallbackDate);

            // Try parsing Russian date format "Июль 2002 г."
            if (TryParseRussianDate(dateString, out DateTime russianDate))
                return ValidateDate(russianDate, fallbackDate);

            // Try parsing just a year
            if (TryParseYearOnly(dateString, out DateTime yearDate))
                return ValidateDate(yearDate, fallbackDate);

            // Try parsing various other formats
            if (TryParseFlexibleFormat(dateString, out DateTime flexDate))
                return ValidateDate(flexDate, fallbackDate);

            // Return fallback if all parsing attempts failed
            Log.WriteLine(LogLevel.Warning, "Could not parse date: '{0}', using fallback", dateString);
            return fallbackDate ?? DateTime.Now;
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
            result = default;

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
            result = default;

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

            return false;
        }

        /// <summary>
        /// Try various flexible date formats
        /// </summary>
        private static bool TryParseFlexibleFormat(string dateString, out DateTime result)
        {
            result = default;

            // Common date patterns
            string[] patterns = new[]
            {
                "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy/MM/dd",
                "yyyyMMdd", "dd MMM yyyy","MMM dd, yyyy", "MMMM dd, yyyy", "dd MMMM yyyy"
            };

            foreach (var pattern in patterns)
            {
                if (DateTime.TryParseExact(dateString, pattern, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out result))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validate date is within reasonable bounds
        /// </summary>
        private static DateTime ValidateDate(DateTime date, DateTime? fallback)
        {
            // Check for reasonable date bounds
            if (date.Year < 1 || date.Year > 9999)
            {
                Log.WriteLine(LogLevel.Warning, "Date year out of bounds: {0}, using fallback", date);
                return fallback ?? DateTime.Now;
            }

            // Check for suspiciously old dates (before 1800)
            if (date.Year < 1800)
            {
                Log.WriteLine(LogLevel.Info, "Very old date detected: {0}, keeping as historical", date);
                // Keep historical dates for old books
            }

            // Check for future dates (more than 10 years ahead)
            if (date > DateTime.Now.AddYears(10))
            {
                Log.WriteLine(LogLevel.Warning, "Future date detected: {0}, using current date", date);
                return DateTime.Now;
            }

            return date;
        }

        /// <summary>
        /// Get file date from ZIP entry path as fallback
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
                        return File.GetLastWriteTime(zipPath);
                    }
                }

                // Try to get regular file date
                if (File.Exists(filePath))
                {
                    return File.GetLastWriteTime(filePath);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Could not get file date for {0}: {1}", filePath, ex.Message);
            }

            return DateTime.Now;
        }

        /// <summary>
        /// Safe wrapper for FB2Library date parsing with fallback
        /// </summary>
        public static DateTime ParseFB2Date(DateItem dateItem, string fileName)
        {
            if (dateItem == null)
                return GetFileDate(fileName);

            try
            {
                // Try FB2Library's built-in parser first
                DateTime dateValue = dateItem.DateValue;

                // Check if FB2Library returned MinValue (failed to parse)
                if (dateValue == DateTime.MinValue || dateValue.Year == 1)
                {
                    // FB2Library couldn't parse, use our parser
                    string dateText = dateItem.Text ?? "";

                    if (!string.IsNullOrEmpty(dateText))
                    {
                        DateTime fallback = GetFileDate(fileName);
                        DateTime parsedDate = ParseDate(dateText, fallback);

                        Log.WriteLine(LogLevel.Info, "Parsed Russian date '{0}' -> {1:yyyy-MM-dd}",
                            dateText, parsedDate);

                        return parsedDate;
                    }

                    // No text to parse, use file date
                    return GetFileDate(fileName);
                }

                // FB2Library parsed successfully, validate the date
                return ValidateDate(dateValue, GetFileDate(fileName));
            }
            catch (Exception ex)
            {
                // Any exception - try to parse text or use file date
                string dateText = dateItem?.Text ?? "";

                if (!string.IsNullOrEmpty(dateText))
                {
                    DateTime fallback = GetFileDate(fileName);
                    return ParseDate(dateText, fallback);
                }

                Log.WriteLine(LogLevel.Warning, "Could not parse date from FB2, using file date for {0}: {1}",
                    fileName, ex.Message);
                return GetFileDate(fileName);
            }
        }
    }
}