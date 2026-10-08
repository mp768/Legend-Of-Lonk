// A room child that reacts to its room becoming active or inactive.
public interface IResettableEntity
{
    void OnRoomEntered();

    void OnRoomExited() { }
}
