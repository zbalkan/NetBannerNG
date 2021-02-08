using System.Drawing;
using System.Windows.Forms;

namespace NetBannerNG
{
    public class Dispatcher
    {
        private readonly Setting settings;

        public Dispatcher()
        {
            settings = ReadRegistry();
        }

        public Banner DrawBanner()
        {
            var banner = new Banner
            {
                BackColor = settings.CustomSettings != null ?
                            settings.CustomSettings.CustomBackgroundColor.Color :
                            settings.Classification.BackgroundColor,
                ClassificationLabel = WriteClassification(settings),
                ConditionLabel = WriteCon(settings)
            };

            return banner;
        }

        private Label WriteClassification(Setting setting)
        {
            if (!string.IsNullOrEmpty(setting.CustomSettings.CustomDisplayText))
            {
                // Custom settings override other settings
                var customLabel = new ClassificationLabel
                {
                    Text = setting.CustomSettings.CustomDisplayText,
                    ForeColor = setting.CustomSettings.CustomForeColor.Color
                };
                return customLabel;
            }

            // Plain clasification
            var label = new ClassificationLabel
            {
                Text = setting.Classification.ClassificationName,
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
            var separator = " | ";
            var text = string.Join(separator, setting.ForceProtectionCondition.ConditionLevel ?? string.Empty, setting.InformationOperationCondition.ConditionLevel ?? string.Empty);
            if (text.Equals(separator))
            {
                return null;
            }
            return new ConditionLabel
            {
                Text = text,
                ForeColor = setting.Classification.TextColor
            };
        }

        private Setting ReadRegistry()
        {
            var registry = new RegistrySetting();

            var settings = new Setting
            {
                Classification = GetClassification(registry.Classification),
                Caveats = registry.CaveatsEnabled == 1 ? registry.Caveats : null,
                ForceProtectionCondition = GetFpCon(registry.FpCon),
                InformationOperationCondition = GetInfoCon(registry.InfoCon),
                CustomSettings = GetCustomSettings((CustomBackgroundColors)registry.CustomBackgroundColor, (CustomForeColors)registry.CustomForeColor, registry.CustomDisplayText)
            };
            return settings;
        }

        private ClassificationMark GetClassification(int value)
        {
            // In order to minimize instance creation for Backgroundcolor and foreColor classes, the colors are defined manually.
            // Since the valuees are hardcoded and not read from registry, therefore it's not needed in this use case.
            // It is possible to add these into Resources but it's overhead is unnecessary.
            ClassificationMark[] classifications = {
                new ClassificationMark(){ClassificationName ="UNCLASSIFIED", BackgroundColor = Color.Green, TextColor = Color.White },
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

        private ConditionMark GetFpCon(int value)
        {

            string fpCon;
            switch (value)
            {
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
                default:
                    fpCon = null;
                    break;
            }
            return new ConditionMark() { ConditionLevel = fpCon };
        }

        private ConditionMark GetInfoCon(int value)
        {
            return value < 1 || value > 5
                ? new ConditionMark() { ConditionLevel = null }
                : new ConditionMark() { ConditionLevel = value.ToString() };
        }

        private CustomSettings GetCustomSettings(CustomBackgroundColors bgColor, CustomForeColors foreColor, string displayText) => new CustomSettings(bgColor, foreColor, displayText);
    }
}