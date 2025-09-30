namespace GGs.Shared.Enums;

public enum LicenseTier
{
    Basic = 1,
    Pro = 2,
    Enterprise = 3,
    Admin = 9
}

public enum SafetyLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Experimental = 3
}

public enum RiskLevel
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum CommandType
{
    Registry = 0,
    Service = 1,
    Script = 2,
    Policy = 3
}

public enum ServiceAction
{
    Start = 0,
    Stop = 1,
    Restart = 2,
    Enable = 3,
    Disable = 4
}

public enum RbacRole
{
    Basic = 1,
    Pro = 2,
    Enterprise = 3,
    Moderator = 4,
    Admin = 5,
    Owner = 6
}