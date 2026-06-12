namespace UlanziAdapter.Core.Input;

public interface IInputSource : IDisposable
{
    bool IsRunning { get; }

    void Start(Func<InputEvent, bool> handler);

    void Stop();
}
