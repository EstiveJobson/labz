using System.Windows.Threading;

namespace Labz.App;

public sealed class WpfMainThread : Labz.Modules.Circuits.IMainThread
{
    private readonly Dispatcher _dispatcher;

    public WpfMainThread()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }
}
