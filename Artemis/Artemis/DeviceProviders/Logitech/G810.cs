﻿using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Artemis.DAL;
using Artemis.DeviceProviders.Logitech.Utilities;
using Artemis.Properties;
using Artemis.Settings;

namespace Artemis.DeviceProviders.Logitech
{
    internal class G810 : LogitechKeyboard
    {
        private GeneralSettings _generalSettings;

        public G810()
        {
            Name = "Logitech G810 RGB";
            Slug = "logitech-g810";
            CantEnableText = "Couldn't connect to your Logitech G810.\n" +
                             "Please check your cables and updating the Logitech Gaming Software\n" +
                             "A minimum version of 8.81.15 is required.\n\n" +
                             "If needed, you can select a different keyboard in Artemis under settings.";
            Height = 6;
            Width = 21;
            PreviewSettings = new PreviewSettings(675, 185, new Thickness(0, 35, 0, 0), Resources.g810);
            _generalSettings = SettingsProvider.Load<GeneralSettings>();
        }

        public override KeyMatch? GetKeyPosition(Keys keyCode)
        {
            return _generalSettings.Layout == "Qwerty"
                ? KeyMap.QwertyLayout.FirstOrDefault(k => k.KeyCode == keyCode)
                : KeyMap.AzertyLayout.FirstOrDefault(k => k.KeyCode == keyCode);
        }
    }
}