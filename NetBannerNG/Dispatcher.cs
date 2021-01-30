using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NetBannerNG
{
    public class Dispatcher
    {

        private readonly Setting settings;

        public Dispatcher()
        {
            settings = MapToPOCO(ReadRegistry());
        }

        public Banner DrawBanner()
        {
            var banner = new Banner
            {
                BackColor = settings.CustomSettings != null ?
                            settings.CustomSettings.ConvertBackgroundColor() :
                            settings.Classification.BackgroundColor,
                ClassificationLabel = WriteClassification(settings)
            };


            if (settings.ForceProtectionCondition != null || settings.InformationOperationCondition != null)
            {
                banner.ConditionLabel = WriteCon(settings);
            }

            return banner;
        }


        private RegistrySetting ReadRegistry()
        {
            RegistrySetting bannerSettings = null;
            if (RegistryHelper.ConnectRegistry())
            {
                bannerSettings = new RegistrySetting()
                {
                    Classification = RegistryHelper.GetClassification(),
                    CaveatsEnabled = RegistryHelper.GetCaveatsEnabled(),

                    FpCon = RegistryHelper.GetFpCon(),
                    InfoCon = RegistryHelper.GetInfoCon(),

                };
                if (RegistryHelper.GetCaveatsEnabled() == 1)
                {
                    bannerSettings.Caveats = RegistryHelper.GetCaveat();
                }
                if (RegistryHelper.GetCustomSettingsKey() == 1)
                {
                    bannerSettings.CustomSettings = RegistryHelper.GetCustomSettingsKey();
                    bannerSettings.CustomDisplayText = RegistryHelper.GetCustomDisplayText();
                    bannerSettings.CustomBackgroundColor = RegistryHelper.GetCustomBackgroundColor();
                    bannerSettings.CustomForeColor = RegistryHelper.GetCustomForeColor();
                }
                RegistryHelper.DisconnectRegistry();
            }

            return bannerSettings;
        }

        private Setting MapToPOCO(RegistrySetting registry)
        {
            var settings = new Setting
            {
                Classification = GetClassification(registry.Classification),
                Caveats = registry.CaveatsEnabled == 1 ? registry.Caveats : null,
                ForceProtectionCondition = GetFpCon(registry.FpCon),
                InformationOperationCondition = GetInfoCon(registry.InfoCon),
                CustomSettings = GetCustomSettings(registry.CustomBackgroundColor, registry.CustomForeColor, registry.CustomDisplayText)
            };
            return settings;
        }

        private ClassificationMark GetClassification(int value)
        {
            var classifications = new List<ClassificationMark>
            {
                new ClassificationMark(){ ClassificationName ="UNCLASSIFIED", BackgroundColor = Color.Green, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="SECRET", BackgroundColor = Color.Blue, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="TOP SECRET", BackgroundColor = Color.Red, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="SCI", BackgroundColor = Color.Red, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO UNCLASSIFIED", BackgroundColor = Color.Green, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO RESTRICTED", BackgroundColor = Color.Blue, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO CONFIDENTIAL", BackgroundColor = Color.Blue, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO SECRET", BackgroundColor = Color.Red, TextColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO TOP SECRET", BackgroundColor = Color.Red, TextColor = Color.White }
            };

            return classifications[value - 1];
        }

        private ConditionMark GetFpCon(int? value)
        {
            if (value.HasValue)
            {
                string fpCon;
                switch (value)
                {
                    default:
                    case 1:
                        fpCon = "ALPHA";
                        break;
                    case 2:
                        fpCon = "BETA";
                        break;
                    case 3:
                        fpCon = "CHARLIE";
                        break;
                    case 4:
                        fpCon = "DELTA";
                        break;
                }
                return new ConditionMark() { ConditionLevel = fpCon };
            }
            return null;
        }

        private ConditionMark GetInfoCon(int? value)
        {
            if (value.HasValue)
            {
                return new ConditionMark() { ConditionLevel = value.ToString() };
            }
            return null;
        }

        private CustomSettings GetCustomSettings(int bgcolor, int foreColor, string displayText)
        {
            return new CustomSettings
            {
                CustomBackgroundColor = (CustomBackgroundColor)(bgcolor),
                CustomForeColor = (CustomForeColor)(foreColor),
                CustomDisplayText = displayText
            };
        }

        private Label WriteClassification(Setting setting)
        {
            if (!string.IsNullOrEmpty(setting.CustomSettings.CustomDisplayText))
            {
                // Custom settings override other settings
                var customLabel = new Label
                {
                    AutoSize = true,
                    Name = "ClassificationLabel",
                    Size = new Size(20, 20),
                    Dock = DockStyle.None,
                    Text = setting.CustomSettings.CustomDisplayText,
                    Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold),
                    ForeColor = setting.CustomSettings.ConvertForeColor()
                };
                return customLabel;
            }

            // Plain clasification
            var label = new Label
            {
                AutoSize = true,
                Name = "ClassificationLabel",
                Size = new Size(20, 20),
                Dock = DockStyle.None,
                Text = setting.Classification.ClassificationName,
                Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold),
                ForeColor = setting.Classification.TextColor
            };

            // With Caveats
            if (setting.Caveats != null)
            {
                label.Text = $"{setting.Classification.ClassificationName} RELEASABLE TO {setting.Caveats}";
            }

            return label;
        }

        private Label WriteCon(Setting setting)
        {
            var text = string.Join(" | ", setting.ForceProtectionCondition.ConditionLevel ?? string.Empty, setting.InformationOperationCondition.ConditionLevel ?? string.Empty);

            return new Label
            {
                AutoSize = true,
                Name = "ConLabel",
                Size = new Size(20, 14),
                Text = text,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = setting.Classification.TextColor
            };
        }
    }
}