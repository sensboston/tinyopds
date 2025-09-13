using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Reflection;

namespace Bluegrams.Application
{
    /// <summary>
    /// A shared base class for portable settings providers.
    /// </summary>
    public abstract class PortableSettingsProviderBase : SettingsProvider, IApplicationSettingsProvider
    {
        /// <summary>
        /// Specifies if all settings should be roaming.
        /// </summary>
        public static bool AllRoaming { get; set; } = false;

        /// <summary>
        /// Specifies the directory of the settings file.
        /// </summary>
        public static string SettingsDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

        public override string ApplicationName { get { return Assembly.GetExecutingAssembly().GetName().Name; } set { } }

        protected static void ApplyProvider(PortableSettingsProviderBase provider, params ApplicationSettingsBase[] settingsList)
        {
            foreach (var settings in settingsList)
            {
                settings.Providers.Clear();
                settings.Providers.Add(provider);
                foreach (SettingsProperty prop in settings.Properties)
                    prop.Provider = provider;
                settings.Reload();
            }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (String.IsNullOrEmpty(name)) name = Name;
            base.Initialize(name, config);
        }

        public virtual SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            throw new NotImplementedException();
        }

        public abstract void Reset(SettingsContext context);

        public virtual void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        { /* don't do anything here*/ }

        /// <summary>
        /// Iterates through a property's attributes to determine whether it is user-scoped or application-scoped.
        /// </summary>
        protected bool IsUserScoped(SettingsProperty prop)
        {
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a.GetType() == typeof(UserScopedSettingAttribute))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Iterates through a property's attributes to determine whether it is set to roam.
        /// </summary>
        protected bool IsRoaming(SettingsProperty prop)
        {
            if (AllRoaming)
                return true;
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a.GetType() == typeof(SettingsManageabilityAttribute))
                    return true;
            }
            return false;
        }
    }
}
