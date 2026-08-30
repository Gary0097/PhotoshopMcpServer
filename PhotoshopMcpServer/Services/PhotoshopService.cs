using System.Runtime.InteropServices;
using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

// Service for controlling Adobe Photoshop via Windows COM automation.
// Uses late binding (dynamic) to avoid requiring the Photoshop type library.
public sealed class PhotoshopService : IPhotoshopService, IDisposable
{
    private dynamic _photoshopApplication;
    private bool _disposed;

    private const string PhotoshopProgId = "Photoshop.Application";

    // Marshal.GetActiveObject was removed from modern .NET; replicate it via P/Invoke.
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private static object GetActiveComObject(string progId)
    {
        var clsid = Type.GetTypeFromProgID(progId)?.GUID
            ?? throw new InvalidOperationException("没有找到 Photoshop 自动控制接口，请重新运行一键安装。");
        GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
        return instance;
    }

    public bool IsPhotoshopRunning()
    {
        try
        {
            var runningObject = GetActiveComObject(PhotoshopProgId);
            return runningObject != null;
        }
        catch (COMException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void LaunchPhotoshop()
    {
        if (IsPhotoshopRunning())
        {
            ConnectToRunningInstance();
            return;
        }

        var photoshopType = Type.GetTypeFromProgID(PhotoshopProgId)
            ?? throw new InvalidOperationException(
                "没有找到 Photoshop，或自动控制接口尚未就绪。请先启动一次 Photoshop，再重新运行一键安装。");

        _photoshopApplication = Activator.CreateInstance(photoshopType)
            ?? throw new InvalidOperationException("Photoshop 启动失败，请确认软件已激活且当前没有许可弹窗。");
    }

    private void ConnectToRunningInstance()
    {
        try
        {
            _photoshopApplication = GetActiveComObject(PhotoshopProgId);
        }
        catch (COMException comException)
        {
            throw new InvalidOperationException(
                "无法连接已经打开的 Photoshop。请保存工作，完全关闭 Photoshop 后重试。", comException);
        }
    }

    private dynamic GetApplication()
    {
        if (_photoshopApplication != null)
            return _photoshopApplication;

        if (IsPhotoshopRunning())
            ConnectToRunningInstance();
        else
            LaunchPhotoshop();

        return _photoshopApplication
            ?? throw new InvalidOperationException("无法连接 Photoshop。请保存工作，完全关闭 Photoshop 后重试。");
    }

    public string ExecuteJavaScript(string script)
    {
        var application = GetApplication();
        try
        {
            // DoJavaScript(script, arguments, ExecutionMode)
            // ExecutionMode 1 = psDisplayDialogs (show dialogs), 2 = psNoDialogs (suppress)
            var result = application.DoJavaScript(script, null, 2);
            return result?.ToString() ?? string.Empty;
        }
        catch (COMException comException)
        {
            throw new InvalidOperationException(
                "图片软件没有完成当前操作。请关闭弹窗后重试；仍失败时说“生成故障报告”。", comException);
        }
    }

    public PhotoshopScriptResult ExecuteJavaScriptWithResult(string script)
    {
        try
        {
            var result = ExecuteJavaScript(script);
            return new PhotoshopScriptResult(true, result, string.Empty);
        }
        catch (Exception exception)
        {
            return new PhotoshopScriptResult(false, string.Empty, exception.Message);
        }
    }

    public PhotoshopDocumentInfo GetActiveDocumentInfo()
    {
        var application = GetApplication();
        try
        {
            dynamic document = application.ActiveDocument;
            return new PhotoshopDocumentInfo(
                Name: document.Name?.ToString() ?? string.Empty,
                FilePath: document.FullName?.ToString() ?? string.Empty,
                Width: (int)(double)document.Width,
                Height: (int)(double)document.Height,
                ColorMode: document.Mode?.ToString() ?? string.Empty,
                Resolution: (double)document.Resolution
            );
        }
        catch (COMException comException)
        {
            throw new InvalidOperationException(
                "无法读取 Photoshop 当前图片。请先在 Photoshop 中打开任务工作副本。", comException);
        }
    }

    public IReadOnlyList<string> GetOpenDocumentNames()
    {
        var application = GetApplication();
        try
        {
            dynamic documents = application.Documents;
            int documentCount = (int)documents.Count;
            var documentNames = new List<string>(documentCount);
            for (int index = 1; index <= documentCount; index++)
                documentNames.Add(documents[index].Name?.ToString() ?? string.Empty);
            return documentNames;
        }
        catch (COMException comException)
        {
            throw new InvalidOperationException("无法读取 Photoshop 已打开的图片，请关闭弹窗后重试。", comException);
        }
    }

    public string GetPhotoshopVersion()
    {
        var application = GetApplication();
        try
        {
            return application.Version?.ToString() ?? string.Empty;
        }
        catch (COMException comException)
        {
            throw new InvalidOperationException(
                "无法读取 Photoshop 版本，请确认软件已正常启动且没有弹窗。", comException);
        }
    }

    public void Disconnect()
    {
        if (_photoshopApplication == null)
            return;

        Marshal.ReleaseComObject(_photoshopApplication);
        _photoshopApplication = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disconnect();
        _disposed = true;
    }
}
