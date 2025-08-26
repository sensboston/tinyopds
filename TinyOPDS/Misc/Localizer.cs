/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple but effective app localization
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
#if !CONSOLE
using System.Windows.Forms;
#endif
using System.Reflection;

namespace TinyOPDS
{
    public static class Localizer
    {
        private static string _lang = "en";
        private static Dictionary<string, string> _translations = new Dictionary<string, string>();
        private static XDocument _xml = null;
#if !CONSOLE
        private static List<ToolStripItem> _menuItems = new List<ToolStripItem>();
#endif

        /// <summary>
        /// Static classes don't have a constructors but we need to initialize translations
        /// </summary>
        /// <param name="xmlFile">Name of xml translations, added to project as an embedded resource</param>
        public static void Init(string xmlFile = "translation.xml")
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetName().Name + "." + xmlFile;

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        _xml = XDocument.Load(stream);
                    }
                    else
                    {
                        Log.WriteLine("Localizer.Init({0}) error: resource not found", xmlFile);
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine("Localizer.Init({0}) exception: {1}", xmlFile, e.Message);
            }
        }

#if !CONSOLE
        /// <summary>
        /// Add menu to translator
        /// </summary>
        /// <param name="menu"></param>
        public static void AddMenu(ContextMenuStrip menu)
        {
            if (menu?.Items != null)
            {
                foreach (ToolStripItem item in menu.Items)
                {
                    if (item != null)
                        _menuItems.Add(item);
                }
            }
        }
#endif

        /// <summary>
        /// Returns supported translations in Dictionary<langCode, languageName>
        /// </summary>
        public static Dictionary<string, string> Languages
        {
            get
            {
                if (_xml == null) return new Dictionary<string, string>();

                try
                {
                    return _xml.Descendants("language")
                              .Where(l => l.Attribute("id") != null && !string.IsNullOrEmpty(l.Value))
                              .ToDictionary(d => d.Attribute("id").Value, d => d.Value);
                }
                catch (Exception e)
                {
                    Log.WriteLine("Localizer.Languages exception: {0}", e.Message);
                    return new Dictionary<string, string>();
                }
            }
        }

        /// <summary>
        /// Current selected language
        /// </summary>
        public static string Language
        {
            get { return _lang; }
            set
            {
                if (_lang != value && _xml != null)
                {
                    _lang = value;
                    LoadTranslations();
                }
            }
        }

#if !CONSOLE
        /// <summary>
        /// Sets current language
        /// </summary>
        /// <param name="form"></param>
        /// <param name="lang"></param>
        public static void SetLanguage(Form form, string lang)
        {
            if (_lang != lang && _xml != null)
            {
                _lang = lang;
                LoadTranslations();
                UpdateControls(form);
            }
        }
#endif

        /// <summary>
        /// Translation helper
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string Text(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            return _translations.ContainsKey(source) ? _translations[source] : source;
        }

        /// <summary>
        /// Loads translations for the current language
        /// </summary>
        private static void LoadTranslations()
        {
            if (_xml == null) return;

            try
            {
                _translations.Clear();

                var properties = _xml.Descendants("property");

                foreach (var property in properties)
                {
                    var englishText = property.Descendants("text")
                                             .FirstOrDefault(t => t.Attribute("lang")?.Value == "en");

                    if (englishText == null || string.IsNullOrEmpty(englishText.Value))
                        continue;

                    string translatedText = englishText.Value; // Default to English

                    if (_lang != "en")
                    {
                        var targetText = property.Descendants("text")
                                                .FirstOrDefault(t => t.Attribute("lang")?.Value == _lang);

                        if (targetText != null && !string.IsNullOrEmpty(targetText.Value))
                        {
                            translatedText = targetText.Value;
                        }
                    }

                    // Use TryAdd to avoid exceptions on duplicate keys
                    string key = englishText.Value;
                    if (!_translations.ContainsKey(key))
                    {
                        _translations.Add(key, translatedText);
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "Duplicate translation key found: '{0}'", key);
                    }
                }

                Log.WriteLine(LogLevel.Info, "Loaded {0} translations for language '{1}'",
                             _translations.Count, _lang);
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "LoadTranslations() exception: {0}", e.Message);

                // Fallback: at least initialize with empty dictionary
                if (_translations == null)
                    _translations = new Dictionary<string, string>();
            }
        }

