namespace CMS.Domain.Entities;

/// <summary>Single-row counter per code family (currently "client"). Grown atomically by the generator.</summary>
public class ClientCodeCounter
{
    public string Name { get; private set; } = string.Empty;
    public long NextValue { get; private set; }

    private ClientCodeCounter() { }

    public ClientCodeCounter(string name, long nextValue)
    {
        Name = name;
        NextValue = nextValue;
    }
}
