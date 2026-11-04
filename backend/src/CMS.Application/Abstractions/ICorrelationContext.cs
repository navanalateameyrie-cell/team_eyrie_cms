namespace CMS.Application.Abstractions;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
