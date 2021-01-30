namespace NetBannerNG
{
    public class Setting
    {
        public ClassificationMark Classification { get; set; }
        public string Caveats { get; set; }
        public ConditionMark ForceProtectionCondition { get; set; }
        public ConditionMark InformationOperationCondition { get; set; }
        public CustomSettings CustomSettings { get; set; }
    }
}
