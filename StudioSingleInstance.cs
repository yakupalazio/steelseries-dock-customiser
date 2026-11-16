using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Exactly one interactive OLED owner per Windows session, even when different
/// published copies of the application are launched. Diagnostic CLI commands
/// intentionally do not take this lock and never start the normal OLED UI.
/// </summary>
internal sealed class StudioSingleInstance : IDisposable
{
    private const string Identifier="SteelseriesDockCustomiser.InteractiveStudio.v1";
    private readonly Mutex mutex;
    private readonly string pipeName;
    private readonly CancellationTokenSource closing=new();
    private readonly Task listener;
    private Form? mainWindow;
    private Action? bringToFront;
    private int pendingRestore;
    private bool disposed;

    private static string Name(string? testId) =>
        Identifier+(string.IsNullOrWhiteSpace(testId)?"":"."+testId);
    private static string Pipe(string? testId) =>
        Name(testId)+"."+Process.GetCurrentProcess().SessionId;

    private StudioSingleInstance(Mutex owner,string pipe)
    {
        mutex=owner;
        pipeName=pipe;
        listener=Task.Run(ListenAsync);
    }

    internal static StudioSingleInstance? TryAcquire(string? testId=null)
    {
        var mutex=new Mutex(true,@"Local\"+Name(testId),out bool first);
        if(!first)
        {
            mutex.Dispose();
            return null;
        }
        return new StudioSingleInstance(mutex,Pipe(testId));
    }

    /// <summary>Signal the existing instance without opening HID or a form.</summary>
    internal static bool NotifyExisting(string? testId=null)
    {
        try
        {
            using var client=new NamedPipeClientStream(".",Pipe(testId),
                PipeDirection.Out,PipeOptions.None);
            client.Connect(1800);
            using var writer=new StreamWriter(client){AutoFlush=true};
            writer.WriteLine("SHOW");
            return true;
        }
        catch(Exception ex) when(ex is IOException or TimeoutException
            or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    internal void Bind(Form window,Action restore)
    {
        mainWindow=window;
        bringToFront=restore;
        window.HandleCreated+=(_,_)=>DispatchRestore();
        DispatchRestore();
    }

    private async Task ListenAsync()
    {
        while(!closing.IsCancellationRequested)
        {
            try
            {
                using var server=new NamedPipeServerStream(pipeName,
                    PipeDirection.In,1,PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(closing.Token);
                using var reader=new StreamReader(server);
                if(await reader.ReadLineAsync(closing.Token)=="SHOW")
                {
                    Interlocked.Exchange(ref pendingRestore,1);
                    DispatchRestore();
                }
            }
            catch(OperationCanceledException) {break;}
            catch(IOException) when(!closing.IsCancellationRequested)
            {
                // A disconnected secondary launch cannot terminate the owner.
            }
            catch(UnauthorizedAccessException) {break;}
            catch(ObjectDisposedException) {break;}
        }
    }

    private void DispatchRestore()
    {
        Form? window=mainWindow;
        if(window is null||window.IsDisposed||!window.IsHandleCreated)return;
        try
        {
            window.BeginInvoke((Action)(()=>
            {
                if(window.IsDisposed)return;
                if(Interlocked.Exchange(ref pendingRestore,0)==1)
                    bringToFront?.Invoke();
            }));
        }
        catch(InvalidOperationException) { /* Form shutting down. */ }
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        closing.Cancel();
        // Do not wait on the UI thread for a remote client to disconnect.
        try{mutex.ReleaseMutex();}catch(ApplicationException){}
        mutex.Dispose();
        closing.Dispose();
        _=listener.Exception;
    }
}
