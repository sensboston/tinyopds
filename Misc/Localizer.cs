/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Very simple but very effective application localization
 * 
 ************************************************************/

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
        /// <param name="xmlFile">Name of xml translations, added to project as an embedded resource </param>
        public static void Init(string xmlFile = "translation.xml")
        {
            try 
            { 
                _xml = XDocument.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDS."+xmlFile));
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
            foreach (ToolStripItem item in menu.Items) _menuItems.Add(item);
        }
#endif
        /// <summary>
        /// Returns supported translations in Dictionary<langCode, languageName>
        /// </summary>
        public static Dictionary<string,string> Languages
        {
            get
            {
                return _xml != null ? _xml.Descendants("language").ToDictionary(d => d.Attribute("id").Value, d => d.Value) : null;
            }
        }

        /// <summary>
        /// Current selected language
        /// </summary>
        public static string Language { get { return _lang; } }

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
                try
                {
                    // Update localized string dictionary
                    List<string> t = _xml.Descendants("property") // .Where(a => !a.HasAttributes)
                                         .Descendants("text").Where(b => b.Attribute("lang").Value == "en" || b.Attribute("lang").Value == _lang)
                                         .Select(c => c.Value).ToList();
                    _translations.Clear();

                    if (lang.Equals("en"))
                    {
                        for (int i = 0; i < t.Count; i++)
                            if (!string.IsNullOrEmpty(t[i]))
                                _translations.Add(t[i], t[i]);
                    }
                    else
                    {
                        for (int i = 0; i < t.Count / 2; i++)
                            if (!string.IsNullOrEmpty(t[i * 2])) 
                                _translations.Add(t[i * 2], t[i * 2 + 1]);
                    }

                    // Update form controls
                    UpdateControls(form);
                }
                catch (Exception e)
                {
                    Log.WriteLine(".SetLanguage({0},{1}) exception: {2}", form, lang, e.Message);
                }
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
            return (_translations.ContainsKey(source)) ? _translations[source] : source;
        }

#if !CONSOLE
        /// <summary>
        /// Updates texts for all form controls (if translation exists)
        /// </summary>
        /// <param name="form">Form to be updated</param>
        private static void UpdateControls(Form form)
        {
            // Find all controls
            var controls = GetAllControls(form);

            foreach (Control ctrl in new IteratorIsolateCollection(controls))
            {
                var xmlProp = _xml.Descendants("property").Where(e => e.Attribute("form") != null && e.Attribute("form").Value.Equals(form.Name) && 
                                                                      e.Attribute("ctrl") != null && e.Attribute("ctrl").Value == ctrl.Name);
                if (xmlProp != null && xmlProp.Count() > 0)
                {
                    var trans = xmlProp.FirstOrDefault().Descendants("text").Where(p => p.Attribute("lang").Value == _lang).Select(p => p.Value);
                    if (trans != null && trans.Count() > 0) ctrl.Text = trans.First() as string;
                }
            }

            foreach (ToolStripItem ctrl in _menuItems)
            {
                var xmlProp = _xml.Descendants("property").Where(e => e.Attribute("ctrl") != null && e.Attribute("ctrl").Value == ctrl.Name);
                if (xmlProp != null && xmlProp.Count() > 0)
                {
                    var trans = xmlProp.First().Descendants("text").Where(p => p.Attribute("lang").Value == _lang).Select(p => p.Value);
                    if (trans != null && trans.Count() > 0) ctrl.Text = trans.First() as string;
                }

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

                var controls = GetAllControls(form);

                foreach (Control ctrl in controls)
                {
                    foreach (var propInfo in ctrl.GetType().GetProperties())
                    {
                        if (propInfo.Name == "Text")
                        {
                            try
                            {
                                var value = propInfo.GetValue(ctrl, null);
                                doc.Root.Element("properties").Add(
                                    new XElement("property",
                                        new XAttribute("form", form.Name),
                                        new XAttribute("ctrl", ctrl.Name),
                                        new XElement("text",
                                            new XAttribute("lang", srcLang), value))
                                    );
                            }
                            catch
                            { }
                        }
                    }
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
            var controls = control.Controls.Cast<Control>();
            return (controls.SelectMany(ctrl => GetAllControls(ctrl)).Concat(controls)).ToList();
        }
#endif
    }
}
