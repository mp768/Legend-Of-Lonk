// Produces the command an entity should execute this physics tick, or null for none.
// Implementations must be Nodes that are children of the entity, so they can be swapped at runtime.
public interface IInputProvider
{
    ICommand FetchNextCommand();
}