#if !CONSOLE
        /// <summary>
        /// Updates texts for all form controls (if translation exists)
        /// </summary>
        /// <param name="form">Form to be updated</param>
        private static void UpdateControls(Form form)
        {
            if (form == null || _xml == null) return;

            try
            {
                // Find all controls
                var controls = GetAllControls(form);

                foreach (Control ctrl in new IteratorIsolateCollection(controls))
                {
                    if (ctrl == null || string.IsNullOrEmpty(ctrl.Name))
                        continue;

                    try
                    {
                        var xmlProp = _xml.Descendants("property")
                                         .Where(e => e.Attribute("form")?.Value == form.Name &&
                                                    e.Attribute("ctrl")?.Value == ctrl.Name);

                        if (xmlProp.Any())
                        {
                            var trans = xmlProp.First()
                                              .Descendants("text")
                                              .Where(p => p.Attribute("lang")?.Value == _lang)
                                              .Select(p => p.Value)
                                              .FirstOrDefault();

                            if (!string.IsNullOrEmpty(trans))
                                ctrl.Text = trans;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Warning, "Error updating control {0}: {1}",
                                     ctrl.Name, e.Message);
                    }
                }

                // Update menu items
                foreach (ToolStripItem ctrl in _menuItems)
                {
                    if (ctrl == null || string.IsNullOrEmpty(ctrl.Name))
                        continue;

                    try
                    {
                        var xmlProp = _xml.Descendants("property")
                                         .Where(e => e.Attribute("ctrl")?.Value == ctrl.Name);

                        if (xmlProp.Any())
                        {
                            var trans = xmlProp.First()
                                              .Descendants("text")
                                              .Where(p => p.Attribute("lang")?.Value == _lang)
                                              .Select(p => p.Value)
                                              .FirstOrDefault();

                            if (!string.IsNullOrEmpty(trans))
                                ctrl.Text = trans;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Warning, "Error updating menu item {0}: {1}",
                                     ctrl.Name, e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "UpdateControls() exception: {0}", e.Message);
            }
        }

        /// <summary>
        /// Localization helper: scans form and return xml document with controls names and texts
        /// </summary>
        /// <param name="form">Form to localize</param>
        /// <param name="srcLang">Current form language</param>
        /// <returns>Xml document</returns>
        public static XDocument Setup(Form form, string srcLang = "en")
        {
            XDocument doc = new XDocument();
            doc.Add(new XElement("root", new XElement("languages"), new XElement("properties")));

            if (form != null)
            {
                try
                {
                    var controls = GetAllControls(form);

                    foreach (Control ctrl in controls)
                    {
                        if (ctrl == null) continue;

                        try
                        {
                            foreach (var propInfo in ctrl.GetType().GetProperties())
                            {
                                if (propInfo.Name == "Text")
                                {
                                    var value = propInfo.GetValue(ctrl, null);
                                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                                    {
                                        doc.Root.Element("properties").Add(
                                            new XElement("property",
                                                new XAttribute("form", form.Name ?? ""),
                                                new XAttribute("ctrl", ctrl.Name ?? ""),
                                                new XElement("text",
                                                    new XAttribute("lang", srcLang), value))
                                        );
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.WriteLine(LogLevel.Warning, "Error processing control {0}: {1}",
                                         ctrl.Name ?? "Unknown", e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.WriteLine(LogLevel.Error, "Setup() exception: {0}", e.Message);
                }
            }

            return doc;
        }

        /// <summary>
        /// Enums all controls on the form
        /// </summary>
        /// <param name="control"></param>
        /// <returns></returns>
        private static List<Control> GetAllControls(Control control)
        {
            if (control == null) return new List<Control>();

            try
            {
                var controls = control.Controls.Cast<Control>().Where(c => c != null);
                return controls.SelectMany(ctrl => GetAllControls(ctrl)).Concat(controls).ToList();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Warning, "GetAllControls() exception: {0}", e.Message);
                return new List<Control>();
            }
        }
#endif
    }
}