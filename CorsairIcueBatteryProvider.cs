using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Optional official Corsair iCUE SDK v4 battery reader (CDPI_BatteryLevel).
/// No SDK binaries are bundled or downloaded. The user must separately
/// supply the official x64 redistributable from Corsair in VendorSDK/Corsair.
/// All native calls are Connect/DeviceInfo/ReadProperty/FreeProperty only.
/// No lighting control, exclusive access, input capture or HID writes.
/// </summary>
internal sealed class CorsairIcueBatteryProvider:IPeripheralBatteryProvider
{
    public string Protocol=>"Corsair iCUE SDK BatteryLevel (official)";
    private const int BatteryProperty=9;
    private const int Int32Type=1;
    private const int CanReadFlag=1;
    private const int MaximumDevices=64;
    private const int DeviceInfoBytes=396; // 4-byte type + 3x128 char + 2x int

    internal static string OfficialSdkPath=>Path.Combine(
        AppContext.BaseDirectory,"VendorSDK","Corsair",
        "iCUESDK.x64_2019.dll");

    internal static string LastDiagnostic {get;private set;}=
        "Corsair iCUE SDK henüz sorgulanmadı.";

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)]
    private struct CorsairDeviceInfo
    {
        public int Type;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
        public string Id;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
        public string Serial;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
        public string Model;
        public int LedCount;
        public int ChannelCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CorsairDeviceFilter {public int DeviceTypeMask;}

    // iCUE SDK C ABI, not the legacy Corsair CUE SDK.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StateCallback(IntPtr context,IntPtr stateEvent);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ConnectFn(StateCallback callback,IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDevicesFn(ref CorsairDeviceFilter filter,
        int capacity,IntPtr devices,out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,
        CharSet=CharSet.Ansi)]
    private delegate int GetPropertyInfoFn(
        [MarshalAs(UnmanagedType.LPStr)]string deviceId,
        int propertyId,uint index,out int type,out uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,
        CharSet=CharSet.Ansi)]
    private delegate int ReadPropertyFn(
        [MarshalAs(UnmanagedType.LPStr)]string deviceId,
        int propertyId,uint index,IntPtr property);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FreePropertyFn(IntPtr property);

    private static readonly object Gate=new();
    private static int activeRequest;
    private static IntPtr nativeModule;
    private static StateCallback? stateCallback;
    private static ConnectFn? connect;
    private static GetDevicesFn? getDevices;
    private static GetPropertyInfoFn? getPropertyInfo;
    private static ReadPropertyFn? readProperty;
    private static FreePropertyFn? freeProperty;
    private static int sessionState; // CSS_Connected = 6.
    private static bool connectRequested;
    private static DateTimeOffset nextLoadAttempt=DateTimeOffset.MinValue;

    internal static bool HasOfficialSdkFile=>File.Exists(OfficialSdkPath);

    internal static string StableKind(string id)
    {
        if(string.IsNullOrWhiteSpace(id))return "";
        string hash=Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(id)),0,7);
        return "Vendor:Corsair:"+hash;
    }

    internal static BatteryReading? CheckedReading(
        string deviceId,string model,int dataType,uint flags,int percent,
        DateTimeOffset at)
    {
        if(dataType!=Int32Type||(flags&CanReadFlag)==0 ||
           percent is <0 or >100 ||string.IsNullOrWhiteSpace(deviceId))
            return null;
        string kind=StableKind(deviceId);
        string name=string.IsNullOrWhiteSpace(model)?
            "Corsair wireless device":model.Trim();
        if(name.Length>96)name=name[..96];
        return new BatteryReading(kind,percent,
            "Corsair iCUE SDK (read-only)",
            "Official SDK BatteryLevel (live reading)",at)
        {
            FriendlyName=name,
            IconKind=BatteryDeviceClassifier.IconFor(name,"USB")
        };
    }

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!HasOfficialSdkFile)
        {
            LastDiagnostic="Corsair SDK dosyası bulunamadı. "+
                "VendorSDK/Corsair/iCUESDK.x64_2019.dll";
            return Array.Empty<BatteryReading>();
        }
        if(Interlocked.CompareExchange(ref activeRequest,1,0)!=0)
            return Array.Empty<BatteryReading>();
        Task<IReadOnlyList<BatteryReading>> read=Task.Run(()=>{
            try{return ReadWithSdk();}
            finally{Interlocked.Exchange(ref activeRequest,0);}
        });
        // A nonresponsive iCUE native DLL must never stall OLED refresh.
        var completed=await Task.WhenAny(read,Task.Delay(1600));
        if(completed==read)return await read;
        LastDiagnostic="Corsair SDK yanıtı gecikti; diğer pil "+
            "okuyucuları çalışmaya devam ediyor.";
        return Array.Empty<BatteryReading>();
    }

    private static IReadOnlyList<BatteryReading> ReadWithSdk()
    {
        if(!OperatingSystem.IsWindows()||!Environment.Is64BitProcess)
            return Array.Empty<BatteryReading>();
        // SDK is optional; it is not installed/launched by this application.
        if(!File.Exists(OfficialSdkPath))
        {
            LastDiagnostic="Corsair iCUE SDK dosyası eklenmedi. "+
                "Resmî x64 SDK: VendorSDK/Corsair/iCUESDK.x64_2019.dll";
            return Array.Empty<BatteryReading>();
        }
        lock(Gate)
        {
            try
            {
                if(!EnsureConnected())
                    return Array.Empty<BatteryReading>();
                var filter=new CorsairDeviceFilter{DeviceTypeMask=unchecked((int)0xFFFFFFFF)};
                IntPtr buffer=Marshal.AllocHGlobal(DeviceInfoBytes*MaximumDevices);
                try
                {
                    // Insecure / uninitialized pointers never enter the API.
                    // Each native struct is fixed-size and read at most 64x.
                    int code=getDevices!(ref filter,MaximumDevices,buffer,out int count);
                    if(code!=0)
                    {
                        LastDiagnostic="Corsair SDK bağlı fakat cihaz listesi "+
                            "okunamıyor (SDK "+code+").";
                        return Array.Empty<BatteryReading>();
                    }
                    if(count is <0 or >MaximumDevices)
                    {
                        LastDiagnostic="Corsair SDK geçersiz cihaz sayısı döndürdü.";
                        return Array.Empty<BatteryReading>();
                    }
                    var results=new List<BatteryReading>();
                    for(int i=0;i<count;i++)
                    {
                        var info=Marshal.PtrToStructure<CorsairDeviceInfo>(
                            IntPtr.Add(buffer,i*DeviceInfoBytes));
                        if(string.IsNullOrWhiteSpace(info.Id))continue;
                        int type;
                        uint flags;
                        if(getPropertyInfo!(info.Id,BatteryProperty,0,out type,
                            out flags)!=0 ||type!=Int32Type ||
                            (flags&CanReadFlag)==0)continue;
                        // CorsairProperty C structure: int type at byte 0,
                        // alignment to an 8-byte union beginning at byte 8.
                        IntPtr nativeProperty=Marshal.AllocHGlobal(32);
                        bool propertyAllocated=false;
                        try
                        {
                            for(int j=0;j<32;j+=4)
                                Marshal.WriteInt32(nativeProperty,j,0);
                            if(readProperty!(info.Id,BatteryProperty,0,
                                nativeProperty)!=0)continue;
                            propertyAllocated=true;
                            int returnedType=Marshal.ReadInt32(nativeProperty,0);
                            int percent=Marshal.ReadInt32(nativeProperty,8);
                            var reading=CheckedReading(info.Id,info.Model,
                                returnedType,flags,percent,DateTimeOffset.Now);
                            if(reading is not null)
                            {
                                BatteryDeviceCatalog.Register(reading.Kind,
                                    reading.FriendlyName,reading.IconKind);
                                results.Add(reading);
                            }
                        }
                        finally
                        {
                            // SDK allocates backing memory inside the property
                            // only on a successful read. Releasing the struct
                            // itself is our responsibility separately.
                            // For Int32 property there is no nested allocation;
                            // call the official matching free routine anyway.
                            if(propertyAllocated)
                            {
                                try{freeProperty!(nativeProperty);}catch{}
                            }
                            Marshal.FreeHGlobal(nativeProperty);
                        }
                    }
                    LastDiagnostic=results.Count>0
                        ?"Corsair iCUE canlı pil kaynakları okundu."
                        :"iCUE açık; desteklenen cihazlardan BatteryLevel "+
                            "özelliği sağlanmıyor.";
                    return results;
                }
                finally{Marshal.FreeHGlobal(buffer);}
            }
            catch(Exception ex) when(ex is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException or
                ExternalException or InvalidOperationException or
                UnauthorizedAccessException or ArgumentException)
            {
                LastDiagnostic="Corsair SDK okuyucusu: "+ex.GetType().Name;
                return Array.Empty<BatteryReading>();
            }
        }
    }

    private static bool EnsureConnected()
    {
        if(nativeModule==IntPtr.Zero)
        {
            if(DateTimeOffset.UtcNow<nextLoadAttempt)return false;
            nextLoadAttempt=DateTimeOffset.UtcNow.AddSeconds(45);
            if(!NativeLibrary.TryLoad(OfficialSdkPath,out nativeModule))
            {
                LastDiagnostic="Resmî Corsair SDK DLL dosyası yüklenemedi.";
                return false;
            }
            try
            {
                connect=Load<ConnectFn>("CorsairConnect");
                getDevices=Load<GetDevicesFn>("CorsairGetDevices");
                getPropertyInfo=Load<GetPropertyInfoFn>(
                    "CorsairGetDevicePropertyInfo");
                readProperty=Load<ReadPropertyFn>("CorsairReadDeviceProperty");
                freeProperty=Load<FreePropertyFn>("CorsairFreeProperty");
                stateCallback=(_,stateEvent)=>{
                    if(stateEvent!=IntPtr.Zero)
                        Volatile.Write(ref sessionState,
                            Marshal.ReadInt32(stateEvent));
                };
            }
            catch
            {
                NativeLibrary.Free(nativeModule);
                nativeModule=IntPtr.Zero;
                LastDiagnostic="Corsair SDK sürümü gerekli fonksiyonları "+
                    "sağlamıyor.";
                return false;
            }
        }
        if(!connectRequested)
        {
            int error=connect!(stateCallback!,IntPtr.Zero);
            if(error!=0)
            {
                LastDiagnostic="Corsair SDK bağlantı isteği reddedildi: "+error;
                return false;
            }
            connectRequested=true;
        }
        if(Volatile.Read(ref sessionState)!=6)
        {
            LastDiagnostic="iCUE SDK henüz bağlanmadı. iCUE çalışıyor "+
                "ve üçüncü taraf SDK erişimi açık mı?";
            return false;
        }
        return true;
    }

    private static T Load<T>(string name) where T:Delegate=>
        Marshal.GetDelegateForFunctionPointer<T>(
            NativeLibrary.GetExport(nativeModule,name));

    internal static int NativeDeviceInfoSize=>Marshal.SizeOf<CorsairDeviceInfo>();
}
