namespace CMS.Domain.Entities;

public enum ImportDecision
{
    Pending = 0,
    CreateNew = 1,
    UpdateExisting = 2,
    Skip = 3,
}
