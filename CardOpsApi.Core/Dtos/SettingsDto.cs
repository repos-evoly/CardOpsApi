namespace CardOpsApi.Core.Dtos
{
    public class SettingsDto
    {
        public int TopAtmRefundLimit { get; set; }
        public int TopReasonLimit { get; set; }
         public string FisBankAccount {get; set;}
    }

    public class SettingsPatchDto
    {

        public int? TopAtmRefundLimit { get; set; }
        public int? TopReasonLimit { get; set; }
         public string FisBankAccount {get; set;}
    }
}
