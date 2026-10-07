namespace MapChanger;

// Resolve the optional interface from the registered instance, not a mandatory
// assembly type reference. Ordinary timer servers may load an older contract.
internal sealed class OptionalBattleInterface
{
    private readonly object _instance;
    private readonly Type _contract;
    public OptionalBattleInterface(object instance, string contractName)
    {
        _instance = instance;
        _contract = instance.GetType().GetInterface(contractName)
            ?? throw new InvalidOperationException("Registered battle provider lacks " + contractName);
    }
    private object? Invoke(string method, params object[] args)
        => (_contract.GetMethod(method) ?? throw new MissingMethodException(_contract.FullName, method)).Invoke(_instance,args);
    public bool DeferRtvChange() => (bool)Invoke("DeferRtvChange")!;
    public void CancelRtvChange() => Invoke("CancelRtvChange");
    public bool ReadyForMapVote => (bool)Invoke("get_ReadyForMapVote")!;
    public void SetMapVoteActive(bool active) => Invoke("SetMapVoteActive",active);
}
