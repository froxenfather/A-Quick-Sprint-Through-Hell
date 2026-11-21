// the two small contracts the objects in the world use.
// the namespace stays IObjects, because CustomPlayerController refers to it
namespace IObjects
{
    // something a PressurePlate can switch on and off (the Door is one)
    public interface IPowered
    {
        void SwitchOn();
        void SwitchOff();
    }

    // something that reacts when the player touches it (the launch pad is one).
    // CustomPlayerController calls this
    public interface IPlayerContact
    {
        void OnPlayerContact(CustomPlayerController player);
    }
}
