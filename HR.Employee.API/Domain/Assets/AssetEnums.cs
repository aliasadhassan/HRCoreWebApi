namespace HR.Employee.API.Domain.Assets;

public enum AssetStatus : byte { Available = 1, Assigned = 2, InRepair = 3, Retired = 4, Lost = 5 }

public enum AssetCondition : byte { New = 1, Good = 2, Fair = 3, Poor = 4, Damaged = 5 }

public enum AssetEventType : byte { Created = 1, Updated = 2, Assigned = 3, Returned = 4, StatusChanged = 5, Acknowledged = 6 }
