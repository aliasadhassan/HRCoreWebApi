namespace HR.Employee.API.Domain.Policies;

public enum PolicyStatus : byte { Draft = 1, Published = 2, Archived = 3 }

public enum PolicyCategory : byte
{
    General = 1, Conduct = 2, Workplace = 3, TimeOff = 4, Pay = 5, Security = 6, HealthSafety = 7, Legal = 8
}

public enum ObligationCategory : byte { Tax = 1, SocialSecurity = 2, Labour = 3, Licence = 4, Reporting = 5, Other = 6 }

public enum ObligationFrequency : byte { Once = 1, Monthly = 2, Quarterly = 3, HalfYearly = 4, Annually = 5 }
