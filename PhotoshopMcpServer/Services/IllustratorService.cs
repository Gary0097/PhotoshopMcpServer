using System.Runtime.InteropServices;
using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

// Uses late-bound COM so the server can be built without an Illustrator type library.
public sealed class IllustratorService : IIllustratorService, IDisposable
{
    private const string IllustratorProgId = "Illustrator.Application";
    private dynamic _illustratorApplication;
    private bool _disposed;

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    private static object GetActiveComObject()
    {
        var classId = Type.GetTypeFromProgID(IllustratorProgId)?.GUID
            ?? throw new InvalidOperationException(
                "Adobe Illustrator is not installed or its COM registration is missing.");
        GetActiveObject(ref classId, IntPtr.Zero, out var instance);
        return instance;
    }

    public bool IsIllustratorRunning()
    {
        try
        {
            return GetActiveComObject() != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void LaunchIllustrator()
    {
        if (IsIllustratorRunning())
        {
            _illustratorApplication = GetActiveComObject();
            return;
        }

        var illustratorType = Type.GetTypeFromProgID(IllustratorProgId)
            ?? throw new InvalidOperationException(
                "Adobe Illustrator is not installed or its COM registration is missing.");
        _illustratorApplication = Activator.CreateInstance(illustratorType)
            ?? throw new InvalidOperationException("Failed to create Illustrator COM instance.");
    }

    private dynamic GetApplication()
    {
        if (_illustratorApplication != null)
            return _illustratorApplication;
        LaunchIllustrator();
        return _illustratorApplication;
    }

    public string GetIllustratorVersion()
        => GetApplication().Version?.ToString() ?? string.Empty;

    public string GetActiveDocumentName()
    {
        try
        {
            return GetApplication().ActiveDocument?.Name?.ToString() ?? string.Empty;
        }
        catch (COMException exception)
        {
            throw new InvalidOperationException(
                "Illustrator has no accessible active document.", exception);
        }
    }

    public IllustratorScriptResult ExecuteJavaScriptWithResult(string script)
    {
        try
        {
            var result = GetApplication().DoJavaScript(script)?.ToString() ?? string.Empty;
            return new IllustratorScriptResult(true, result, string.Empty);
        }
        catch (Exception exception)
        {
            return new IllustratorScriptResult(false, string.Empty, exception.Message);
        }
    }

    public void Disconnect()
    {
        if (_illustratorApplication == null)
            return;
        Marshal.ReleaseComObject(_illustratorApplication);
        _illustratorApplication = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Disconnect();
        _disposed = true;
    }
}
